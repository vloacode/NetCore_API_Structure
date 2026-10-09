"""Verifica que el código de standards/ compile y funcione en la matriz perfil × motor × framework.

Uso:
  python scripts/verify/verify-standards.py
      Compila las 4 combinaciones (secure/public × sqlserver/postgresql) en Release + analizadores + dotnet format.
  python scripts/verify/verify-standards.py --profiles public --databases postgresql
  python scripts/verify/verify-standards.py --framework net11.0
      Prueba los standards con otra versión de .NET (requiere ese SDK instalado). Ver standards/16.
  python scripts/verify/verify-standards.py --smoke-sqlserver "Server=(localdb)\\MSSQLLocalDB;Trusted_Connection=True;TrustServerCertificate=True"
  python scripts/verify/verify-standards.py --smoke-postgresql "Host=localhost;Port=5432;Username=postgres;Password=<tu-clave>"
      Además: migración, arranque contra la BD y pruebas de humo. La BD temporal se crea y se borra sola.
  python scripts/verify/verify-standards.py --run-tests
      Genera el proyecto de tests de standards/12 (xUnit v3 + Testcontainers + Respawn) y lo ejecuta. Requiere Docker.

Requisitos: SDK de .NET del framework elegido y Python 3. Para las pruebas de humo, además: dotnet-ef.
Trabaja en .verify/ (ignorada por git). Código de salida 1 si algo falla.
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

COMMON_PACKAGES = ["Microsoft.EntityFrameworkCore.Design", "Scalar.AspNetCore",
                   "FluentValidation.DependencyInjectionExtensions",
                   "Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore"]
DB_PACKAGES = {"sqlserver": ["Microsoft.EntityFrameworkCore.SqlServer"],
               "postgresql": ["Npgsql.EntityFrameworkCore.PostgreSQL", "EFCore.NamingConventions"]}
SECURE_PACKAGES = ["Microsoft.AspNetCore.Identity.EntityFrameworkCore", "Microsoft.AspNetCore.Authentication.JwtBearer"]
PORTS = {("secure", "sqlserver"): 5199, ("public", "sqlserver"): 5198,
         ("secure", "postgresql"): 5197, ("public", "postgresql"): 5196}


def run(cmd, cwd, check=True, env=None):
    env = dict(env or os.environ, PYTHONIOENCODING="utf-8")
    result = subprocess.run(cmd, cwd=cwd, capture_output=True, text=True, encoding="utf-8", errors="replace", env=env)
    if check and result.returncode != 0:
        tail = "\n".join((result.stdout + result.stderr).splitlines()[-30:])
        raise RuntimeError(f"Falló: {' '.join(cmd)}\n{tail}")
    return result


def write_build_files(root: pathlib.Path, framework: str):
    md = (KIT / "standards" / "14-coding-standards.md").read_text(encoding="utf-8")
    props = re.search(r"## `Directory.Build.props`\n```xml\n(.*?)```", md, re.S).group(1)
    props = re.sub(r"<TargetFramework>[^<]+</TargetFramework>", f"<TargetFramework>{framework}</TargetFramework>", props)
    editorconfig = re.search(r"## `.editorconfig` \(base\)\n```ini\n(.*?)```", md, re.S).group(1)
    extra = re.search(r"Reglas ajustadas por el kit.*?```ini\n(.*?)```", md, re.S).group(1)
    tests_editorconfig = re.search(r"`tests/.editorconfig`.*?```ini\n(.*?)```", md, re.S).group(1)
    (root / "Directory.Build.props").write_text(props, encoding="utf-8")
    (root / ".editorconfig").write_text(editorconfig + "\n" + extra, encoding="utf-8")
    (root / "global.json").write_text('{\n  "test": { "runner": "Microsoft.Testing.Platform" }\n}\n', encoding="utf-8")
    return tests_editorconfig


TEST_PACKAGES = ["xunit.v3", "Microsoft.AspNetCore.Mvc.Testing", "Microsoft.Testing.Extensions.CodeCoverage", "Respawn", "Shouldly"]
TEST_DB_PACKAGES = {"sqlserver": ["Testcontainers.MsSql"], "postgresql": ["Testcontainers.PostgreSql"]}


def create_tests_project(root: pathlib.Path, database: str, framework: str, editorconfig: str) -> pathlib.Path:
    """Proyecto de tests de integración según standards/12 (xUnit v3 + Testcontainers + Respawn)."""
    run(["dotnet", "new", "xunit", "-n", "KitVerify.IntegrationTests", "-f", framework, "-o", "KitVerify.IntegrationTests"], root)
    tests = root / "KitVerify.IntegrationTests"
    (tests / "UnitTest1.cs").unlink(missing_ok=True)
    run(["dotnet", "remove", "package", "xunit"], tests)
    run(["dotnet", "remove", "package", "coverlet.collector"], tests, check=False)
    for package in TEST_PACKAGES + TEST_DB_PACKAGES[database]:
        run(["dotnet", "add", "package", package], tests)
    run(["dotnet", "add", "reference", "../KitVerify/KitVerify.csproj"], tests)
    csproj = tests / "KitVerify.IntegrationTests.csproj"
    csproj.write_text(csproj.read_text(encoding="utf-8").replace(
        "<IsPackable>false</IsPackable>", "<IsPackable>false</IsPackable>\n    <OutputType>Exe</OutputType>", 1), encoding="utf-8")
    (tests / ".editorconfig").write_text(editorconfig, encoding="utf-8")
    return tests


def build(profile: str, database: str, framework: str, with_migration: bool, with_tests: bool = False) -> pathlib.Path:
    root = WORK / f"{profile}-{database}"
    if root.exists():
        shutil.rmtree(root)
    root.mkdir(parents=True)
    tests_editorconfig = write_build_files(root, framework)
    run(["dotnet", "new", "webapi", "-n", "KitVerify", "--use-controllers", "-f", framework, "-o", "KitVerify"], root)
    project = root / "KitVerify"
    for sample in ("Program.cs", "WeatherForecast.cs", "KitVerify.http", "Controllers/WeatherForecastController.cs"):
        (project / sample).unlink(missing_ok=True)
    for package in COMMON_PACKAGES + DB_PACKAGES[database] + (SECURE_PACKAGES if profile == "secure" else []):
        run(["dotnet", "add", "package", package], project)
    tests = create_tests_project(root, database, framework, tests_editorconfig) if with_tests else None
    materialize_args = [sys.executable, str(HERE / "materialize.py"), str(project), profile, database] + ([str(tests)] if tests else [])
    print("  " + run(materialize_args, project).stdout.strip())
    if with_migration:
        run(["dotnet", "ef", "migrations", "add", "InitialCreate", "-o", "Infrastructure/Persistence/Migrations"], project)
        print("  migración InitialCreate generada")
    for folder in [project] + ([tests] if tests else []):
        run(["dotnet", "format"], folder, check=False)
        run(["dotnet", "format", "--verify-no-changes"], folder)
        run(["dotnet", "build", "-c", "Release", "-nologo"], folder)
    print("  dotnet format + build Release (analizadores, warnings como errores): OK" + (" (API + tests)" if tests else ""))
    return project


def run_tests(project: pathlib.Path):
    """Ejecuta los tests de integración con Testcontainers (requiere Docker)."""
    tests = project.parent / "KitVerify.IntegrationTests"
    result = run(["dotnet", "test", "-c", "Release", "--no-build"], tests, check=False)
    summary = [l.strip() for l in result.stdout.splitlines() if re.search(r"total:|succeeded:|failed:|Passed!|Failed!", l)]
    print("  tests de integración (Testcontainers): " + " ".join(summary))
    if result.returncode != 0:
        tail = "\n".join((result.stdout + result.stderr).splitlines()[-30:])
        raise RuntimeError(f"tests de integración con fallas\n{tail}")


def drop_database(database: str, base: str, name: str, project: pathlib.Path):
    if database == "sqlserver" and shutil.which("sqlcmd"):
        server = re.search(r"Server=([^;]+)", base, re.I).group(1)
        run(["sqlcmd", "-S", server, "-Q",
             f"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}];"], project, check=False)
    elif database == "postgresql" and shutil.which("psql"):
        run(["psql", base_to_psql_uri(base, "postgres"), "-c", f'DROP DATABASE IF EXISTS "{name}" WITH (FORCE);'], project, check=False)
    else:
        print(f"  (borra a mano la base temporal '{name}' si ya no la necesitas)")


def base_to_psql_uri(base: str, dbname: str) -> str:
    parts = dict(p.split("=", 1) for p in base.split(";") if "=" in p)
    lower = {k.lower(): v for k, v in parts.items()}
    return (f"postgresql://{lower.get('username', 'postgres')}:{lower.get('password', '')}@"
            f"{lower.get('host', 'localhost')}:{lower.get('port', '5432')}/{dbname}")


def smoke(profile: str, database: str, project: pathlib.Path, base: str):
    name = f"kitverify_{profile}_{int(time.time())}"
    connection = f"{base.rstrip(';')};Database={name}"
    port = PORTS[(profile, database)]
    env = dict(os.environ, ASPNETCORE_ENVIRONMENT="Development", ConnectionStrings__Default=connection,
               Database__ApplyMigrationsOnStartup="true", Cors__AllowedOrigins__0="http://localhost:5173")
    if profile == "secure":
        env.update(Jwt__Issuer="KitVerify", Jwt__Audience="KitVerify.Client", Jwt__SigningKey="verify-" + "k" * 60,
                   Seed__AdminEmail="admin@verify.local", Seed__AdminPassword="Admin#Pass12345",
                   App__ClientUrl="http://localhost:5173", App__AppName="KitVerify")
    run(["dotnet", "build", "-nologo"], project)   # incluye la migración
    log = project.parent / "run.log"
    with log.open("w", encoding="utf-8") as handle:
        api = subprocess.Popen(["dotnet", "run", "--no-build", "--urls", f"http://localhost:{port}"],
                               cwd=project, env=env, stdout=handle, stderr=subprocess.STDOUT)
    try:
        for _ in range(120):
            try:
                urllib.request.urlopen(f"http://localhost:{port}/health/ready", timeout=2)
                break
            except OSError:
                if api.poll() is not None:
                    raise RuntimeError(f"La API terminó al arrancar. Ver {log}")
                time.sleep(1)
        else:
            raise RuntimeError(f"La API no respondió /health/ready. Ver {log}")
        result = run([sys.executable, str(HERE / f"smoke_{profile}.py"), str(port), str(log)], project, check=False)
        print(result.stdout)
        if result.returncode != 0:
            raise RuntimeError("pruebas de humo con fallas")
    finally:
        api.terminate()
        try:
            api.wait(timeout=15)
        except subprocess.TimeoutExpired:
            api.kill()
        drop_database(database, base, name, project)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--profiles", default="secure,public")
    parser.add_argument("--databases", default="sqlserver,postgresql")
    parser.add_argument("--framework", default="net10.0")
    parser.add_argument("--smoke-sqlserver", metavar="CONEXION_SIN_DATABASE")
    parser.add_argument("--smoke-postgresql", metavar="CONEXION_SIN_DATABASE")
    parser.add_argument("--tests", action="store_true", help="genera y compila el proyecto de tests de standards/12")
    parser.add_argument("--run-tests", action="store_true", help="además ejecuta los tests con Testcontainers (requiere Docker)")
    args = parser.parse_args()
    smoke_bases = {"sqlserver": args.smoke_sqlserver, "postgresql": args.smoke_postgresql}

    failures = []
    for database in [d.strip() for d in args.databases.split(",") if d.strip()]:
        for profile in [p.strip() for p in args.profiles.split(",") if p.strip()]:
            label = f"{profile} / {database} / {args.framework}"
            print(f"== {label}")
            try:
                base = smoke_bases.get(database)
                project = build(profile, database, args.framework, with_migration=True,
                                with_tests=args.tests or args.run_tests)
                if args.run_tests:
                    run_tests(project)
                if base:
                    smoke(profile, database, project, base)
            except RuntimeError as error:
                failures.append(label)
                print(f"  ERROR: {error}")
    print("\nResultado:", "OK" if not failures else "fallas en " + "; ".join(failures))
    sys.exit(1 if failures else 0)


if __name__ == "__main__":
    main()
