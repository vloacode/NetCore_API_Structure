# Versiones de .NET

> **Aplica a:** Todos los perfiles  
> **Propósito:** Qué versión de .NET usar, qué partes del kit dependen de la versión y cómo adoptar versiones nuevas (en el kit y en los proyectos).  
> Índice general: `standards/00-INDEX.md`

## Política
1. **Por defecto, la versión LTS vigente** (hoy **.NET 10**). Queda en el perfil: `TargetFramework = net10.0`.
2. Una versión **STS** solo con un ADR, sabiendo que tiene menos tiempo de soporte y que habrá que migrar antes.
3. **Nunca** iniciar un proyecto con una versión fuera de soporte o en preview.
4. Al salir una LTS nueva, el kit se actualiza a ella (versión MAJOR o MINOR del kit según el impacto), y los proyectos migran con `ai/workflows/upgrade-dotnet.md` antes de que termine el soporte de su versión.

Calendario de .NET: una versión mayor cada noviembre; las pares son **LTS** (3 años de soporte) y las impares **STS** (soporte más corto). Fechas exactas en la política oficial: https://dotnet.microsoft.com/platform/support/policy/dotnet-core

## Cómo saber qué versiones hay
```bash
dotnet --list-sdks                       # SDKs instalados
dotnet --info                            # runtime, SDK y sistema
dotnet package search Microsoft.EntityFrameworkCore --exact-match   # versiones publicadas de un paquete
```
- Índice oficial de versiones (JSON, apto para automatizar): https://dotnetcli.blob.core.windows.net/dotnet/release-metadata/releases-index.json
- Notas de cada versión: https://github.com/dotnet/core/tree/main/release-notes
- Con acceso web, la IA debe confirmar aquí la versión vigente antes de proponerla (ver `ai/references.md`).

## Dónde aparece la versión en un proyecto

| Archivo | Valor | Ejemplo (.NET 10) |
|---|---|---|
| `docs/00-MASTER_CONTEXT.md` (perfil) | `TargetFramework` | `net10.0` |
| `Directory.Build.props` | `<TargetFramework>` | `net10.0` |
| `global.json` | `sdk.version` | `10.0.100` (+ `rollForward: latestMajor`) |
| `Directory.Packages.props` | `MicrosoftVersion` y las versiones de Npgsql/EFCore.NamingConventions | `10.0.12`, `10.0.3` |
| `Dockerfile` | `ARG DOTNET_VERSION` | `10.0` |
| CI (`.github/workflows/*.yml`) | `dotnet-version` | `10.0.x` |
| `dotnet new kitapi --framework` | framework (cambia `Directory.Build.props` y `Dockerfile`) | `net10.0` |

**Regla de paquetes:** los `Microsoft.*` (ASP.NET Core, EF Core, Extensions) y los proveedores de EF Core (Npgsql, EFCore.NamingConventions) usan la **misma versión mayor que el framework**. Las librerías de terceros (FluentValidation, Scalar, Testcontainers) usan su última versión estable compatible.

## Qué partes del kit dependen de la versión
El código de las plantillas (`templates/`) está escrito y verificado para **.NET 10**. Estas piezas requieren una versión mínima; al usar otra versión, revisarlas primero:

| Pieza | Mínimo | Si la versión es distinta |
|---|---|---|
| Primary constructors, collection expressions (`[]`) | C# 12 / .NET 8 | — |
| `TimeProvider`, `IExceptionHandler`, `GeneratedRegex` | .NET 8 | — |
| `Guid.CreateVersion7()` | .NET 9 | En .NET 8: `Guid.NewGuid()` |
| `AddOpenApi()` / `MapOpenApi()` integrados | .NET 9 | En .NET 8: Swashbuckle |
| `HybridCache` (`Microsoft.Extensions.Caching.Hybrid`) | .NET 9 | — |
| `ExecuteUpdateAsync(Action<UpdateSettersBuilder<T>>)` | EF Core 10 | En EF Core 8/9 la firma usa `Expression<Func<SetPropertyCalls<T>, SetPropertyCalls<T>>>` |
| Microsoft.OpenApi 2.x (`IOpenApiSecurityScheme`, `OpenApiSecuritySchemeReference`) en `BearerSecuritySchemeTransformer` | .NET 10 | En .NET 9: Microsoft.OpenApi 1.x (`Microsoft.OpenApi.Models`) |
| Solución `.slnx` por defecto | SDK .NET 10 | Con SDK anteriores, `.sln` |

Versiones **futuras** (.NET 11, 12…): el código debería funcionar igual, salvo lo que indiquen sus listas de *breaking changes*. Por eso la adopción siempre pasa por la verificación (abajo) y nunca se asume.

## Adoptar una versión nueva en el kit (mantenedores)
1. Leer **What's new** y **Breaking changes** de .NET, ASP.NET Core, EF Core y C# para la versión nueva (enlaces en `ai/references.md`).
2. Instalar el SDK nuevo y correr:
   ```bash
   python scripts/verify/verify-template.py --framework net11.0
   python scripts/verify/verify-template.py --framework net11.0 --smoke-sqlserver "<conexión>" --smoke-postgresql "<conexión>"
   ```
3. Corregir las plantillas hasta que la matriz completa pase. Si una API cambió, actualizar el código **y** la tabla "Qué partes dependen de la versión".
4. Si la versión nueva pasa a ser la base, actualizar en `templates/` los valores por defecto (`framework` en `.template.config/template.json`, `net10.0` → `net11.0` y los de la tabla "Dónde aparece la versión"), las versiones de `Directory.Packages.props`, el job de CI y el `CHANGELOG` del kit.
5. Agregar la versión a la matriz de CI (`.github/workflows/kit-checks.yml`) cuando su SDK esté disponible en los runners.

## Migrar un proyecto a otra versión
Flujo `ai/workflows/upgrade-dotnet.md` (comando `/upgrade-dotnet` en Claude Code).
