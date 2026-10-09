"""Verifica la API detrás de Nginx (standards/13a) usando un contenedor Nginx real.

Uso:
  python scripts/verify/verify-nginx.py --project .verify/secure-sqlserver/KitVerify/src/KitVerify.Api \\
      --connection "Server=(localdb)\\MSSQLLocalDB;Trusted_Connection=True;TrustServerCertificate=True"

Requisitos: Docker, y un proyecto del perfil con seguridad ya generado por verify-template.py (build Release con migración).
Comprueba:
  1. Sin ReverseProxy, la API ve la IP de Nginx (el problema que se quiere evitar).
  2. Con ReverseProxy + KnownProxies, la API ve la IP real del cliente.
  3. Un X-Forwarded-For falsificado por el cliente no cambia la IP ni permite saltarse el rate limiting.
  4. Headers de seguridad, gzip y ProblemDetails llegan intactos a través del proxy.
"""
import argparse
import json
import os
import pathlib
import re
import shutil
import subprocess
import sys
import time
import urllib.error
import urllib.request

for _stream in (sys.stdout, sys.stderr):
    if hasattr(_stream, "reconfigure"):
        _stream.reconfigure(encoding="utf-8")

KIT = pathlib.Path(__file__).resolve().parents[2]
API_PORT, NGINX_PORT, CONTAINER = 5195, 8088, "kitverify-nginx"
results = []


def check(name, ok, detail=""):
    results.append(ok)
    print(("OK   " if ok else "FAIL ") + name + (f"  -> {detail}" if detail and not ok else ""))


def call(method, path, body=None, token=None, headers=None, port=NGINX_PORT):
    data = json.dumps(body).encode() if body is not None else None
    req = urllib.request.Request(f"http://localhost:{port}{path}", data=data, method=method)
    req.add_header("Content-Type", "application/json")
    for key, value in (headers or {}).items():
        req.add_header(key, value)
    if token:
        req.add_header("Authorization", f"Bearer {token}")
    try:
        with urllib.request.urlopen(req, timeout=30) as r:
            raw = r.read()
            return r.status, raw, dict(r.headers)
    except urllib.error.HTTPError as e:
        return e.code, e.read(), dict(e.headers)


def as_json(raw):
    try:
        return json.loads(raw.decode() or "null")
    except (UnicodeDecodeError, json.JSONDecodeError):
        return None


def nginx_local_conf() -> str:
    """Toma el nginx.conf de standards/13a y lo adapta a HTTP local (sin TLS) apuntando a la API del host."""
    md = (KIT / "standards" / "13a-reverse-proxy-nginx.md").read_text(encoding="utf-8")
    conf = re.search(r"## `deploy/nginx/nginx.conf`\n```nginx\n(.*?)```", md, re.S).group(1)
    head = conf[:conf.index("server {")]
    tls_server = conf[conf.rindex("server {"):]
    body = [l for l in tls_server.splitlines()[1:]
            if not re.match(r"\s*(listen|http2|ssl_|server_name)", l)]
    head = head.replace("server api:8080;", f"server host.docker.internal:{API_PORT};")
    return head + "server {\n    listen 80;\n" + "\n".join(body) + "\n"


def start_api(project: pathlib.Path, connection: str, proxy_ip: str | None) -> subprocess.Popen:
    env = dict(os.environ, ASPNETCORE_ENVIRONMENT="Development", ConnectionStrings__Default=connection,
               Database__ApplyMigrationsOnStartup="true", Jwt__Issuer="KitVerify", Jwt__Audience="KitVerify.Client",
               Jwt__SigningKey="verify-" + "k" * 60, Seed__AdminEmail="admin@verify.local",
               Seed__AdminPassword="Admin#Pass12345", App__ClientUrl="http://localhost:5173", App__AppName="KitVerify",
               Analytics__HashKey="verify-" + "h" * 60)
    if proxy_ip:
        env.update(ReverseProxy__Enabled="true", ReverseProxy__KnownProxies__0=proxy_ip)
    log = (project.parent / f"nginx-run-{'proxy' if proxy_ip else 'direct'}.log").open("w", encoding="utf-8")
    api = subprocess.Popen(["dotnet", "run", "--no-build", "-c", "Release", "--no-launch-profile", "--urls", f"http://0.0.0.0:{API_PORT}"],
                           cwd=project, env=env, stdout=log, stderr=subprocess.STDOUT)
    for _ in range(90):
        try:
            urllib.request.urlopen(f"http://localhost:{API_PORT}/health/ready", timeout=2)
            return api
        except OSError:
            time.sleep(1)
    api.kill()
    raise SystemExit("La API no arrancó")


def stop(api: subprocess.Popen):
    api.terminate()
    try:
        api.wait(timeout=15)
    except subprocess.TimeoutExpired:
        api.kill()


def session_ip(headers=None) -> str | None:
    s, raw, _ = call("POST", "/api/auth/login", {"email": "admin@verify.local", "password": "Admin#Pass12345"}, headers=headers)
    token = (as_json(raw) or {}).get("tokens", {}).get("accessToken") if s == 200 else None
    if not token:
        return None
    s, raw, _ = call("GET", "/api/account/sessions", token=token)
    sessions = as_json(raw) or []
    return sessions[0]["ipAddress"] if sessions else None


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--project", required=True)
    parser.add_argument("--connection", required=True, help="cadena de conexión SIN Database")
    args = parser.parse_args()
    project = pathlib.Path(args.project).resolve()
    if not shutil.which("docker"):
        raise SystemExit("Docker no está en el PATH")

    database = f"kitverify_nginx_{int(time.time())}"
    connection = f"{args.connection.rstrip(';')};Database={database}"
    conf = project.parent / "nginx.local.conf"
    conf.write_text(nginx_local_conf(), encoding="utf-8")
    subprocess.run(["dotnet", "build", "-nologo", "-v", "q"], cwd=project, check=True, capture_output=True)
    subprocess.run(["docker", "rm", "-f", CONTAINER], capture_output=True)
    subprocess.run(["docker", "run", "-d", "--name", CONTAINER, "-p", f"{NGINX_PORT}:80",
                    "--add-host", "host.docker.internal:host-gateway",
                    "-v", f"{conf}:/etc/nginx/conf.d/default.conf:ro", "nginx:stable-alpine"], check=True, capture_output=True)
    try:
        test = subprocess.run(["docker", "exec", CONTAINER, "nginx", "-t"], capture_output=True, text=True)
        check("nginx -t acepta la configuración de standards/13a", test.returncode == 0, test.stderr.strip())

        # 1. Sin ReverseProxy: la API ve la IP del proxy
        api = start_api(project, connection, proxy_ip=None)
        proxy_ip = session_ip()
        stop(api)
        check("sin ReverseProxy la API registra la IP de Nginx", bool(proxy_ip), f"ip={proxy_ip}")

        # 2. Con ReverseProxy confiando en ese proxy: IP real del cliente
        api = start_api(project, connection, proxy_ip=proxy_ip)
        try:
            client_ip = session_ip()
            check("con ReverseProxy la API registra la IP real del cliente (distinta de la de Nginx)",
                  bool(client_ip) and client_ip != proxy_ip, f"proxy={proxy_ip} cliente={client_ip}")

            # 3. X-Forwarded-For falsificado: no cambia la IP
            spoofed_ip = session_ip(headers={"X-Forwarded-For": "203.0.113.77"})
            check("un X-Forwarded-For falsificado no cambia la IP registrada", spoofed_ip == client_ip,
                  f"esperado={client_ip} obtenido={spoofed_ip}")

            # 4. Headers, gzip y ProblemDetails a través del proxy
            s, raw, h = call("GET", "/api/no-existe", headers={"Accept-Encoding": "gzip"})
            check("headers de seguridad de la API llegan por el proxy",
                  h.get("X-Content-Type-Options") == "nosniff" and "X-Trace-Id" in h, f"{h}")
            check("Nginx no expone su versión (server_tokens off)", h.get("Server", "").lower() in ("nginx", ""), h.get("Server"))
            s, raw, h = call("GET", "/openapi/v1.json", headers={"Accept-Encoding": "gzip"})
            check("gzip activo para respuestas JSON grandes", h.get("Content-Encoding") == "gzip", f"{s} {h.get('Content-Encoding')}")

            # 5. Rate limiting de Auth con X-Forwarded-For distinto en cada petición: igual llega el 429
            codes = []
            for i in range(14):
                s, _, h = call("POST", "/api/auth/forgot-password", {"email": "nadie@verify.local"},
                               headers={"X-Forwarded-For": f"198.51.100.{i + 1}"})
                codes.append(s)
                if s == 429:
                    break
            check("falsificar X-Forwarded-For no evita el rate limiting (429)", 429 in codes, f"{codes}")
        finally:
            stop(api)
    finally:
        subprocess.run(["docker", "rm", "-f", CONTAINER], capture_output=True)
        if shutil.which("sqlcmd") and "Server=" in args.connection:
            server = re.search(r"Server=([^;]+)", args.connection).group(1)
            subprocess.run(["sqlcmd", "-S", server, "-Q",
                            f"ALTER DATABASE [{database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{database}];"],
                           capture_output=True)

    print(f"\n{sum(results)}/{len(results)} OK")
    sys.exit(0 if all(results) else 1)


if __name__ == "__main__":
    main()
