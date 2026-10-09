# Estándares de código

> **Aplica a:** Todos los perfiles  
> **Propósito:** Estructura de la solución, configuración común de proyectos, estilo C#, analizadores y convenciones de Git.  
> Índice general: `standards/00-INDEX.md`

## Estructura de la solución
```
{Project}/
├── {Project}.slnx                 ← formato de solución XML (default de `dotnet new sln` en .NET 10)
├── Directory.Build.props          ← propiedades comunes a todos los proyectos
├── Directory.Packages.props       ← versiones de NuGet centralizadas
├── .editorconfig                  ← estilo y severidad de analizadores
├── global.json                    ← fija el SDK y activa Microsoft.Testing.Platform para dotnet test
├── src/{Project}.Api/
└── tests/{Project}.UnitTests/, tests/{Project}.IntegrationTests/
```

`global.json`:
```json
{
  "sdk": { "version": "10.0.100", "rollForward": "latestFeature" },
  "test": { "runner": "Microsoft.Testing.Platform" }
}
```
`test.runner` es obligatorio con xUnit v3 en el SDK de .NET 10: sin él, `dotnet test` falla ("Testing with VSTest target is no longer supported").

## `Directory.Build.props`
```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>   <!-- TargetFramework del perfil (standards/16) -->
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <LangVersion>latest</LangVersion>
    <AnalysisLevel>latest-recommended</AnalysisLevel>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
    <!-- Warnings como errores en Release (CI); en Debug no bloquean el desarrollo. -->
    <TreatWarningsAsErrors Condition="'$(Configuration)' == 'Release'">true</TreatWarningsAsErrors>
  </PropertyGroup>
</Project>
```
Con esto, los `.csproj` quedan mínimos: sin `TargetFramework` ni `Nullable` repetidos.

## `Directory.Packages.props` (gestión central de paquetes)
```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
    <!-- Una sola propiedad por familia: al cambiar de versión de .NET se edita aquí (standards/16). -->
    <MicrosoftVersion>10.0.12</MicrosoftVersion>
    <NpgsqlEfVersion>10.0.3</NpgsqlEfVersion>
  </PropertyGroup>
  <ItemGroup>
    <PackageVersion Include="Microsoft.EntityFrameworkCore.Design" Version="$(MicrosoftVersion)" />
    <PackageVersion Include="Microsoft.AspNetCore.OpenApi" Version="$(MicrosoftVersion)" />
    <PackageVersion Include="Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore" Version="$(MicrosoftVersion)" />
    <PackageVersion Include="FluentValidation.DependencyInjectionExtensions" Version="12.1.1" />
    <PackageVersion Include="Scalar.AspNetCore" Version="2.17.14" />
    <!-- [MSSQL] -->
    <PackageVersion Include="Microsoft.EntityFrameworkCore.SqlServer" Version="$(MicrosoftVersion)" />
    <!-- [PGSQL] -->
    <PackageVersion Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="$(NpgsqlEfVersion)" />
    <PackageVersion Include="EFCore.NamingConventions" Version="10.0.1" />
    <!-- [SEC] -->
    <PackageVersion Include="Microsoft.AspNetCore.Identity.EntityFrameworkCore" Version="$(MicrosoftVersion)" />
    <PackageVersion Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="$(MicrosoftVersion)" />
  </ItemGroup>
</Project>
```
En los `.csproj`: `<PackageReference Include="..." />` **sin** `Version`. `dotnet add package` actualiza este archivo automáticamente. Versiones de referencia de oct-2026 para .NET 10; verificar las vigentes al crear el proyecto. Los paquetes `Microsoft.*` y los proveedores de EF Core siguen la **versión mayor del framework** (`standards/16`).

## `.editorconfig` (base)
```ini
root = true

[*]
charset = utf-8
insert_final_newline = true
trim_trailing_whitespace = true
indent_style = space
indent_size = 4

[*.{json,yml,yaml,xml,csproj,props,slnx}]
indent_size = 2

[*.cs]
csharp_style_namespace_declarations = file_scoped:warning
csharp_style_var_for_built_in_types = true:suggestion
csharp_style_var_when_type_is_apparent = true:suggestion
csharp_prefer_primary_constructors = true:suggestion
csharp_style_expression_bodied_methods = when_on_single_line:suggestion
csharp_using_directive_placement = outside_namespace:warning
dotnet_sort_system_directives_first = true
dotnet_style_qualification_for_field = false:warning
dotnet_style_readonly_field = true:warning

# Nombres: constantes en PascalCase (va antes que la regla de campos privados)
dotnet_naming_rule.constants_pascal.symbols = constant_fields
dotnet_naming_rule.constants_pascal.style = pascal_case
dotnet_naming_rule.constants_pascal.severity = warning
dotnet_naming_symbols.constant_fields.applicable_kinds = field
dotnet_naming_symbols.constant_fields.required_modifiers = const
dotnet_naming_style.pascal_case.capitalization = pascal_case

# Campos privados: _camelCase
dotnet_naming_rule.private_fields_underscore.symbols = private_fields
dotnet_naming_rule.private_fields_underscore.style = underscore_camel
dotnet_naming_rule.private_fields_underscore.severity = warning
dotnet_naming_symbols.private_fields.applicable_kinds = field
dotnet_naming_symbols.private_fields.applicable_accessibilities = private
dotnet_naming_style.underscore_camel.capitalization = camel_case
dotnet_naming_style.underscore_camel.required_prefix = _
```
Reglas ajustadas por el kit (agregar al final del `.editorconfig`; verificadas con `AnalysisLevel=latest-recommended` en Release):
```ini
# Migraciones generadas por EF Core: fuera del análisis
[**/Migrations/*.cs]
generated_code = true
dotnet_analyzer_diagnostic.severity = none

[*.cs]
# LoggerMessage obligatorio solo en rutas calientes (standards/10); en el resto, ILogger normal
dotnet_diagnostic.CA1848.severity = suggestion
dotnet_diagnostic.CA1873.severity = suggestion
# La API no se consume desde VB: 'Error' y parámetros como 'to' son nombres válidos
dotnet_diagnostic.CA1716.severity = none
# Convención del kit: 'ct' para CancellationToken; DbContext e IdentityDbContext nombran distinto el parámetro de OnModelCreating
dotnet_diagnostic.CA1725.severity = suggestion
```

`tests/.editorconfig` (hereda del principal; solo para los proyectos de tests):
```ini
# Nombres de test Metodo_Escenario_Resultado y fixtures de xUnit con sufijo Collection
[*.cs]
dotnet_diagnostic.CA1707.severity = none
dotnet_diagnostic.CA1711.severity = none
```

Si otro analizador choca con un patrón del kit, se baja su severidad en `.editorconfig` (`dotnet_diagnostic.CAxxxx.severity = suggestion`) con un comentario del porqué. Nunca con `#pragma` disperso.

## Estilo C#
| Regla | Ejemplo |
|---|---|
| `namespace` de archivo | `namespace {Project}.Application.Features.{Entities};` |
| **Primary constructors** para inyección de dependencias | `public sealed class {Entity}Service(IUnitOfWork uow) : I{Entity}Service` |
| Clases `sealed` por defecto; solo se abren si hay herencia real | `public sealed class ...` |
| `record` para DTOs, requests y valores inmutables | `public sealed record {Entity}Dto(...)` |
| Sufijo `Async` en métodos asíncronos; `CancellationToken ct` como último parámetro | `Task<Result> DeleteAsync(int id, CancellationToken ct)` |
| `var` cuando el tipo es evidente | `var entity = request.ToEntity();` |
| Cláusulas de guarda y retorno temprano; sin anidar `if` | `if (entity is null) return {Entity}Errors.NotFound(id);` |
| Nada de strings mágicos repetidos: constantes o `nameof` | `Permissions.{Entities}.Read`, `nameof(GetById)` |
| El operador `!` solo cuando el valor está garantizado, con comentario si no es obvio | `user.Email!` |
| Sin `#region`, sin código comentado, sin `TODO` sin ticket | — |

**Organización por funcionalidad:** cada entidad tiene su carpeta `Application/Features/{Entities}/`, con un archivo por tipo de pieza (`Contracts`, `Specifications`, `Validators`, `Service`). Se aceptan varios tipos pequeños relacionados en un mismo archivo (por ejemplo, los DTOs y errores en `{Entity}Contracts.cs`).

**Idioma:** identificadores en inglés; comentarios, mensajes al usuario y documentación en español. Los comentarios explican el **porqué**, no el qué.

## Formato y análisis
**Después de generar o copiar código desde las plantillas, ejecutar siempre `dotnet format`.** El orden de los `using` depende del nombre real del proyecto (`{Project}` puede quedar antes o después de `Microsoft.*`), así que las plantillas no pueden traerlo ya ordenado. Sin este paso, `dotnet format --verify-no-changes` falla en la CI.

```bash
dotnet format                         # aplica estilo y fixes automáticos
dotnet format --verify-no-changes     # en CI: falla si hay algo sin formatear
dotnet build -c Release               # analizadores y warnings como errores
```

## Git
- **Conventional Commits**: `feat:`, `fix:`, `refactor:`, `test:`, `docs:`, `chore:`, `perf:`, `ci:`. Ejemplo: `feat({entities}): agregar búsqueda por código`.
- Ramas: `feature/<modulo>-<descripcion>`, `fix/<descripcion>`, `hotfix/<descripcion>`.
- Pull requests pequeños, con la **Definition of Done** de `AGENTS.md` como checklist.
- Nunca subir: `bin/`, `obj/`, `.vs/`, `*.user`, `.env`, `appsettings.*.local.json`, `secrets.json`.
