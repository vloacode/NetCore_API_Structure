"""Arma un proyecto .NET con el código C# de standards/ para comprobar que compila.

Uso: python scripts/verify/materialize.py <carpeta_proyecto> <secure|public>

- Toma cada bloque ```csharp de standards/ que tenga una ruta de archivo (`Ruta/Archivo.cs`) en las 3 líneas previas.
- Perfil `public`: omite standards 05* y 06, quita líneas/bloques `// [SEC]` y conserva `// [PUB]`.
- Perfil `secure`: quita las líneas `// [PUB]`.
- Reemplaza los marcadores con nombres de prueba (VerifyItem / VerifyParent) para compilar las plantillas de standards/07.
  Estos nombres existen solo en la carpeta temporal de verificación, nunca en un proyecto real.
"""
import pathlib
import re
import sys

KIT = pathlib.Path(__file__).resolve().parents[2] / "standards"
out = pathlib.Path(sys.argv[1])
secure = sys.argv[2] == "secure"

REPL = {"{Project}": "KitVerify", "{Entities}": "VerifyItems", "{Entity}": "VerifyItem", "{entities}": "verifyitems",
        "{entity}": "verifyitem", "{Parent}": "VerifyParent", "{parent}": "verifyParent"}

# Bloques sin ruta explícita en el texto previo.
MANUAL = {("06-authorization-permissions.md", "using Microsoft.AspNetCore.Authentication.JwtBearer;"): "Api/Authorization/PermissionAuthorization.cs"}

SECURE_ONLY_PREFIXES = ("05", "06")
SECURE_ONLY_FILES = {"Api/OpenApi/BearerSecuritySchemeTransformer.cs",
                     "Infrastructure/Persistence/Configurations/IdentityConfigurations.cs"}
PUBLIC_ONLY_FILES = {"Infrastructure/Services/SystemCurrentUserService.cs"}
OPTIONAL_FILES = {"Api/Authentication/ApiKeyAuthentication.cs"}   # se compila en ambos perfiles (no se registra)


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
                if path and not path.startswith("tests/"):
                    yield md.name, path, lines[i + 1:j]
                i = j
            i += 1


def strip_sec(lines):
    """Quita líneas y bloques marcados [SEC]: comentario `// [SEC]` sobre un método o sentencia, o marca al final de línea."""
    result, i = [], 0
    while i < len(lines):
        stripped = lines[i].strip()
        if stripped.startswith("// [SEC]"):
            nxt = lines[i + 1].strip() if i + 1 < len(lines) else ""
            if nxt.startswith("public static"):
                depth, started, i = 0, False, i + 1
                while i < len(lines):
                    depth += lines[i].count("{") - lines[i].count("}")
                    started = started or "{" in lines[i]
                    i += 1
                    if started and depth == 0:
                        break
                continue
            i += 1
            while i < len(lines) and not lines[i - 1].rstrip().endswith(";"):
                i += 1
            continue
        if "[SEC]" in lines[i] and "//" in lines[i]:
            i += 1
            continue
        result.append(lines[i])
        i += 1
    return result


written = []
for md_name, rel, lines in blocks():
    if not secure and (md_name.startswith(SECURE_ONLY_PREFIXES) or rel in SECURE_ONLY_FILES):
        continue
    if secure and rel in PUBLIC_ONLY_FILES:
        continue
    text = "\n".join(lines)

    # Activar las líneas comentadas de "una por entidad" para compilar la plantilla.
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

    if secure:
        text = "\n".join(l for l in text.splitlines() if "// [PUB]" not in l)
    else:
        text = "\n".join(strip_sec(text.splitlines()))

    for key, value in REPL.items():
        text = text.replace(key, value)
    target = out / rel.replace("{Entities}", "VerifyItems").replace("{Entity}", "VerifyItem")
    target.parent.mkdir(parents=True, exist_ok=True)
    if target.exists():
        raise SystemExit(f"Archivo duplicado: {target}")
    target.write_text(text + "\n", encoding="utf-8")
    written.append(rel)

# Entidad padre mínima de prueba (en un proyecto real la crea new-entity).
(out / "Domain/Entities/VerifyParent.cs").write_text("""using KitVerify.Domain.Common;

namespace KitVerify.Domain.Entities;

public class VerifyParent : BaseEntity
{
    public string Name { get; set; } = string.Empty;
}
""", encoding="utf-8")
(out / "Infrastructure/Persistence/Configurations/VerifyParentConfiguration.cs").write_text("""using Microsoft.EntityFrameworkCore;
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

print(f"{'secure' if secure else 'public'}: {len(written)} archivos desde standards/")
