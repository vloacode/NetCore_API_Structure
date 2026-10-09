# Arquitectura de la solución

> **Aplica a:** Todos los perfiles  
> **Propósito:** Perfiles, reglas no negociables, creación del proyecto con la plantilla, estructura, nombres y arranque (Program.cs).  
> **Código:** [`templates/api/`](../templates/api/) (proyecto) y [`templates/entity/`](../templates/entity/) (entidad). Índice: `standards/00-INDEX.md`

## Perfil del proyecto → opciones de la plantilla

El **perfil** está en `docs/00-MASTER_CONTEXT.md`. Cada campo se traduce en una opción de `dotnet new kitapi`:

| Perfil | Opción | Qué cambia |
|---|---|---|
| `Security = enabled / disabled` | `--security true/false` | Identity local + JWT + 2FA + permisos (`standards/05`, `06`) o API pública |
| `Database = sqlserver / postgresql` | `--database sqlserver/postgresql` | Proveedor EF Core; en PostgreSQL, `snake_case` (las tablas de Identity conservan `AspNet*`) y búsquedas con `ILike` |
| `Capabilities` incluye `product-analytics` | `--analytics true` | Módulo de analítica de uso (`standards/17`) |
| `ApiKey = yes` (solo sin seguridad) | `--apikey true` | Escrituras protegidas con `X-Api-Key` (`standards/09`) |
| `TargetFramework` | `--framework net10.0` | Versión de .NET (`standards/16`) |

En el código de la plantilla, las variantes se escriben con `#if (security)`, `#if (postgresql)`, etc. El proyecto generado ya no las contiene: solo queda lo del perfil.

## Crear el proyecto
```bash
dotnet new install ./templates/api            # una vez por máquina (desde la raíz del kit)
dotnet new install ./templates/entity
dotnet new kitapi -n {Project} --security true --database postgresql --analytics false
cd {Project}
dotnet user-secrets set "Jwt:SigningKey" "<64+ caracteres aleatorios>" --project src/{Project}.Api   # con seguridad
dotnet user-secrets set "Seed:AdminPassword" "<contraseña fuerte>" --project src/{Project}.Api       # con seguridad
dotnet user-secrets set "Analytics:HashKey" "<64+ caracteres aleatorios>" --project src/{Project}.Api # con analytics
dotnet ef migrations add InitialCreate -p src/{Project}.Api -o Infrastructure/Persistence/Migrations
dotnet build && dotnet test
```
Llave aleatoria en PowerShell: `[Convert]::ToBase64String((1..64 | % { Get-Random -Max 256 }) -as [byte[]])`.

La plantilla trae fijadas versiones verificadas en `Directory.Packages.props`. Si hay acceso web, revisar en nuget.org si hay parches más nuevos de la misma versión mayor (`standards/16`).

## Reglas no negociables
1. **Controllers → Services → IUnitOfWork/IRepository → DbContext.** Un controller nunca toca `DbContext` ni repositorios. Sin MediatR: el controller llama directamente al service.
2. Los **services** reciben `IUnitOfWork` y obtienen repositorios con `uow.Repository<T>()`. `Repository<T>` **no se registra** en DI.
3. `IRepository<T>` **nunca** llama `SaveChanges`. Persistir es tarea del service (`uow.SaveChangesAsync`) o de `uow.ExecuteInTransactionAsync`.
4. Todo método de service devuelve **`Result` / `Result<T>`**. Nunca `null`, nunca excepciones para flujo de negocio. Los errores se declaran en `{Entity}Errors`.
5. **Nunca exponer entidades EF** por la API. Entran `Create{Entity}Request` / `Update{Entity}Request` y sale `{Entity}Dto`.
6. Lecturas: **proyección** (`Expression<Func<T, TDto>>`) con `ListAsync` / `PagedListAsync` / `FirstOrDefaultAsync(spec, selector)`.
7. Consultas con filtro, orden o paginación: **una `Specification<T>` con nombre de negocio**. Paginar sin `OrderBy` está prohibido (el evaluador lanza excepción).
8. **Auditoría y concurrencia automáticas** con `AuditableEntityInterceptor`. Ningún service asigna `CreatedBy/At`, `UpdatedBy/At` ni `RowVersion`, salvo en `ExecuteUpdateAsync` (no pasa por el interceptor: ahí se asignan `UpdatedAt`, `UpdatedBy` y `RowVersion = Guid.NewGuid()`).
9. `Remove()` sobre una entidad `ISoftDelete` es soft delete. El filtro global oculta los borrados.
10. Escrituras de varios pasos: `uow.ExecuteInTransactionAsync(...)`, que solo hace commit si el `Result` es exitoso. **Nada de efectos externos (emails, HTTP) dentro de la transacción.**
11. Todo método async recibe y propaga **`CancellationToken`**.
12. Validación de entrada con **FluentValidation**. El `ValidationFilter` responde 400 automáticamente.
13. Con seguridad: autorización por **permisos** (`[HasPermission(Permissions.{Entities}.Read)]`), no por nombres de rol.
14. Fechas en **UTC** con `TimeProvider`, nunca `DateTime.Now`.
15. SQL crudo solo parametrizado: `FromSqlInterpolated` / `SqlQuery<T>($"...")`. **Nunca concatenar strings.**
16. Secretos (cadena de conexión de producción, llave JWT, password del admin, `Analytics:HashKey`) en **user-secrets** o variables de entorno, nunca en `appsettings.json`.

## Estructura (un proyecto de API, carpetas por responsabilidad)
```
{Project}/
├── {Project}.slnx, Directory.Build.props, Directory.Packages.props, .editorconfig, global.json
├── Dockerfile, docker-compose.yml
├── src/{Project}.Api/
│   ├── Program.cs                 ← índice del arranque: servicios por área + pipeline HTTP
│   ├── Extensions/                ← AddPersistence, AddSecurity, AddApplication, AddApi, AddObservability
│   ├── Features/{Entities}/       ← UNA carpeta por entidad de negocio, 4 archivos (standards/07)
│   ├── Domain/Common/             ← BaseEntity, IAuditable, ISoftDelete
│   ├── Application/               ← Result, paginación, Specification, abstracciones; Auth y Analytics (módulos del kit)
│   ├── Infrastructure/            ← AppDbContext, UnitOfWork, Repository, interceptor, Identity, email, analytics
│   └── Api/                       ← ApiControllerBase, filtros, errores, middleware, autorización
└── tests/{Project}.IntegrationTests/   ← xUnit v3 + Testcontainers (motor real) + Respawn
```
**Regla:** lo de negocio va en `Features/`; las demás carpetas son la base del kit y casi no se tocan.

> **Variante multi-proyecto** (solo si el usuario la pide, con ADR): `{Project}.Domain`, `.Application`, `.Infrastructure` y `.Api`. El código es el mismo; cambian las referencias entre proyectos.

## Arranque: `Program.cs` + `Extensions/`
`Program.cs` se lee de arriba abajo como un índice:
1. **Servicios**, una línea por área: `AddPersistence` → `AddSecurity` (con seguridad) → `AddApplication` → `AddApi` → `AddObservability`. Cada método vive en `Extensions/{Área}Extensions.cs`.
2. **Pipeline HTTP** (el orden importa): Forwarded Headers (si `ReverseProxy:Enabled`) → manejo de excepciones → `X-Trace-Id` → headers de seguridad → OpenAPI/Scalar (solo Development) → HTTPS → CORS → autenticación → rate limiting → autorización → controllers → health checks.
3. **Arranque**: migraciones si `Database:ApplyMigrationsOnStartup` (solo desarrollo y tests) y, con seguridad, el seeder de roles/admin.

- **Registro automático:** `AddApplication()` registra como scoped cada clase `*Service` de `Features/` que implemente `I*Service`. Una entidad nueva **no toca** `Program.cs` ni `Extensions/`.
- Algo nuevo y transversal (caché, colas, un cliente HTTP externo) se agrega como método en el `Extensions/` del área o en un archivo nuevo `Extensions/{Área}Extensions.cs`, y una línea en `Program.cs`.
- `appsettings.json` sin secretos. `appsettings.Development.json` activa `Database:ApplyMigrationsOnStartup`.

→ Código: [`Program.cs`](../templates/api/src/KitApi.Api/Program.cs) · [`Extensions/`](../templates/api/src/KitApi.Api/Extensions/) · [`appsettings.json`](../templates/api/src/KitApi.Api/appsettings.json)

## Convenciones de nombres

| Elemento | Nombre |
|---|---|
| Carpeta / namespace | `Features/{Entities}/` → `{Project}.Features.{Entities}` |
| Entidad + configuración EF | `{Entity}`, `{Entity}Configuration` (en `{Entity}.cs`) |
| DTO, requests, filtro | `{Entity}Dto`, `Create{Entity}Request`, `Update{Entity}Request`, `{Entity}Filter` |
| Errores, mapeo, validadores | `{Entity}Errors`, `{Entity}Mappings`, `Create{Entity}RequestValidator` (en `{Entity}Contracts.cs`) |
| Service y especificaciones | `I{Entity}Service`, `{Entity}Service`, `{Entity}ByIdSpec`, `{Entities}ByFilterSpec` (en `{Entity}Service.cs`) |
| Controller | `{Entities}Controller` → ruta `api/{entities}` |
| Permisos (con seguridad) | `Permissions.{Entities}.Read/Write/Delete` → `"{entities}.read"` |

Marcadores en los standards y docs: `{Project}`, `{Entity}`, `{Entities}`, `{entity}`/`{entities}` (minúsculas), `{Parent}`. `Name` y `Code` son campos **representativos**: se reemplazan por los reales. `{id:int}` en una ruta no es un marcador: es una restricción de ASP.NET Core.
