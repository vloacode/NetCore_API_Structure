"""Verifica el asistente `dnx NetCoreApiKit` (tools/NetCoreApiKit) empaquetado como en NuGet.

Uso:
  python scripts/verify/verify-wizard.py            todos los casos (requiere Docker)
  python scripts/verify/verify-wizard.py --quick    solo el caso sin base de datos (sin Docker; lo corre la CI)

Casos:
  quick     pública + SQL Server + API key, base "después": genera, compila en Release, pasa format, guía copiada, perfil lleno.
  docker    con seguridad + PostgreSQL + SMTP, base en Docker: tablas creadas y `dotnet test` en verde.
  server    con seguridad + PostgreSQL contra un servidor ya existente (contenedor temporal): conexión probada,
            secretos en user-secrets + variables (scripts) + appsettings local, archivos ignorados por git, tablas NO creadas.
  appsettings  igual pero con secretos solo en appsettings local: la API arranca y /health/ready responde (AddLocalSettings).
  bad-password  conexión con contraseña incorrecta: el asistente falla con un error claro y no crea nada.

Todo se hace en .verify/wizard (ignorada por git). Las plantillas se instalan en un hive propio y los user-secrets
de prueba se borran al final. Código de salida 1 si algo falla.
"""
import argparse
import json
import os
import pathlib
import re
import secrets
import shutil
import subprocess
import sys
import time
import urllib.request

for _stream in (sys.stdout, sys.stderr):
    if hasattr(_stream, "reconfigure"):
        _stream.reconfigure(encoding="utf-8")

KIT = pathlib.Path(__file__).resolve().parents[2]
WORK = KIT / ".verify" / "wizard"
FEED = WORK / "feed"
HIVE = WORK / "hive"
VERSION = f"3.4.0-verify.{int(time.time())}"
PG_CONTAINER = "kitwizard-server-pg"
failures = []


def run(cmd, cwd, env=None, check=True):
    env = dict(env or os.environ, DOTNET_CLI_TELEMETRY_OPTOUT="1", PYTHONIOENCODING="utf-8")
    result = subprocess.run(cmd, cwd=cwd, capture_output=True, text=True, encoding="utf-8", errors="replace", env=env)
    if check and result.returncode != 0:
        raise RuntimeError(f"Falló: {' '.join(cmd)}\n" + "\n".join((result.stdout + result.stderr).splitlines()[-25:]))
    return result


def wizard(args, env=None, check=True):
    """Ejecuta el paquete como lo haría un usuario: dnx NetCoreApiKit -- <args>."""
    cmd = ["dotnet", "dnx", "NetCoreApiKit", "--version", VERSION, "--add-source", str(FEED), "--",
           *args, "--template-hive", str(HIVE), "--no-git", "--yes"]
    return run(cmd, WORK, env=env, check=check)


def check(label, condition, detail=""):
    print(f"  {'OK  ' if condition else 'FAIL'} {label}" + (f" — {detail}" if detail and not condition else ""))
    if not condition:
        failures.append(label)


def user_secrets_file(project: pathlib.Path, name: str) -> pathlib.Path:
    csproj = (project / "src" / f"{name}.Api" / f"{name}.Api.csproj").read_text(encoding="utf-8")
    secret_id = re.search(r"<UserSecretsId>([^<]+)</UserSecretsId>", csproj).group(1)
    base = (pathlib.Path(os.environ["APPDATA"]) / "Microsoft" / "UserSecrets") if os.name == "nt" \
        else pathlib.Path.home() / ".microsoft" / "usersecrets"
    return base / secret_id / "secrets.json"


def cleanup_secrets(project: pathlib.Path, name: str):
    if (project / "src").exists():
        shutil.rmtree(user_secrets_file(project, name).parent, ignore_errors=True)


def pack():
    if WORK.exists():
        shutil.rmtree(WORK, ignore_errors=True)
    FEED.mkdir(parents=True)
    run(["dotnet", "pack", str(KIT / "tools" / "NetCoreApiKit"), "-c", "Release", "-o", str(FEED), f"-p:Version={VERSION}", "-nologo"], KIT)
    print(f"paquete NetCoreApiKit {VERSION} listo")


def case_quick():
    print("== quick: pública + SQL Server + API key, base después")
    wizard(["--name", "WizQuick", "--public", "--apikey", "--database", "sqlserver", "--db-location", "later"])
    project = WORK / "WizQuick"
    run(["dotnet", "build", "WizQuick.slnx", "-c", "Release", "-nologo"], project)
    fmt = run(["dotnet", "format", "WizQuick.slnx", "--verify-no-changes"], project, check=False)
    check("compila en Release y pasa format", fmt.returncode == 0, fmt.stdout[-500:])
    profile = (project / "docs" / "00-MASTER_CONTEXT.md").read_text(encoding="utf-8")
    check("perfil lleno (Security, ApiKey, Database)",
          "| Security | disabled |" in profile and "| ApiKey | enabled |" in profile and "| Database | sqlserver |" in profile)
    check("guía para IA copiada", (project / "AGENTS.md").exists() and (project / "standards").exists() and (project / ".claude").exists())
    check("adaptadores de opencode y Hermes copiados",
          (project / "opencode.json").exists() and (project / ".opencode" / "agents").exists()
          and (project / ".opencode" / "commands").exists() and (project / ".agents" / "skills" / "project-init" / "SKILL.md").exists())
    check("sin archivos propios del kit", not (project / "templates").exists() and not (project / "scripts" / "verify").exists())
    check("migración inicial creada", any((project / "src" / "WizQuick.Api" / "Infrastructure" / "Persistence" / "Migrations").glob("*InitialCreate.cs")))
    kit_check = run([sys.executable, "scripts/check-kit.py"], project, check=False)
    check("check-kit del proyecto sin errores", kit_check.returncode == 0, kit_check.stdout[-400:])
    cleanup_secrets(project, "WizQuick")


def case_docker():
    print("== docker: con seguridad + PostgreSQL + SMTP, base en Docker")
    project = WORK / "WizDocker"
    try:
        out = wizard(["--name", "WizDocker", "--security", "--database", "postgresql", "--email", "smtp", "--db-location", "docker"])
        check("tablas creadas", "Tablas creadas" in out.stdout, out.stdout[-600:])
        secrets_json = json.loads(user_secrets_file(project, "WizDocker").read_text(encoding="utf-8"))
        check("user-secrets con conexión, JWT y admin",
              {"ConnectionStrings:Default", "Jwt:SigningKey", "Seed:AdminPassword"} <= secrets_json.keys())
        env_text = (project / ".env").read_text(encoding="utf-8")
        check(".env usa el mismo admin que user-secrets", f"ADMIN_PASSWORD={secrets_json['Seed:AdminPassword']}" in env_text)
        run(["dotnet", "build", "WizDocker.slnx", "-c", "Release", "-nologo"], project)
        tests = run(["dotnet", "test", "--solution", "WizDocker.slnx", "-c", "Release", "--no-build"], project, check=False)
        summary = " ".join(l.strip() for l in tests.stdout.splitlines() if re.search(r"total:|succeeded:|failed:", l))
        check(f"dotnet test en verde ({summary})", tests.returncode == 0, tests.stdout[-600:])
    finally:
        if project.exists():
            run(["docker", "compose", "down", "-v"], project, check=False)
            cleanup_secrets(project, "WizDocker")


def start_server_pg():
    password = secrets.token_urlsafe(18)
    run(["docker", "rm", "-f", PG_CONTAINER], WORK, check=False)
    run(["docker", "run", "-d", "--name", PG_CONTAINER, "-e", f"POSTGRES_PASSWORD={password}", "-p", "5435:5432", "postgres:17"], WORK)
    for _ in range(30):
        if run(["docker", "exec", PG_CONTAINER, "pg_isready", "-U", "postgres"], WORK, check=False).returncode == 0:
            break
        time.sleep(2)
    time.sleep(2)
    return password


def tables_exist(database):
    out = run(["docker", "exec", PG_CONTAINER, "psql", "-U", "postgres", "-d", database, "-tAc",
               "SELECT count(*) FROM information_schema.tables WHERE table_schema='public'"], WORK, check=False)
    return out.returncode == 0 and out.stdout.strip() not in ("", "0")


def case_server(password):
    print("== server: servidor existente, secretos en los 3 destinos, sin crear tablas")
    project = WORK / "WizServer"
    connection = f"Host=localhost;Port=5435;Database=wizserver_db;Username=postgres;Password={password}"
    try:
        out = wizard(["--name", "WizServer", "--security", "--database", "postgresql", "--email", "log",
                      "--db-location", "server", "--secrets", "user-secrets,env,appsettings"],
                     env=dict(os.environ, KIT_DB_CONNECTION=connection))
        check("conexión probada", "Conectado a localhost:5435" in out.stdout, out.stdout[-600:])
        local = project / "src" / "WizServer.Api" / "appsettings.Development.local.json"
        check("user-secrets escritos", "ConnectionStrings:Default" in user_secrets_file(project, "WizServer").read_text(encoding="utf-8"))
        check("appsettings local escrito", local.exists() and "ConnectionStrings" in local.read_text(encoding="utf-8"))
        check("scripts de variables escritos", (project / "env.local.ps1").exists() and (project / "env.local.sh").exists())
        run(["git", "init", "-q"], project)
        ignored = run(["git", "check-ignore", str(local), "env.local.ps1", "env.local.sh"], project, check=False)
        check("appsettings local y env.local.* ignorados por git", ignored.stdout.count("\n") == 3, ignored.stdout)
        check("tablas NO creadas en el servidor (por defecto)", not tables_exist("wizserver_db"))
    finally:
        cleanup_secrets(project, "WizServer")


def case_appsettings(password):
    print("== appsettings: secretos solo en appsettings local; la API arranca con ellos")
    project = WORK / "WizLocal"
    connection = f"Host=localhost;Port=5435;Database=wizlocal_db;Username=postgres;Password={password}"
    api = None
    try:
        wizard(["--name", "WizLocal", "--security", "--database", "postgresql", "--email", "log",
                "--db-location", "server", "--apply-migrations", "--secrets", "appsettings"],
               env=dict(os.environ, KIT_DB_CONNECTION=connection))
        check("tablas creadas con --apply-migrations", tables_exist("wizlocal_db"))
        check("sin user-secrets", not user_secrets_file(project, "WizLocal").exists())
        log = (WORK / "wizlocal-run.log").open("w", encoding="utf-8")
        env = {k: v for k, v in os.environ.items() if not k.startswith(("ConnectionStrings__", "Jwt__", "Seed__"))}
        env["ASPNETCORE_ENVIRONMENT"] = "Development"
        api = subprocess.Popen(["dotnet", "run", "--project", "src/WizLocal.Api", "--no-launch-profile", "--urls", "http://localhost:5193"],
                               cwd=project, env=env, stdout=log, stderr=subprocess.STDOUT)
        ready = False
        for _ in range(90):
            try:
                ready = urllib.request.urlopen("http://localhost:5193/health/ready", timeout=2).status == 200
                break
            except OSError:
                if api.poll() is not None:
                    break
                time.sleep(1)
        check("la API arranca leyendo appsettings.Development.local.json", ready, "ver .verify/wizard/wizlocal-run.log")
    finally:
        if api:
            api.terminate()
            try:
                api.wait(timeout=20)
            except subprocess.TimeoutExpired:
                api.kill()
        cleanup_secrets(project, "WizLocal")


def case_bad_password():
    print("== bad-password: contraseña incorrecta")
    connection = "Host=localhost;Port=5435;Database=wizbad_db;Username=postgres;Password=incorrecta"
    out = wizard(["--name", "WizBad", "--security", "--database", "postgresql", "--db-location", "server"],
                 env=dict(os.environ, KIT_DB_CONNECTION=connection), check=False)
    check("falla con error de conexión claro", out.returncode != 0 and "No se pudo conectar" in out.stdout, out.stdout[-400:])
    check("no crea el proyecto", not (WORK / "WizBad").exists())


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--quick", action="store_true")
    args = parser.parse_args()
    try:
        pack()
        case_quick()
        if not args.quick:
            case_docker()
            password = start_server_pg()
            try:
                case_server(password)
                case_appsettings(password)
                case_bad_password()
            finally:
                run(["docker", "rm", "-f", PG_CONTAINER], WORK, check=False)
    except RuntimeError as error:
        failures.append(str(error).splitlines()[0])
        print(f"  ERROR: {error}")
    print("\nResultado:", "OK" if not failures else "fallas en " + "; ".join(failures))
    sys.exit(1 if failures else 0)


if __name__ == "__main__":
    main()
