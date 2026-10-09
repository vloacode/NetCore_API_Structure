"""Verifica las plantillas `dotnet new` del kit (templates/api y templates/entity) en la matriz perfil × motor.

Uso:
  python scripts/verify/verify-template.py
      Genera las combinaciones (secure/public × sqlserver/postgresql, con analytics) y una mínima
      (pública + PostgreSQL + API key, sin analytics). En cada una: kit-entity (con y sin padre),
      migración InitialCreate, dotnet format --verify-no-changes y build Release (analizadores, warnings como errores).
  python scripts/verify/verify-template.py --profiles public --databases postgresql
  python scripts/verify/verify-template.py --framework net11.0
  python scripts/verify/verify-template.py --run-tests
      Además ejecuta los tests de integración generados (Testcontainers; requiere Docker).
  python scripts/verify/verify-template.py --smoke-sqlserver "Server=(localdb)\\MSSQLLocalDB;Trusted_Connection=True;TrustServerCertificate=True"
  python scripts/verify/verify-template.py --smoke-postgresql "Host=localhost;Port=5432;Username=postgres;Password=<clave>"
      Además: arranque contra la BD real y pruebas de humo (smoke_secure.py / smoke_public.py). La BD temporal se borra al final.

Las plantillas se instalan en un "hive" propio dentro de .verify/ (no toca las plantillas instaladas del usuario).
Requisitos: SDK de .NET del framework elegido, Python 3 y dotnet-ef. Código de salida 1 si algo falla.
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
HIVE = WORK / "hive"
APP = "KitVerify"
PORTS = {("secure", "sqlserver"): 5199, ("public", "sqlserver"): 5198,
         ("secure", "postgresql"): 5197, ("public", "postgresql"): 5196}


def run(cmd, cwd, check=True, env=None):
    env = dict(env or os.environ, PYTHONIOENCODING="utf-8", DOTNET_CLI_TELEMETRY_OPTOUT="1")
    result = subprocess.run(cmd, cwd=cwd, capture_output=True, text=True, encoding="utf-8", errors="replace", env=env)
    if check and result.returncode != 0:
        tail = "\n".join((result.stdout + result.stderr).splitlines()[-30:])
        raise RuntimeError(f"Falló: {' '.join(cmd)}\n{tail}")
    return result


def dotnet_new(args, cwd):
    return run(["dotnet", "new", *args, "--debug:custom-hive", str(HIVE)], cwd)


def install_templates():
    if HIVE.exists():
        shutil.rmtree(HIVE)
    for template in ("api", "entity"):
        dotnet_new(["install", str(KIT / "templates" / template)], KIT)


def register_entity(root: pathlib.Path, entity_file: pathlib.Path):
    """Aplica las líneas del comentario REGISTRO, igual que lo haría la IA o el desarrollador."""
    api = root / "src" / f"{APP}.Api"
    text = entity_file.read_text(encoding="utf-8")
    dbset = re.search(r"AppDbContext\.cs\s+->\s+(using [^;]+;)\s+\+\s+(public DbSet<[^;]+;)", text)
    context = api / "Infrastructure/Persistence/AppDbContext.cs"
    source = context.read_text(encoding="utf-8")
    source = source.replace("namespace ", f"{dbset.group(1)}\n\nnamespace ", 1)
    source = re.sub(r"(    // Entidades de negocio:[^\n]*\n)", lambda m: m.group(1) + f"    {dbset.group(2)}\n", source, count=1)
    context.write_text(source, encoding="utf-8")

    permissions = re.search(r"Permissions\.cs\s+->\s+(public static class .+\})\s*$", text, re.M)
    if permissions:
        path = api / "Application/Common/Security/Permissions.cs"
        source = path.read_text(encoding="utf-8")
        source = source.replace("    /// <summary>Todos los permisos", f"    {permissions.group(1)}\n\n    /// <summary>Todos los permisos", 1)
        path.write_text(source, encoding="utf-8")


def add_parent_seeder(api: pathlib.Path):
    """Siembra un VerifyParent al arrancar para las pruebas de humo (solo existe en la carpeta de verificación)."""
    (api / "Features/VerifyParents/VerifyParentSeeder.cs").write_text(f"""using {APP}.Infrastructure.Persistence;

namespace {APP}.Features.VerifyParents;

public sealed class VerifyParentSeeder(IServiceProvider services, IConfiguration configuration) : IHostedService
{{
    public async Task StartAsync(CancellationToken cancellationToken)
    {{
        if (!configuration.GetValue("Verify:SeedParent", false))
            return;

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (!db.VerifyParents.Any())
        {{
            db.VerifyParents.Add(new VerifyParent {{ Name = "Parent 1", Code = "P-1" }});
            await db.SaveChangesAsync(cancellationToken);
        }}
    }}

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}}
""", encoding="utf-8")
    program = api / "Program.cs"
    program.write_text(program.read_text(encoding="utf-8").replace(
        "var app = builder.Build();",
        f"builder.Services.AddHostedService<{APP}.Features.VerifyParents.VerifyParentSeeder>();\n\nvar app = builder.Build();", 1),
        encoding="utf-8")


def build(name, profile, database, analytics, apikey, framework, with_entities=True):
    folder = WORK / name
    if folder.exists():
        shutil.rmtree(folder)
    folder.mkdir(parents=True)
    options = ["--security", str(profile == "secure").lower(), "--database", database,
               "--analytics", str(analytics).lower(), "--apikey", str(apikey).lower(), "--framework", framework]
    dotnet_new(["kitapi", "-n", APP, "-o", APP, *options], folder)
    root = folder / APP
    api = root / "src" / f"{APP}.Api"
    entity_options = ["--app", APP, "--security", str(profile == "secure").lower(), "--database", database,
                      "--apikey", str(apikey).lower()]
    if with_entities:
        dotnet_new(["kit-entity", "-n", "VerifyParent", *entity_options], root)
        dotnet_new(["kit-entity", "-n", "VerifyItem", "--parent", "VerifyParent", *entity_options], root)
        for entity in ("VerifyParents/VerifyParent.cs", "VerifyItems/VerifyItem.cs"):
            register_entity(root, api / "Features" / entity)
        add_parent_seeder(api)
    run(["dotnet", "build", "-nologo"], root)
    run(["dotnet", "ef", "migrations", "add", "InitialCreate", "-o", "Infrastructure/Persistence/Migrations",
         "--project", str(api)], root)
    run(["dotnet", "format", str(root / f"{APP}.slnx")], root, check=False)
    run(["dotnet", "format", str(root / f"{APP}.slnx"), "--verify-no-changes"], root)
    run(["dotnet", "build", "-c", "Release", "-nologo"], root)
    print("  generado + kit-entity + migración + format + build Release (API + tests): OK")
    return root


def run_tests(root: pathlib.Path):
    result = run(["dotnet", "test", "-c", "Release", "--no-build"], root, check=False)
    summary = [l.strip() for l in result.stdout.splitlines() if re.search(r"total:|succeeded:|failed:|Passed!|Failed!", l)]
    print("  tests de integración (Testcontainers): " + " ".join(summary))
    if result.returncode != 0:
        tail = "\n".join((result.stdout + result.stderr).splitlines()[-30:])
        raise RuntimeError(f"tests de integración con fallas\n{tail}")


def base_to_psql_uri(base: str, dbname: str) -> str:
    parts = {k.lower(): v for k, v in (p.split("=", 1) for p in base.split(";") if "=" in p)}
    return (f"postgresql://{parts.get('username', 'postgres')}:{parts.get('password', '')}@"
            f"{parts.get('host', 'localhost')}:{parts.get('port', '5432')}/{dbname}")


def drop_database(database: str, base: str, name: str, cwd: pathlib.Path):
    if database == "sqlserver" and shutil.which("sqlcmd"):
        server = re.search(r"Server=([^;]+)", base, re.I).group(1)
        run(["sqlcmd", "-S", server, "-Q",
             f"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}];"], cwd, check=False)
    elif database == "postgresql" and shutil.which("psql"):
        run(["psql", base_to_psql_uri(base, "postgres"), "-c", f'DROP DATABASE IF EXISTS "{name}" WITH (FORCE);'], cwd, check=False)
    else:
        print(f"  (borra a mano la base temporal '{name}' si ya no la necesitas)")


def smoke(profile: str, database: str, root: pathlib.Path, base: str):
    api = root / "src" / f"{APP}.Api"
    name = f"kitverify_{profile}_{int(time.time())}"
    port = PORTS[(profile, database)]
    env = dict(os.environ, ASPNETCORE_ENVIRONMENT="Development", ConnectionStrings__Default=f"{base.rstrip(';')};Database={name}",
               Database__ApplyMigrationsOnStartup="true", Cors__AllowedOrigins__0="http://localhost:5173",
               Analytics__HashKey="verify-" + "h" * 60, Verify__SeedParent="true")
    if profile == "secure":
        env.update(Jwt__Issuer="KitVerify", Jwt__Audience="KitVerify.Client", Jwt__SigningKey="verify-" + "k" * 60,
                   Seed__AdminEmail="admin@verify.local", Seed__AdminPassword="Admin#Pass12345",
                   App__ClientUrl="http://localhost:5173", App__AppName="KitVerify")
    log = root / "run.log"
    with log.open("w", encoding="utf-8") as handle:
        process = subprocess.Popen(["dotnet", "run", "--no-build", "-c", "Release", "--no-launch-profile",
                                    "--urls", f"http://localhost:{port}"], cwd=api, env=env, stdout=handle, stderr=subprocess.STDOUT)
    try:
        for _ in range(120):
            try:
                urllib.request.urlopen(f"http://localhost:{port}/health/ready", timeout=2)
                break
            except OSError:
                if process.poll() is not None:
                    raise RuntimeError(f"La API terminó al arrancar. Ver {log}")
                time.sleep(1)
        else:
            raise RuntimeError(f"La API no respondió /health/ready. Ver {log}")
        result = run([sys.executable, str(HERE / f"smoke_{profile}.py"), str(port), str(log)], root, check=False)
        print(result.stdout)
        if result.returncode != 0:
            raise RuntimeError("pruebas de humo con fallas")
    finally:
        process.terminate()
        try:
            process.wait(timeout=15)
        except subprocess.TimeoutExpired:
            process.kill()
        drop_database(database, base, name, root)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--profiles", default="secure,public")
    parser.add_argument("--databases", default="sqlserver,postgresql")
    parser.add_argument("--framework", default="net10.0")
    parser.add_argument("--no-minimal", action="store_true", help="omite la combinación mínima (pública + API key, sin analytics)")
    parser.add_argument("--smoke-sqlserver", metavar="CONEXION_SIN_DATABASE")
    parser.add_argument("--smoke-postgresql", metavar="CONEXION_SIN_DATABASE")
    parser.add_argument("--run-tests", action="store_true", help="ejecuta los tests con Testcontainers (requiere Docker)")
    args = parser.parse_args()
    smoke_bases = {"sqlserver": args.smoke_sqlserver, "postgresql": args.smoke_postgresql}

    WORK.mkdir(exist_ok=True)
    install_templates()
    databases = [d.strip() for d in args.databases.split(",") if d.strip()]
    combos = [(f"{p}-{d}", p, d, True, False) for d in databases for p in [x.strip() for x in args.profiles.split(",") if x.strip()]]
    if not args.no_minimal:
        combos.append(("minimal-public-apikey", "public", databases[-1], False, True))

    failures = []
    for name, profile, database, analytics, apikey in combos:
        label = f"{name} / {args.framework}"
        print(f"== {label}")
        try:
            root = build(name, profile, database, analytics, apikey, args.framework)
            if args.run_tests:
                run_tests(root)
            base = smoke_bases.get(database)
            if base and not name.startswith("minimal"):
                smoke(profile, database, root, base)
        except RuntimeError as error:
            failures.append(label)
            print(f"  ERROR: {error}")
    print("\nResultado:", "OK" if not failures else "fallas en " + "; ".join(failures))
    sys.exit(1 if failures else 0)


if __name__ == "__main__":
    main()
