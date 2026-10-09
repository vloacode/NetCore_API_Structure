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
└── tests/{Project}.IntegrationTests/
```
Todo lo anterior lo genera `dotnet new kitapi`.

`global.json`:
→ Código: [`templates/api/global.json`](../templates/api/global.json)
`test.runner` es obligatorio con xUnit v3 en el SDK de .NET 10: sin él, `dotnet test` falla ("Testing with VSTest target is no longer supported").

## `Directory.Build.props`
→ Código: [`templates/api/Directory.Build.props`](../templates/api/Directory.Build.props)
Con esto, los `.csproj` quedan mínimos: sin `TargetFramework` ni `Nullable` repetidos.

## `Directory.Packages.props` (gestión central de paquetes)
→ Código: [`templates/api/Directory.Packages.props`](../templates/api/Directory.Packages.props)
En los `.csproj`: `<PackageReference Include="..." />` **sin** `Version`. Un paquete nuevo: `<PackageVersion>` aquí y `<PackageReference>` sin versión en el `.csproj` (o `dotnet add package`, que hace ambas cosas). Las versiones de la plantilla son de oct-2026 para .NET 10. Los paquetes `Microsoft.*` y los proveedores de EF Core siguen la **versión mayor del framework** (`standards/16`).

## `.editorconfig` (base)
→ Código: [`templates/api/.editorconfig`](../templates/api/.editorconfig)
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
→ Código: [`templates/api/tests/.editorconfig`](../templates/api/tests/.editorconfig)

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

**Organización por funcionalidad:** cada entidad tiene su carpeta `Features/{Entities}/` con **4 archivos**: `{Entity}.cs` (entidad + configuración EF), `{Entity}Contracts.cs` (DTOs, requests, filtro, errores, mapeo, validadores), `{Entity}Service.cs` (interfaz, service y specs) y `{Entities}Controller.cs`. Varios tipos pequeños relacionados van en el mismo archivo a propósito: menos archivos que abrir, todo lo de la entidad junto.

**Idioma:** identificadores en inglés; comentarios, mensajes al usuario y documentación en español. Los comentarios explican el **porqué**, no el qué.

## Formato y análisis
**Después de generar código con las plantillas (`kitapi`, `kit-entity`), ejecutar siempre `dotnet format`.** El orden de los `using` depende del nombre real del proyecto (`{Project}` puede quedar antes o después de `Microsoft.*`), así que las plantillas no pueden traerlo ya ordenado. Sin este paso, `dotnet format --verify-no-changes` falla en la CI.

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
