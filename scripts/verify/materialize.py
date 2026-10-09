"""Arma un proyecto .NET con el código C# de standards/ para comprobar que compila (y, opcionalmente, su proyecto de tests).

Uso: python scripts/verify/materialize.py <carpeta_api> <secure|public> <sqlserver|postgresql> [carpeta_tests]

- Toma cada bloque ```csharp de standards/ que tenga una ruta de archivo (`Ruta/Archivo.cs`) en las 3 líneas previas.
  Los bloques con ruta `tests/{Project}.IntegrationTests/...` van a <carpeta_tests> (si se indica).
- Marcas: `// [SEC]` (solo con seguridad), `// [PUB]` (solo sin seguridad), `// [MSSQL]` y `// [PGSQL]` (motor).
  Una marca al final de una línea afecta a esa línea; una marca sola en su línea afecta al miembro o sentencia siguiente.
- Bloques cuyo título (línea `#`) lleva `[SEC]`, o de standards 05*/06, se omiten en el perfil público.
- Las plantillas de standards/07 se compilan con nombres de prueba (VerifyItem / VerifyParent) que solo existen
  en la carpeta temporal de verificación, nunca en un proyecto real.
"""
import pathlib
import re
import sys

KIT = pathlib.Path(__file__).resolve().parents[2] / "standards"
api_dir = pathlib.Path(sys.argv[1])
secure = sys.argv[2] == "secure"
database = sys.argv[3] if len(sys.argv) > 3 else "sqlserver"
tests_dir = pathlib.Path(sys.argv[4]) if len(sys.argv) > 4 else None

REMOVE_MARKS = ["// [PUB]" if secure else "// [SEC]", "// [PGSQL]" if database == "sqlserver" else "// [MSSQL]"]
REPL = {"{Project}": "KitVerify", "{Entities}": "VerifyItems", "{Entity}": "VerifyItem", "{entities}": "verifyitems",
        "{entity}": "verifyitem", "{Parent}": "VerifyParent", "{parent}": "verifyParent"}

# Bloques sin ruta explícita en el texto previo: (archivo, primera línea) -> ruta
MANUAL = {("06-authorization-permissions.md", "using Microsoft.AspNetCore.Authentication.JwtBearer;"): "Api/Authorization/PermissionAuthorization.cs"}
SECURE_ONLY_FILES = {"Api/OpenApi/BearerSecuritySchemeTransformer.cs"}
PUBLIC_ONLY_FILES = {"Infrastructure/Services/SystemCurrentUserService.cs"}
TESTS_PREFIX = "tests/{Project}.IntegrationTests/"


def blocks():
    for md in sorted(KIT.glob("*.md")):
        lines = md.read_text(encoding="utf-8").splitlines()
        i = 0
        while i < len(lines):
            if lines[i].startswith("```csharp"):
                context = " | ".join(l.strip() for l in lines[max(0, i - 3):i] if l.strip())
                found = re.findall(r"`([\w/{}.]+\.cs)`", context)
                j = i + 1
                while j < len(lines) and not lines[j].startswith("```"):
                    j += 1
                first = lines[i + 1].strip() if i + 1 < j else ""
                path = MANUAL.get((md.name, first)) or (found[-1] if found else None)
                if path:
                    headings = [l for l in lines[max(0, i - 3):i] if l.lstrip().startswith("#")]
                    yield md.name, path, any("[SEC]" in h for h in headings), lines[i + 1:j]
                i = j
            i += 1


def strip_marked(lines, mark):
    """Quita líneas con `mark` al final y el miembro/sentencia que sigue a una línea que es solo `mark`."""
    result, i = [], 0
    while i < len(lines):
        stripped = lines[i].strip()
        if stripped.startswith(mark):          # marca sola: afecta a lo siguiente
            i += 1
            depth, started = 0, False
            while i < len(lines):
                line = lines[i]
                depth += line.count("{") - line.count("}")
                started = started or "{" in line
                i += 1
                if started and depth == 0:
                    break
                if not started and line.rstrip().endswith(";"):
                    break
            continue
        if mark in lines[i]:
            i += 1
            continue
        result.append(lines[i])
        i += 1
    return result


def transform(rel, text):
    # Activar las líneas comentadas de "una por entidad" para compilar las plantillas.
    if rel == "DependencyInjection.cs":
        text = text.replace("// services.AddScoped<I{Entity}Service, {Entity}Service>();",
                            "services.AddScoped<I{Entity}Service, {Entity}Service>();")
        text = text.replace("using {Project}.Infrastructure.Persistence.Interceptors;",
                            "using {Project}.Infrastructure.Persistence.Interceptors;\nusing {Project}.Application.Features.{Entities};")
    if rel == "Infrastructure/Persistence/AppDbContext.cs":
        text = text.replace("// public DbSet<{Entity}> {Entities} => Set<{Entity}>();",
                            "public DbSet<{Entity}> {Entities} => Set<{Entity}>();\n    public DbSet<{Parent}> {Parent}s => Set<{Parent}>();")
        text = text.replace("using {Project}.Domain.Common;",
                            "using {Project}.Domain.Common;\nusing {Project}.Domain.Entities;\nusing {Project}.Infrastructure.Persistence.Converters;")
    if rel == "Application/Common/Security/Permissions.cs":
        text = re.sub(r"^    // (public static class \{Entities\}|\{|    public const string \w+ = \"\{entities\}\.\w+\";|\})$",
                      r"    \1", text, flags=re.M)
    if rel == "Api/Controllers/{Entities}Controller.cs" and not secure:
        text = re.sub(r", HasPermission\([^)]*\)", "", text)
        text = text.replace("using {Project}.Api.Authorization;\n", "").replace("using {Project}.Application.Common.Security;\n", "")

    lines = text.splitlines()
    for mark in REMOVE_MARKS:
        lines = strip_marked(lines, mark)
    text = "\n".join(lines)
    for key, value in REPL.items():
        text = text.replace(key, value)
    return text


written = {"api": 0, "tests": 0}
for md_name, rel, sec_heading, lines in blocks():
    is_test = rel.startswith(TESTS_PREFIX)
    if is_test and tests_dir is None:
        continue
    if not is_test and rel.startswith("tests/"):
        continue
    if not secure and (md_name.startswith(("05", "06")) or rel in SECURE_ONLY_FILES or sec_heading):
        continue
    if secure and rel in PUBLIC_ONLY_FILES:
        continue
    text = transform(rel, "\n".join(lines))
    if is_test:
        target = tests_dir / rel[len(TESTS_PREFIX):].replace("{Entities}", "VerifyItems")
    else:
        target = api_dir / rel.replace("{Entities}", "VerifyItems").replace("{Entity}", "VerifyItem")
    target.parent.mkdir(parents=True, exist_ok=True)
    if target.exists():
        raise SystemExit(f"Archivo duplicado: {target}")
    target.write_text(text + "\n", encoding="utf-8")
    written["tests" if is_test else "api"] += 1

# Entidad padre mínima de prueba (en un proyecto real la crea new-entity).
(api_dir / "Domain/Entities/VerifyParent.cs").write_text("""using KitVerify.Domain.Common;

namespace KitVerify.Domain.Entities;

public class VerifyParent : BaseEntity
{
    public string Name { get; set; } = string.Empty;
}
""", encoding="utf-8")
(api_dir / "Infrastructure/Persistence/Configurations/VerifyParentConfiguration.cs").write_text("""using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using KitVerify.Domain.Entities;

namespace KitVerify.Infrastructure.Persistence.Configurations;

public sealed class VerifyParentConfiguration : BaseEntityConfiguration<VerifyParent>
{
    public override void Configure(EntityTypeBuilder<VerifyParent> builder)
    {
        base.Configure(builder);
        builder.Property(p => p.Name).HasMaxLength(200).IsRequired();
    }
}
""", encoding="utf-8")

# Siembra de prueba para las pruebas de humo: un VerifyParent al arrancar (evita depender de sqlcmd/psql).
(api_dir / "Infrastructure/Persistence/VerifyParentSeeder.cs").write_text("""using KitVerify.Domain.Entities;

namespace KitVerify.Infrastructure.Persistence;

public sealed class VerifyParentSeeder(IServiceProvider services, IConfiguration configuration) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!configuration.GetValue("Verify:SeedParent", true))
            return;

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (!db.VerifyParents.Any())
        {
            db.VerifyParents.Add(new VerifyParent { Name = "Parent 1" });
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
""", encoding="utf-8")
program = api_dir / "Program.cs"
program.write_text(program.read_text(encoding="utf-8").replace(
    "var app = builder.Build();",
    "builder.Services.AddHostedService<KitVerify.Infrastructure.Persistence.VerifyParentSeeder>();\n\nvar app = builder.Build();", 1),
    encoding="utf-8")

profile = "secure" if secure else "public"
extra = f" + {written['tests']} de tests" if tests_dir else ""
print(f"{profile} / {database}: {written['api']} archivos desde standards/{extra}")
