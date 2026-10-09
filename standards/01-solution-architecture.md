# Arquitectura de la solución

> **Aplica a:** Todos los perfiles  
> **Propósito:** Reglas no negociables, marcadores, creación del proyecto, estructura, nombres, DependencyInjection, Program.cs y configuración.  
> Índice general: `standards/00-INDEX.md`

## Perfil del proyecto y marcas

El **perfil del proyecto** está en `docs/00-MASTER_CONTEXT.md`, sección "Perfil del proyecto". Léelo antes de aplicar este archivo.

| Perfil | `Security` | Qué incluye |
|---|---|---|
| Estándar con seguridad | `enabled` | Todo: arquitectura + Identity local + JWT + permisos (`standards/05*`, `06`) |
| Estándar sin seguridad (API pública) | `disabled` | Todo excepto Identity, JWT, permisos y controllers de Auth |
| Entrevista completa | según respuestas | Base del perfil con seguridad, ajustada por las capacidades elegidas |

Además del modo, el perfil fija el **motor de base de datos** (`Database`) y la **versión de .NET** (`TargetFramework`, ver `standards/16-framework-versions.md`).

| `Database` | Proveedor EF Core | Notas |
|---|---|---|
| `sqlserver` (por defecto) | `Microsoft.EntityFrameworkCore.SqlServer` | Collation sin distinguir mayúsculas por defecto |
| `postgresql` | `Npgsql.EntityFrameworkCore.PostgreSQL` + `EFCore.NamingConventions` | Tablas y columnas propias en `snake_case` (las tablas de Identity conservan su nombre `AspNet*`, que Identity fija explícitamente); búsquedas con `ILike` |

Marcas en el código de este y otros standards (al crear el proyecto se conserva solo lo del perfil y se quitan los comentarios de marca):

| Marca | La línea, sentencia o método solo existe si… |
|---|---|
| `// [SEC]` | `Security = enabled` |
| `// [PUB]` | `Security = disabled` (API pública) |
| `// [MSSQL]` | `Database = sqlserver` |
| `// [PGSQL]` | `Database = postgresql` |

## Reglas no negociables
1. **Controllers → Services → IUnitOfWork/IRepository → DbContext.** Un controller nunca toca `DbContext` ni repositorios.
2. Los **Services** de negocio reciben `IUnitOfWork` y obtienen repositorios con `uow.Repository<T>()`. `Repository<T>` **no se registra** en DI.
3. `IRepository<T>` **nunca** llama `SaveChanges`. Persistir es tarea del service (`uow.SaveChangesAsync`) o de `uow.ExecuteInTransactionAsync`.
4. Todo método de service devuelve **`Result` / `Result<T>`**. Nunca `null`, nunca excepciones para flujo de negocio. Los errores se declaran en una clase estática `{Entity}Errors`.
5. **Nunca exponer entidades EF** por la API. Entran `Create{Entity}Request` / `Update{Entity}Request` y sale `{Entity}Dto`.
6. Lecturas para devolver datos: **proyección** (`Expression<Func<T, TDto>>`) con `ListAsync` / `PagedListAsync` / `FirstOrDefaultAsync(spec, selector)`.
7. Consultas con filtro, orden o paginación: **una clase `Specification<T>` con nombre de negocio**. Paginar sin `OrderBy` está prohibido (el evaluador lanza excepción).
8. **Auditoría y concurrencia automáticas** con `AuditableEntityInterceptor`. Ningún service asigna `CreatedBy`, `CreatedAt`, `UpdatedBy`, `UpdatedAt` ni `RowVersion`, salvo en `ExecuteUpdateAsync`, que no pasa por el interceptor (ahí se asignan `UpdatedAt`, `UpdatedBy` y `RowVersion = Guid.NewGuid()`). Sin seguridad, los campos `*By` quedan en `null`.
9. `Remove()` sobre una entidad `ISoftDelete` se convierte en soft delete. El filtro global oculta los registros borrados.
10. Una operación que escribe en varios pasos usa `uow.ExecuteInTransactionAsync(...)`: solo hace commit si el `Result` es exitoso. **Nada de efectos externos (emails, HTTP) dentro de la transacción.**
11. Todo método async recibe y propaga **`CancellationToken`**.
12. Validación de entrada con **FluentValidation** (`{Request}Validator`). El `ValidationFilter` responde 400 automáticamente.
13. `[SEC]` Autorización por **permisos** (`[HasPermission(Permissions.{Entities}.Read)]`), no por nombres de rol sueltos.
14. Fechas en **UTC** a través de `TimeProvider`, nunca `DateTime.Now`.
15. SQL crudo solo parametrizado: `FromSqlInterpolated` / `SqlQuery<T>($"...")`. **Nunca concatenar strings.**
16. Secretos (cadena de conexión de producción; con seguridad, también la llave JWT y el password del admin seed) en **user-secrets** o variables de entorno, nunca en `appsettings.json`.

## Marcadores (placeholders)

| Marcador | Significado | Ejemplo de reemplazo (ilustrativo) |
|---|---|---|
| `{Project}` | Nombre raíz de la solución y del namespace | `Acme` |
| `{Entity}` | Entidad de negocio, singular, PascalCase | `Invoice` |
| `{Entities}` | Plural PascalCase (controller, permisos, tabla) | `Invoices` |
| `{entity}` / `{entities}` | Singular y plural en minúsculas (rutas, claves de permiso) | `invoice` / `invoices` |
| `{Parent}` / `{parent}` | Entidad relacionada (FK), PascalCase / camelCase | `Customer` / `customer` |
| `{Flag}` | Propiedad booleana con regla "solo uno activo" | `IsPrimary` |
| `Name`, `Code` | Campos **representativos** en las plantillas. Reemplazar por los reales. | — |

> `{id:int}` y `{id:guid}` dentro de `[HttpGet("...")]` **no son marcadores**: son restricciones de ruta de ASP.NET Core.

## Crear el proyecto y paquetes

```bash
mkdir {Project} && cd {Project}
dotnet new sln -n {Project}                                   # crea {Project}.slnx
dotnet new webapi -n {Project}.Api -o src/{Project}.Api --use-controllers -f net10.0   # TargetFramework del perfil
dotnet sln add src/{Project}.Api
# Directory.Build.props, Directory.Packages.props, .editorconfig y global.json: standards/14
cd src/{Project}.Api
dotnet add package Microsoft.EntityFrameworkCore.SqlServer          # [MSSQL]
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL           # [PGSQL]
dotnet add package EFCore.NamingConventions                        # [PGSQL]
dotnet add package Microsoft.EntityFrameworkCore.Design
dotnet add package Scalar.AspNetCore
dotnet add package FluentValidation.DependencyInjectionExtensions
dotnet add package Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore
dotnet add package Microsoft.AspNetCore.Identity.EntityFrameworkCore   # [SEC]
dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer       # [SEC]
dotnet user-secrets init
```

Versiones de referencia (oct-2026, .NET 10): ASP.NET Core / EF Core **10.0.12**, Npgsql EF Core **10.0.x**, EFCore.NamingConventions **10.0.x**, FluentValidation **12.1.1**, Scalar.AspNetCore **2.17.x**. Regla: los paquetes de Microsoft y de proveedores EF usan la **misma versión mayor que el framework** (`standards/16`). `dotnet add package` sin versión toma la última estable; si hay acceso web, verificar en nuget.org.

## Estructura (un proyecto de API separado por carpetas)

Raíz de la solución (`{Project}.slnx`, `Directory.Build.props`, `tests/`…): `standards/14-coding-standards.md`.

```
src/{Project}.Api/
├── Domain/
│   ├── Common/BaseEntity.cs                  ← BaseEntity, IAuditable, ISoftDelete
│   └── Entities/                             ← entidades de negocio (standards/07)
├── Application/
│   ├── Abstractions/
│   │   ├── Persistence/IRepository.cs, IUnitOfWork.cs
│   │   └── Services/ICurrentUserService.cs   ← + IEmailSender
│   ├── Common/
│   │   ├── Results/Result.cs                 ← Result, Result<T>, Error, ErrorType
│   │   ├── Paging/PagedResult.cs             ← PaginationParams, PagedResult<T>
│   │   ├── Specifications/                   ← Specification<T>, PredicateBuilder
│   │   └── Security/Permissions.cs           ← [SEC] AppRoles, Permissions
│   └── Features/
│       ├── Auth/                             ← [SEC] contratos y validadores de Auth/Account/Users/Roles
│       └── {Entities}/                       ← uno por entidad de negocio (standards/07)
├── Infrastructure/
│   ├── Persistence/
│   │   ├── AppDbContext.cs
│   │   ├── UnitOfWork.cs
│   │   ├── DatabaseSeeder.cs                 ← [SEC] roles + admin
│   │   ├── Configurations/                   ← IEntityTypeConfiguration<T> por entidad
│   │   ├── Interceptors/AuditableEntityInterceptor.cs
│   │   ├── Repositories/Repository.cs, SpecificationEvaluator.cs
│   │   └── Migrations/
│   ├── Identity/                             ← [SEC] AppUser, AppRole, RefreshToken, TokenService, SessionManager,
│   │                                            AuthService, AccountService, UserAdminService, RoleService, ...
│   ├── Services/SystemCurrentUserService.cs  ← solo SIN seguridad (UserId = null)
│   └── Email/LoggingEmailSender.cs
├── Api/
│   ├── Controllers/                          ← ApiControllerBase, {Entities}; [SEC] Auth, Account, Users, Roles
│   ├── Authorization/                        ← [SEC] HasPermissionAttribute, PermissionPolicyProvider
│   ├── Filters/ValidationFilter.cs
│   ├── Errors/GlobalExceptionHandler.cs
│   ├── Middleware/                           ← SecurityHeaders, TraceIdHeader (standards/09, 10)
│   └── OpenApi/BearerSecuritySchemeTransformer.cs   ← [SEC]
├── DependencyInjection.cs
├── Program.cs
└── appsettings.json
```

> **Variante multi-proyecto** (si el usuario la pide): `{Project}.Domain`, `{Project}.Application` (referencia Domain y EF Core por `UpdateSettersBuilder`), `{Project}.Infrastructure` y `{Project}.Api`. El código es el mismo; solo cambian las referencias entre proyectos.

## Convenciones de nombres

| Elemento | Nombre |
|---|---|
| Entidad | `{Entity}` (hereda `BaseEntity`) |
| Configuración EF | `{Entity}Configuration` |
| DTO de salida | `{Entity}Dto` |
| Requests | `Create{Entity}Request`, `Update{Entity}Request` |
| Filtro paginado | `{Entity}Filter : PaginationParams` |
| Especificaciones | `{Entity}ByIdSpec`, `{Entities}ByFilterSpec`, … |
| Errores | `{Entity}Errors` |
| Mapeo | `{Entity}Mappings` (`Projection`, `ToEntity()`, `ApplyTo()`) |
| Validadores | `Create{Entity}RequestValidator`, `Update{Entity}RequestValidator` |
| Service | `I{Entity}Service` / `{Entity}Service` |
| Controller | `{Entities}Controller` → ruta `api/{entities}` |
| Permisos `[SEC]` | `Permissions.{Entities}.Read/Write/Delete` → `"{entities}.read"` |

## Arranque
DependencyInjection, `Program.cs`, `appsettings.json` y secretos: `standards/01a-bootstrap.md`.
