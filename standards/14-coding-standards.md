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
├── global.json                    ← fija el SDK de .NET 10
├── src/{Project}.Api/
└── tests/{Project}.UnitTests/, tests/{Project}.IntegrationTests/
```

`global.json`:
```json
{ "sdk": { "version": "10.0.100", "rollForward": "latestFeature" } }
```

## `Directory.Build.props`
```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
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
  </PropertyGroup>
  <ItemGroup>
    <PackageVersion Include="Microsoft.EntityFrameworkCore.SqlServer" Version="10.0.12" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.12" />
    <PackageVersion Include="Microsoft.AspNetCore.OpenApi" Version="10.0.12" />
    <PackageVersion Include="Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore" Version="10.0.12" />
    <PackageVersion Include="FluentValidation.DependencyInjectionExtensions" Version="12.1.1" />
    <PackageVersion Include="Scalar.AspNetCore" Version="2.17.14" />
    <!-- [SEC] -->
    <PackageVersion Include="Microsoft.AspNetCore.Identity.EntityFrameworkCore" Version="10.0.12" />
    <PackageVersion Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="10.0.12" />
  </ItemGroup>
</Project>
```
En los `.csproj`: `<PackageReference Include="..." />` **sin** `Version`. `dotnet add package` actualiza este archivo automáticamente. Versiones de referencia de oct-2026; verificar las vigentes al crear el proyecto.

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
Si un analizador choca con un patrón del kit, se baja su severidad en `.editorconfig` (`dotnet_diagnostic.CAxxxx.severity = suggestion`) con un comentario del porqué. Nunca con `#pragma` disperso.

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
