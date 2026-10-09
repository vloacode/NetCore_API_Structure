"""Verifica que el código de standards/ compile y funcione, en los perfiles con y sin seguridad.

Uso:
  python scripts/verify/verify-standards.py                       # compila ambos perfiles (Release + analizadores + format)
  python scripts/verify/verify-standards.py --profiles public     # solo un perfil
  python scripts/verify/verify-standards.py --smoke "Server=(localdb)\\MSSQLLocalDB;Trusted_Connection=True;TrustServerCertificate=True"
                                                                  # además: migración, arranque y pruebas de humo contra SQL Server

Requisitos: .NET 10 SDK. Para --smoke: dotnet-ef y sqlcmd en el PATH, y un SQL Server accesible (LocalDB sirve).
Trabaja en una carpeta temporal (.verify/, ignorada por git). Código de salida 1 si algo falla.
"""
import argparse
import os
import pathlib
import re
import shutil
import subprocess
import sys
import time
import urllib.request

for _stream in (sys.stdout, sys.stderr):
    if hasattr(_stream, "reconfigure"):
        _stream.reconfigure(encoding="utf-8")

HERE = pathlib.Path(__file__).resolve().parent
KIT = HERE.parents[1]
WORK = KIT / ".verify"
COMMON_PACKAGES = ["Microsoft.EntityFrameworkCore.SqlServer", "Microsoft.EntityFrameworkCore.Design", "Scalar.AspNetCore",
                   "FluentValidation.DependencyInjectionExtensions", "Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore"]
SECURE_PACKAGES = ["Microsoft.AspNetCore.Identity.EntityFrameworkCore", "Microsoft.AspNetCore.Authentication.JwtBearer"]
PORTS = {"secure": 5199, "public": 5198}


def run(cmd, cwd, check=True, env=None, quiet=True):
    env = dict(env or os.environ, PYTHONIOENCODING="utf-8")
    result = subprocess.run(cmd, cwd=cwd, capture_output=True, text=True, encoding="utf-8", errors="replace",
                            env=env, shell=False)
    if check and result.returncode != 0:
        tail = "\n".join((result.stdout + result.stderr).splitlines()[-30:])
        raise RuntimeError(f"Falló: {' '.join(cmd)}\n{tail}")
    if not quiet:
        print(result.stdout)
    return result


def write_build_files(root: pathlib.Path):
    md = (KIT / "standards" / "14-coding-standards.md").read_text(encoding="utf-8")
    props = re.search(r"## `Directory.Build.props`\n```xml\n(.*?)```", md, re.S).group(1)
    editorconfig = re.search(r"## `.editorconfig` \(base\)\n```ini\n(.*?)```", md, re.S).group(1)
    extra = re.search(r"Reglas ajustadas por el kit.*?```ini\n(.*?)```", md, re.S).group(1)
    (root / "Directory.Build.props").write_text(props, encoding="utf-8")
    (root / ".editorconfig").write_text(editorconfig + "\n" + extra, encoding="utf-8")


def build_profile(profile: str, smoke: bool) -> pathlib.Path:
    root = WORK / profile
    if root.exists():
        shutil.rmtree(root)
    root.mkdir(parents=True)
    write_build_files(root)
    run(["dotnet", "new", "webapi", "-n", "KitVerify", "--use-controllers", "-f", "net10.0", "-o", "KitVerify"], root)
    project = root / "KitVerify"
    for sample in ("Program.cs", "WeatherForecast.cs", "KitVerify.http", "Controllers/WeatherForecastController.cs"):
        (project / sample).unlink(missing_ok=True)
    for package in COMMON_PACKAGES + (SECURE_PACKAGES if profile == "secure" else []):
        run(["dotnet", "add", "package", package], project)
    print(run([sys.executable, str(HERE / "materialize.py"), str(project), profile], project).stdout.strip())
    if smoke:
        run(["dotnet", "ef", "migrations", "add", "InitialCreate", "-o", "Infrastructure/Persistence/Migrations"], project)
    run(["dotnet", "format"], project, check=False)
    run(["dotnet", "format", "--verify-no-changes"], project)
    print(f"  {profile}: dotnet format OK")
    run(["dotnet", "build", "-c", "Release", "-nologo"], project)
    print(f"  {profile}: build Release (analizadores, warnings como errores) OK")
    return project


def smoke_profile(profile: str, project: pathlib.Path, base_connection: str):
    database = f"KitVerify_{profile}_{int(time.time())}"
    connection = f"{base_connection.rstrip(';')};Database={database}"
    port = PORTS[profile]
    env = dict(os.environ, ASPNETCORE_ENVIRONMENT="Development", ConnectionStrings__Default=connection,
               Database__ApplyMigrationsOnStartup="true", Cors__AllowedOrigins__0="http://localhost:5173")
    if profile == "secure":
        env.update(Jwt__Issuer="KitVerify", Jwt__Audience="KitVerify.Client", Jwt__SigningKey="verify-" + "k" * 60,
                   Seed__AdminEmail="admin@verify.local", Seed__AdminPassword="Admin#Pass12345",
                   App__ClientUrl="http://localhost:5173", App__AppName="KitVerify")
    run(["dotnet", "build", "-nologo"], project)   # incluye la migración
    log = project.parent / "run.log"
    with log.open("w", encoding="utf-8") as handle:
        api = subprocess.Popen(["dotnet", "run", "--no-build", "--urls", f"http://localhost:{port}"], cwd=project,
                               env=env, stdout=handle, stderr=subprocess.STDOUT)
    try:
        for _ in range(90):
            try:
                urllib.request.urlopen(f"http://localhost:{port}/health/live", timeout=2)
                break
            except OSError:
                time.sleep(1)
        else:
            raise RuntimeError(f"La API ({profile}) no arrancó. Ver {log}")
        server = re.search(r"Server=([^;]+)", base_connection).group(1)
        run(["sqlcmd", "-S", server, "-d", database, "-Q",
             "SET NOCOUNT ON; INSERT INTO VerifyParents (Name, IsDeleted) VALUES ('Parent 1', 0);"], project)
        result = run([sys.executable, str(HERE / f"smoke_{profile}.py"), str(port), str(log)], project, check=False)
        print(result.stdout)
        if result.returncode != 0:
            raise RuntimeError(f"Pruebas de humo ({profile}) con fallas")
    finally:
        api.terminate()
        try:
            api.wait(timeout=15)
        except subprocess.TimeoutExpired:
            api.kill()
        server = re.search(r"Server=([^;]+)", base_connection).group(1)
        run(["sqlcmd", "-S", server, "-Q",
             f"ALTER DATABASE [{database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{database}];"], project, check=False)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--profiles", default="secure,public")
    parser.add_argument("--smoke", metavar="CONNECTION_STRING_SIN_DATABASE")
    args = parser.parse_args()

    failures = []
    for profile in [p.strip() for p in args.profiles.split(",") if p.strip()]:
        print(f"== Perfil {profile}")
        try:
            project = build_profile(profile, smoke=bool(args.smoke))
            if args.smoke:
                smoke_profile(profile, project, args.smoke)
        except RuntimeError as error:
            failures.append(profile)
            print(f"  ERROR: {error}")
    print("\nResultado:", "OK" if not failures else f"fallas en {', '.join(failures)}")
    sys.exit(1 if failures else 0)


if __name__ == "__main__":
    main()
