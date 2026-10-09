# Guía de arquitectura para IA — ASP.NET Core Web API (.NET 10 LTS)
## Repository + Unit of Work + Specification + Result + Identity local con JWT

> **Para la IA que lea este documento:** esta es la arquitectura obligatoria para crear o extender la API.
> No es un proyecto existente para copiar: es un **molde**. Adáptalo al dominio que pida el usuario.
>
> - Las piezas de **infraestructura** (secciones 3 a 7) y de **autenticación** (sección 8) son genéricas y se implementan tal cual, cambiando solo `{Project}`.
> - Las **entidades de negocio** (sección 9) **NO existen en esta guía**. Se escriben con plantillas con marcadores (`{Entity}`, `{Entities}`…), y la IA las crea a partir de los requerimientos reales del usuario.
>
> Stack: **.NET 10 (LTS)** · ASP.NET Core Web API con **Controllers** · **EF Core 10** + SQL Server · **ASP.NET Core Identity** (usuarios y roles en la BD del propio proyecto, sin proveedores externos) · **JWT propio + refresh tokens** · FluentValidation · OpenAPI + Scalar.
> **Sin AutoMapper**: el mapeo es manual o por proyección (`Select`).

---

## Índice
0. [Instrucciones para la IA](#0-instrucciones-para-la-ia)
1. [Marcadores (placeholders)](#1-marcadores-placeholders)
2. [Crear el proyecto, paquetes y estructura](#2-crear-el-proyecto-paquetes-y-estructura)
3. [Domain: entidad base](#3-domain-entidad-base)
4. [Application: Result, paginación, especificaciones, abstracciones](#4-application-common)
5. [Persistencia: DbContext, interceptor, repositorio, Unit of Work](#5-persistencia)
6. [Capa API: controller base, validación, errores](#6-capa-api)
7. [Autorización por permisos](#7-autorización-por-permisos)
8. [Autenticación con Identity local + JWT (completo)](#8-autenticación-con-identity-local--jwt)
9. [Entidades de negocio: plantillas para el proyecto real](#9-entidades-de-negocio-plantillas)
10. [DependencyInjection, Program.cs y configuración](#10-dependencyinjection-programcs-y-configuración)
11. [Receta: agregar una entidad de negocio (checklist)](#11-receta-agregar-una-entidad-de-negocio)
12. [Testing](#12-testing)
13. [Producción: checklist](#13-producción-checklist)
14. [Anexo: errores que esta arquitectura evita](#14-anexo-errores-que-esta-arquitectura-evita)

---

## 0. Instrucciones para la IA

### 0.1 Antes de escribir código, confirmar con el usuario
1. Nombre de la solución → `{Project}`.
2. **Entidades del dominio**: nombres, propiedades, relaciones y reglas de negocio. **No inventarlas.**
3. Tipo de PK para las entidades de negocio: `int` identity por defecto. Los usuarios y roles usan siempre `Guid`.
4. Si usa soft delete (sí por defecto).
5. Roles iniciales, aparte de `Admin` y `User`, y los permisos de cada uno.
6. Si habrá envío real de email (SMTP) o solo log en desarrollo.
7. URL del frontend (para los enlaces de los emails) y orígenes de CORS.

### 0.2 Reglas no negociables
1. **Controllers → Services → IUnitOfWork/IRepository → DbContext.** Un controller nunca toca `DbContext` ni repositorios.
2. Los **Services** de negocio reciben `IUnitOfWork` y obtienen repositorios con `uow.Repository<T>()`. `Repository<T>` **no se registra** en DI.
3. `IRepository<T>` **nunca** llama `SaveChanges`. Persistir es tarea del service (`uow.SaveChangesAsync`) o de `uow.ExecuteInTransactionAsync`.
4. Todo método de service devuelve **`Result` / `Result<T>`**. Nunca `null`, nunca excepciones para flujo de negocio. Los errores se declaran en una clase estática `{Entity}Errors`.
5. **Nunca exponer entidades EF** por la API. Entran `Create{Entity}Request` / `Update{Entity}Request` y sale `{Entity}Dto`.
6. Lecturas para devolver datos: **proyección** (`Expression<Func<T, TDto>>`) con `ListAsync` / `PagedListAsync` / `FirstOrDefaultAsync(spec, selector)`.
7. Consultas con filtro, orden o paginación: **una clase `Specification<T>` con nombre de negocio**. Paginar sin `OrderBy` está prohibido (el evaluador lanza excepción).
8. **Auditoría automática** con `AuditableEntityInterceptor`. Ningún service asigna `CreatedBy`, `CreatedAt`, `UpdatedBy` ni `UpdatedAt`, salvo en `ExecuteUpdateAsync`, que no pasa por el interceptor.
9. `Remove()` sobre una entidad `ISoftDelete` se convierte en soft delete. El filtro global oculta los registros borrados.
10. Una operación que escribe en varios pasos usa `uow.ExecuteInTransactionAsync(...)`: solo hace commit si el `Result` es exitoso. **Nada de efectos externos (emails) dentro de la transacción.**
11. Todo método async recibe y propaga **`CancellationToken`**.
12. Validación de entrada con **FluentValidation** (`{Request}Validator`). El `ValidationFilter` responde 400 automáticamente.
13. Autorización por **permisos** (`[HasPermission(Permissions.{Entities}.Read)]`), no por nombres de rol sueltos.
14. Fechas en **UTC** a través de `TimeProvider`, nunca `DateTime.Now`.
15. SQL crudo solo parametrizado: `FromSqlInterpolated` / `SqlQuery<T>($"...")`. **Nunca concatenar strings.**
16. Secretos (llave JWT, password del admin seed, cadena de conexión de producción) en **user-secrets** o variables de entorno, nunca en `appsettings.json`.

---

## 1. Marcadores (placeholders)

| Marcador | Significado | Ejemplo de reemplazo (ilustrativo) |
|---|---|---|
| `{Project}` | Nombre raíz de la solución y del namespace | `Acme` |
| `{Entity}` | Entidad de negocio, singular, PascalCase | `Invoice` |
| `{Entities}` | Plural PascalCase (controller, permisos, tabla) | `Invoices` |
| `{entity}` / `{entities}` | Singular y plural en minúsculas (rutas, claves de permiso) | `invoice` / `invoices` |
| `{Parent}` | Entidad relacionada (FK) | `Customer` |
| `{Flag}` | Propiedad booleana con regla "solo uno activo" | `IsPrimary` |
| `Name`, `Code` | Campos **representativos** en las plantillas. Reemplazar por los reales. | — |

> `{id:int}` y `{id:guid}` dentro de `[HttpGet("...")]` **no son marcadores**: son restricciones de ruta de ASP.NET Core.

---

## 2. Crear el proyecto, paquetes y estructura

```bash
dotnet new webapi -n {Project}.Api --use-controllers -f net10.0
cd {Project}.Api
dotnet add package Microsoft.AspNetCore.Identity.EntityFrameworkCore
dotnet add package Microsoft.EntityFrameworkCore.SqlServer
dotnet add package Microsoft.EntityFrameworkCore.Design
dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer
dotnet add package Scalar.AspNetCore
dotnet add package FluentValidation.DependencyInjectionExtensions
dotnet user-secrets init
```

Versiones de referencia verificadas (oct-2026): ASP.NET Core / EF Core **10.0.12**, FluentValidation **12.1.1**, Scalar.AspNetCore **2.17.x**. `dotnet add package` sin versión toma la última estable.

### 2.1 Estructura (un solo proyecto, separado por carpetas)

```
{Project}.Api/
├── Domain/
│   ├── Common/BaseEntity.cs                  ← BaseEntity, IAuditable, ISoftDelete
│   └── Entities/                             ← entidades de negocio (sección 9)
├── Application/
│   ├── Abstractions/
│   │   ├── Persistence/IRepository.cs, IUnitOfWork.cs
│   │   └── Services/ICurrentUserService.cs   ← + IEmailSender
│   ├── Common/
│   │   ├── Results/Result.cs                 ← Result, Result<T>, Error, ErrorType
│   │   ├── Paging/PagedResult.cs             ← PaginationParams, PagedResult<T>
│   │   ├── Specifications/                   ← Specification<T>, PredicateBuilder
│   │   └── Security/Permissions.cs           ← AppRoles, Permissions
│   └── Features/
│       ├── Auth/                             ← contratos y validadores de Auth/Account/Users/Roles
│       └── {Entities}/                       ← uno por entidad de negocio (sección 9)
├── Infrastructure/
│   ├── Persistence/
│   │   ├── AppDbContext.cs
│   │   ├── UnitOfWork.cs
│   │   ├── DatabaseSeeder.cs
│   │   ├── Configurations/                   ← IEntityTypeConfiguration<T> por entidad
│   │   ├── Interceptors/AuditableEntityInterceptor.cs
│   │   ├── Repositories/Repository.cs, SpecificationEvaluator.cs
│   │   └── Migrations/
│   ├── Identity/                             ← AppUser, AppRole, RefreshToken, TokenService, SessionManager,
│   │                                            AuthService, AccountService, UserAdminService, RoleService, ...
│   └── Email/LoggingEmailSender.cs
├── Api/
│   ├── Controllers/                          ← ApiControllerBase, Auth, Account, Users, Roles, {Entities}
│   ├── Authorization/                        ← HasPermissionAttribute, PermissionPolicyProvider
│   ├── Filters/ValidationFilter.cs
│   ├── Errors/GlobalExceptionHandler.cs
│   └── OpenApi/BearerSecuritySchemeTransformer.cs
├── DependencyInjection.cs
├── Program.cs
└── appsettings.json
```

> **Variante multi-proyecto** (si el usuario la pide): `{Project}.Domain`, `{Project}.Application` (referencia Domain y EF Core por `UpdateSettersBuilder`), `{Project}.Infrastructure` y `{Project}.Api`. El código es el mismo; solo cambian las referencias entre proyectos.

### 2.2 Convenciones de nombres

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
| Permisos | `Permissions.{Entities}.Read/Write/Delete` → `"{entities}.read"` |

---

## 3. Domain: entidad base

`Domain/Common/BaseEntity.cs`
```csharp
namespace {Project}.Domain.Common;

public interface IAuditable
{
    DateTime CreatedAt { get; set; }
    Guid? CreatedBy { get; set; }
    DateTime? UpdatedAt { get; set; }
    Guid? UpdatedBy { get; set; }
}

public interface ISoftDelete
{
    bool IsDeleted { get; set; }
    DateTime? DeletedAt { get; set; }
    Guid? DeletedBy { get; set; }
}

public abstract class BaseEntity<TKey> : IAuditable, ISoftDelete
    where TKey : IEquatable<TKey>
{
    public TKey Id { get; set; } = default!;

    // Auditoría: la llena AuditableEntityInterceptor. Nunca asignarla a mano en servicios.
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    // Soft delete: Remove() se convierte en IsDeleted = true en el interceptor.
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }

    // Concurrencia optimista (rowversion en SQL Server).
    public byte[] RowVersion { get; set; } = [];
}

/// <summary>Entidad con PK int identity (default del proyecto).</summary>
public abstract class BaseEntity : BaseEntity<int>;
```

> `CreatedBy`, `UpdatedBy` y `DeletedBy` guardan el `Guid` del `AppUser` autenticado.

---

## 4. Application: Common

### 4.1 `Result` y `Error` — `Application/Common/Results/Result.cs`
```csharp
namespace {Project}.Application.Common.Results;

public enum ErrorType
{
    Failure = 0,
    Validation = 1,
    NotFound = 2,
    Conflict = 3,
    Unauthorized = 4,
    Forbidden = 5
}

public sealed record Error(
    string Code,
    string Message,
    ErrorType Type = ErrorType.Failure,
    IReadOnlyDictionary<string, string[]>? Details = null)
{
    public static readonly Error None = new(string.Empty, string.Empty);

    public static Error Failure(string code, string message) => new(code, message, ErrorType.Failure);
    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);
    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);
    public static Error Unauthorized(string code, string message) => new(code, message, ErrorType.Unauthorized);
    public static Error Forbidden(string code, string message) => new(code, message, ErrorType.Forbidden);

    public static Error Validation(string code, string message, IReadOnlyDictionary<string, string[]>? details = null)
        => new(code, message, ErrorType.Validation, details);

    /// <summary>Convierte los errores de ASP.NET Identity en un Error de validación.</summary>
    public static Error FromIdentity(IEnumerable<Microsoft.AspNetCore.Identity.IdentityError> errors)
    {
        var details = errors
            .GroupBy(e => e.Code)
            .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray());
        return Validation("Identity.Validation", "Uno o más errores de validación.", details);
    }
}

public class Result
{
    protected Result(bool isSuccess, Error error)
    {
        if (isSuccess && error != Error.None) throw new InvalidOperationException("Un resultado exitoso no puede tener error.");
        if (!isSuccess && error == Error.None) throw new InvalidOperationException("Un resultado fallido debe tener error.");
        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public Error Error { get; }

    public static Result Success() => new(true, Error.None);
    public static Result Failure(Error error) => new(false, error);
    public static Result<T> Success<T>(T value) => new(value, true, Error.None);
    public static Result<T> Failure<T>(Error error) => new(default, false, error);

    public static implicit operator Result(Error error) => Failure(error);
}

public class Result<T> : Result
{
    private readonly T? _value;

    protected internal Result(T? value, bool isSuccess, Error error) : base(isSuccess, error) => _value = value;

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("No se puede leer Value de un resultado fallido.");

    public static implicit operator Result<T>(T value) => Success(value);
    public static implicit operator Result<T>(Error error) => Failure<T>(error);
}
```
Gracias a las conversiones implícitas, en un service se escribe `return dto;` o `return {Entity}Errors.NotFound(id);`.

### 4.2 Paginación — `Application/Common/Paging/PagedResult.cs`
```csharp
namespace {Project}.Application.Common.Paging;

public class PaginationParams
{
    public const int MaxPageSize = 100;
    private int _pageNumber = 1;
    private int _pageSize = 20;

    public int PageNumber
    {
        get => _pageNumber;
        set => _pageNumber = value < 1 ? 1 : value;
    }

    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value < 1 ? 1 : Math.Min(value, MaxPageSize);
    }

    public int Skip => (PageNumber - 1) * PageSize;
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int PageNumber, int PageSize, int TotalCount)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPrevious => PageNumber > 1;
    public bool HasNext => PageNumber < TotalPages;
}
```

### 4.3 `PredicateBuilder` — `Application/Common/Specifications/PredicateBuilder.cs`
```csharp
using System.Linq.Expressions;

namespace {Project}.Application.Common.Specifications;

/// <summary>Compone expresiones (And/Or/Not) para filtros dinámicos traducibles a SQL.</summary>
public static class PredicateBuilder
{
    public static Expression<Func<T, bool>> True<T>() => _ => true;
    public static Expression<Func<T, bool>> False<T>() => _ => false;

    public static Expression<Func<T, bool>> And<T>(this Expression<Func<T, bool>> first, Expression<Func<T, bool>> second)
        => first.Compose(second, Expression.AndAlso);

    public static Expression<Func<T, bool>> Or<T>(this Expression<Func<T, bool>> first, Expression<Func<T, bool>> second)
        => first.Compose(second, Expression.OrElse);

    public static Expression<Func<T, bool>> Not<T>(this Expression<Func<T, bool>> expression)
        => Expression.Lambda<Func<T, bool>>(Expression.Not(expression.Body), expression.Parameters);

    /// <summary>Agrega la condición solo si <paramref name="condition"/> es true.</summary>
    public static Expression<Func<T, bool>> AndIf<T>(this Expression<Func<T, bool>> first, bool condition, Expression<Func<T, bool>> second)
        => condition ? first.And(second) : first;

    private static Expression<TDelegate> Compose<TDelegate>(this Expression<TDelegate> first, Expression<TDelegate> second,
        Func<Expression, Expression, Expression> merge) where TDelegate : Delegate
    {
        var map = first.Parameters
            .Select((f, i) => new { f, s = second.Parameters[i] })
            .ToDictionary(p => p.s, p => p.f);

        var secondBody = new ParameterRebinder(map).Visit(second.Body);
        return Expression.Lambda<TDelegate>(merge(first.Body, secondBody), first.Parameters);
    }

    private sealed class ParameterRebinder(Dictionary<ParameterExpression, ParameterExpression> map) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node)
            => base.VisitParameter(map.TryGetValue(node, out var replacement) ? replacement : node);
    }
}
```

### 4.4 Specification — `Application/Common/Specifications/Specification.cs`
```csharp
using System.Linq.Expressions;
using {Project}.Application.Common.Paging;

namespace {Project}.Application.Common.Specifications;

public interface ISpecification<T>
{
    Expression<Func<T, bool>>? Criteria { get; }
    IReadOnlyList<Expression<Func<T, object>>> Includes { get; }
    IReadOnlyList<string> IncludeStrings { get; }
    IReadOnlyList<(Expression<Func<T, object>> KeySelector, bool Descending)> OrderExpressions { get; }
    int? Skip { get; }
    int? Take { get; }
    bool AsNoTracking { get; }
    bool AsSplitQuery { get; }
    bool IgnoreQueryFilters { get; }
}

/// <summary>
/// Encapsula una consulta reutilizable: filtro + includes + orden + paginación.
/// Crear una clase por consulta con nombre de negocio (ej. {Entities}ByFilterSpec).
/// </summary>
public abstract class Specification<T> : ISpecification<T>
{
    private readonly List<Expression<Func<T, object>>> _includes = [];
    private readonly List<string> _includeStrings = [];
    private readonly List<(Expression<Func<T, object>>, bool)> _orderExpressions = [];

    protected Specification() { }
    protected Specification(Expression<Func<T, bool>> criteria) => Criteria = criteria;

    public Expression<Func<T, bool>>? Criteria { get; private set; }
    public IReadOnlyList<Expression<Func<T, object>>> Includes => _includes;
    public IReadOnlyList<string> IncludeStrings => _includeStrings;
    public IReadOnlyList<(Expression<Func<T, object>> KeySelector, bool Descending)> OrderExpressions => _orderExpressions;
    public int? Skip { get; private set; }
    public int? Take { get; private set; }
    public bool AsNoTracking { get; private set; } = true;
    public bool AsSplitQuery { get; private set; }
    public bool IgnoreQueryFilters { get; private set; }

    protected void Where(Expression<Func<T, bool>> criteria)
        => Criteria = Criteria is null ? criteria : Criteria.And(criteria);

    protected void Include(Expression<Func<T, object>> include) => _includes.Add(include);

    /// <summary>Para ThenInclude anidados: "{Parent}.OtraNavegacion".</summary>
    protected void Include(string navigationPath) => _includeStrings.Add(navigationPath);

    protected void OrderBy(Expression<Func<T, object>> key) => _orderExpressions.Add((key, false));
    protected void OrderByDescending(Expression<Func<T, object>> key) => _orderExpressions.Add((key, true));

    protected void ApplyPaging(PaginationParams paging)
    {
        Skip = paging.Skip;
        Take = paging.PageSize;
    }

    protected void EnableTracking() => AsNoTracking = false;
    protected void SplitQuery() => AsSplitQuery = true;   // usar con 2+ Include de colecciones
    protected void IncludeSoftDeleted() => IgnoreQueryFilters = true;
}
```

### 4.5 Abstracciones de persistencia

`Application/Abstractions/Persistence/IRepository.cs`
```csharp
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Query;
using {Project}.Application.Common.Paging;
using {Project}.Application.Common.Specifications;

namespace {Project}.Application.Abstractions.Persistence;

/// <summary>
/// Repositorio genérico. NUNCA llama SaveChanges: eso es responsabilidad de IUnitOfWork.
/// Las lecturas por especificación son AsNoTracking salvo que la spec pida tracking.
/// </summary>
public interface IRepository<T> where T : class
{
    // ---- Lectura de entidades ----
    /// <summary>Busca por PK con tracking (para luego modificar/eliminar).</summary>
    ValueTask<T?> GetByIdAsync(object id, CancellationToken ct = default);
    Task<T?> FirstOrDefaultAsync(ISpecification<T> spec, CancellationToken ct = default);
    Task<IReadOnlyList<T>> ListAsync(ISpecification<T> spec, CancellationToken ct = default);

    // ---- Lectura con proyección (preferida para devolver DTOs) ----
    Task<TResult?> FirstOrDefaultAsync<TResult>(ISpecification<T> spec, Expression<Func<T, TResult>> selector, CancellationToken ct = default);
    Task<IReadOnlyList<TResult>> ListAsync<TResult>(ISpecification<T> spec, Expression<Func<T, TResult>> selector, CancellationToken ct = default);

    /// <summary>Cuenta con el filtro de la spec (ignora paginación) y trae la página proyectada.</summary>
    Task<PagedResult<TResult>> PagedListAsync<TResult>(ISpecification<T> spec, Expression<Func<T, TResult>> selector,
        PaginationParams paging, CancellationToken ct = default);

    // ---- Agregados ----
    Task<int> CountAsync(ISpecification<T>? spec = null, CancellationToken ct = default);
    Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);

    // ---- Escritura (se persiste con IUnitOfWork.SaveChangesAsync) ----
    void Add(T entity);
    void AddRange(IEnumerable<T> entities);
    void Update(T entity);
    void Remove(T entity);
    void RemoveRange(IEnumerable<T> entities);

    // ---- Operaciones masivas (se ejecutan YA en BD; no pasan por el interceptor de auditoría) ----
    Task<int> ExecuteUpdateAsync(Expression<Func<T, bool>> predicate, Action<UpdateSettersBuilder<T>> setters, CancellationToken ct = default);
    Task<int> ExecuteDeleteAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);
}
```

`Application/Abstractions/Persistence/IUnitOfWork.cs`
```csharp
using {Project}.Application.Common.Results;

namespace {Project}.Application.Abstractions.Persistence;

/// <summary>
/// Unit of Work: un DbContext compartido por request (Scoped).
/// Todos los repositorios obtenidos aquí comparten contexto y transacción.
/// </summary>
public interface IUnitOfWork
{
    IRepository<T> Repository<T>() where T : class;

    Task<int> SaveChangesAsync(CancellationToken ct = default);

    /// <summary>
    /// Ejecuta varias operaciones en UNA transacción de BD.
    /// Hace SaveChanges + Commit solo si el Result es exitoso; si falla o lanza excepción, Rollback.
    /// La operación puede re-ejecutarse si la estrategia de reintentos lo requiere: no tener efectos externos (emails) dentro.
    /// </summary>
    Task<Result<T>> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<Result<T>>> operation, CancellationToken ct = default);

    Task<Result> ExecuteInTransactionAsync(Func<CancellationToken, Task<Result>> operation, CancellationToken ct = default);
}
```

`Application/Abstractions/Services/ICurrentUserService.cs`
```csharp
namespace {Project}.Application.Abstractions.Services;

public interface ICurrentUserService
{
    Guid? UserId { get; }
    string? Email { get; }
    bool IsAuthenticated { get; }
    bool IsInRole(string role);
    bool HasPermission(string permission);
    string? IpAddress { get; }
    string? UserAgent { get; }
}

public interface IEmailSender
{
    Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default);
}
```

---

## 5. Persistencia

### 5.1 `Infrastructure/Persistence/AppDbContext.cs`
```csharp
using System.Linq.Expressions;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using {Project}.Domain.Common;
using {Project}.Infrastructure.Identity;

namespace {Project}.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<AppUser, AppRole, Guid>(options)
{
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    // Entidades de negocio (sección 9): una línea por entidad.
    // public DbSet<{Entity}> {Entities} => Set<{Entity}>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);   // primero Identity

        // Una clase IEntityTypeConfiguration<T> por entidad en Persistence/Configurations.
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Filtro global de soft delete para toda entidad ISoftDelete.
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            if (!typeof(ISoftDelete).IsAssignableFrom(entityType.ClrType) || entityType.BaseType is not null)
                continue;

            var parameter = Expression.Parameter(entityType.ClrType, "e");
            var body = Expression.Not(Expression.Property(parameter, nameof(ISoftDelete.IsDeleted)));
            builder.Entity(entityType.ClrType).HasQueryFilter(Expression.Lambda(body, parameter));
        }
    }
}
```

### 5.2 Configuración base y de Identity — `Infrastructure/Persistence/Configurations/`
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using {Project}.Domain.Common;
using {Project}.Infrastructure.Identity;

namespace {Project}.Infrastructure.Persistence.Configurations;

/// <summary>Configuración común de BaseEntity: concurrencia, defaults SQL, índice de soft delete.</summary>
public abstract class BaseEntityConfiguration<T> : IEntityTypeConfiguration<T> where T : BaseEntity
{
    public virtual void Configure(EntityTypeBuilder<T> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.RowVersion).IsRowVersion();
        builder.Property(e => e.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()"); // nunca HasDefaultValue(DateTime.Now)
        builder.HasIndex(e => e.IsDeleted);
    }
}

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.TokenHash).HasMaxLength(128).IsRequired();
        builder.HasIndex(t => t.TokenHash).IsUnique();
        builder.HasIndex(t => new { t.UserId, t.FamilyId });
        builder.Property(t => t.ReplacedByTokenHash).HasMaxLength(128);
        builder.Property(t => t.CreatedByIp).HasMaxLength(64);
        builder.Property(t => t.UserAgent).HasMaxLength(512);
        builder.Property(t => t.RevokedReason).HasMaxLength(200);
        builder.HasOne(t => t.User).WithMany(u => u.RefreshTokens).HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> builder)
    {
        builder.Property(u => u.FirstName).HasMaxLength(100);
        builder.Property(u => u.LastName).HasMaxLength(100);
        builder.Property(u => u.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
    }
}

public sealed class AppRoleConfiguration : IEntityTypeConfiguration<AppRole>
{
    public void Configure(EntityTypeBuilder<AppRole> builder)
        => builder.Property(r => r.Description).HasMaxLength(250);
}
```

> ⚠️ **No usar `HasDefaultValue(true)` en propiedades `bool`.** EF no envía el valor `false` (es el default de C#), así que la BD guardaría `true`. Para que un bool arranque en `true`, usar el inicializador de C# (`= true`).

### 5.3 Interceptor de auditoría y soft delete — `Infrastructure/Persistence/Interceptors/AuditableEntityInterceptor.cs`
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using {Project}.Application.Abstractions.Services;
using {Project}.Domain.Common;

namespace {Project}.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Llena la auditoría y convierte borrados en soft delete en CADA SaveChanges.
/// Así ningún servicio asigna CreatedBy/UpdatedBy a mano.
/// </summary>
public sealed class AuditableEntityInterceptor(ICurrentUserService currentUser, TimeProvider clock) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Apply(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Apply(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Apply(DbContext? context)
    {
        if (context is null) return;

        var now = clock.GetUtcNow().UtcDateTime;
        var userId = currentUser.UserId;

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.Entity is ISoftDelete softDelete && entry.State == EntityState.Deleted)
            {
                entry.State = EntityState.Modified;
                softDelete.IsDeleted = true;
                softDelete.DeletedAt = now;
                softDelete.DeletedBy = userId;
            }

            if (entry.Entity is not IAuditable auditable) continue;

            switch (entry.State)
            {
                case EntityState.Added:
                    auditable.CreatedAt = now;
                    auditable.CreatedBy = userId;
                    break;

                case EntityState.Modified:
                    auditable.UpdatedAt = now;
                    auditable.UpdatedBy = userId;
                    // Blindaje: los datos de creación nunca se sobrescriben en un update.
                    entry.Property(nameof(IAuditable.CreatedAt)).IsModified = false;
                    entry.Property(nameof(IAuditable.CreatedBy)).IsModified = false;
                    break;
            }
        }
    }
}
```

### 5.4 Evaluador de especificaciones — `Infrastructure/Persistence/Repositories/SpecificationEvaluator.cs`
```csharp
using Microsoft.EntityFrameworkCore;
using {Project}.Application.Common.Specifications;

namespace {Project}.Infrastructure.Persistence.Repositories;

/// <summary>Traduce una ISpecification a IQueryable en el orden correcto: Where → Include → OrderBy → Skip → Take.</summary>
internal static class SpecificationEvaluator
{
    public static IQueryable<T> GetQuery<T>(IQueryable<T> source, ISpecification<T> spec, bool applyPaging = true) where T : class
    {
        var query = source;

        if (spec.IgnoreQueryFilters) query = query.IgnoreQueryFilters();
        if (spec.AsNoTracking) query = query.AsNoTracking();
        if (spec.Criteria is not null) query = query.Where(spec.Criteria);

        query = spec.Includes.Aggregate(query, (current, include) => current.Include(include));
        query = spec.IncludeStrings.Aggregate(query, (current, include) => current.Include(include));

        if (spec.AsSplitQuery) query = query.AsSplitQuery();

        if (spec.OrderExpressions.Count > 0)
        {
            var (firstKey, firstDesc) = spec.OrderExpressions[0];
            var ordered = firstDesc ? query.OrderByDescending(firstKey) : query.OrderBy(firstKey);

            foreach (var (key, desc) in spec.OrderExpressions.Skip(1))
                ordered = desc ? ordered.ThenByDescending(key) : ordered.ThenBy(key);

            query = ordered;
        }

        if (applyPaging && (spec.Skip.HasValue || spec.Take.HasValue))
        {
            if (spec.OrderExpressions.Count == 0)
                throw new InvalidOperationException($"La especificación {spec.GetType().Name} pagina sin OrderBy; el orden no sería determinista.");

            if (spec.Skip.HasValue) query = query.Skip(spec.Skip.Value);
            if (spec.Take.HasValue) query = query.Take(spec.Take.Value);
        }

        return query;
    }
}
```

### 5.5 Repositorio genérico — `Infrastructure/Persistence/Repositories/Repository.cs`
```csharp
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using {Project}.Application.Abstractions.Persistence;
using {Project}.Application.Common.Paging;
using {Project}.Application.Common.Specifications;

namespace {Project}.Infrastructure.Persistence.Repositories;

public class Repository<T>(AppDbContext context) : IRepository<T> where T : class
{
    protected readonly DbSet<T> Set = context.Set<T>();

    public ValueTask<T?> GetByIdAsync(object id, CancellationToken ct = default)
        => Set.FindAsync([id], ct);

    public Task<T?> FirstOrDefaultAsync(ISpecification<T> spec, CancellationToken ct = default)
        => Apply(spec).FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<T>> ListAsync(ISpecification<T> spec, CancellationToken ct = default)
        => await Apply(spec).ToListAsync(ct);

    public Task<TResult?> FirstOrDefaultAsync<TResult>(ISpecification<T> spec, Expression<Func<T, TResult>> selector, CancellationToken ct = default)
        => Apply(spec).Select(selector).FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<TResult>> ListAsync<TResult>(ISpecification<T> spec, Expression<Func<T, TResult>> selector, CancellationToken ct = default)
        => await Apply(spec).Select(selector).ToListAsync(ct);

    public async Task<PagedResult<TResult>> PagedListAsync<TResult>(ISpecification<T> spec, Expression<Func<T, TResult>> selector,
        PaginationParams paging, CancellationToken ct = default)
    {
        var total = await Apply(spec, applyPaging: false).CountAsync(ct);
        var items = await Apply(spec).Select(selector).ToListAsync(ct);
        return new PagedResult<TResult>(items, paging.PageNumber, paging.PageSize, total);
    }

    public Task<int> CountAsync(ISpecification<T>? spec = null, CancellationToken ct = default)
        => spec is null ? Set.CountAsync(ct) : Apply(spec, applyPaging: false).CountAsync(ct);

    public Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
        => Set.AnyAsync(predicate, ct);

    public void Add(T entity) => Set.Add(entity);
    public void AddRange(IEnumerable<T> entities) => Set.AddRange(entities);
    public void Update(T entity) => Set.Update(entity);   // solo para entidades desconectadas
    public void Remove(T entity) => Set.Remove(entity);
    public void RemoveRange(IEnumerable<T> entities) => Set.RemoveRange(entities);

    public Task<int> ExecuteUpdateAsync(Expression<Func<T, bool>> predicate, Action<UpdateSettersBuilder<T>> setters, CancellationToken ct = default)
        => Set.Where(predicate).ExecuteUpdateAsync(setters, ct);

    public Task<int> ExecuteDeleteAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
        => Set.Where(predicate).ExecuteDeleteAsync(ct);

    private IQueryable<T> Apply(ISpecification<T> spec, bool applyPaging = true)
        => SpecificationEvaluator.GetQuery(Set.AsQueryable(), spec, applyPaging);
}
```

> `ExecuteDeleteAsync` hace un **borrado físico** que salta el soft delete. Usarlo solo en tablas técnicas (por ejemplo, limpieza de `RefreshTokens` expirados).

### 5.6 Unit of Work — `Infrastructure/Persistence/UnitOfWork.cs`
```csharp
using Microsoft.EntityFrameworkCore;
using {Project}.Application.Abstractions.Persistence;
using {Project}.Application.Common.Results;
using {Project}.Infrastructure.Persistence.Repositories;

namespace {Project}.Infrastructure.Persistence;

/// <summary>
/// Scoped. NO implementa IDisposable: el DbContext pertenece al contenedor DI.
/// </summary>
public sealed class UnitOfWork(AppDbContext context) : IUnitOfWork
{
    private readonly Dictionary<Type, object> _repositories = [];

    public IRepository<T> Repository<T>() where T : class
    {
        if (!_repositories.TryGetValue(typeof(T), out var repository))
        {
            repository = new Repository<T>(context);
            _repositories[typeof(T)] = repository;
        }
        return (IRepository<T>)repository;
    }

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => context.SaveChangesAsync(ct);

    public async Task<Result<T>> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<Result<T>>> operation, CancellationToken ct = default)
    {
        // La estrategia de ejecución (EnableRetryOnFailure) exige envolver transacciones explícitas.
        var strategy = context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            // Si ya hay una transacción abierta (llamada anidada), reutilizarla.
            if (context.Database.CurrentTransaction is not null)
                return await operation(ct);

            await using var transaction = await context.Database.BeginTransactionAsync(ct);

            var result = await operation(ct);
            if (result.IsFailure)
            {
                await transaction.RollbackAsync(ct);
                context.ChangeTracker.Clear();   // descartar cambios pendientes del intento fallido
                return result;
            }

            await context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return result;
            // Si hay excepción, el using hace Dispose sin Commit = Rollback.
        });
    }

    public async Task<Result> ExecuteInTransactionAsync(Func<CancellationToken, Task<Result>> operation, CancellationToken ct = default)
    {
        var result = await ExecuteInTransactionAsync<bool>(async token =>
        {
            var inner = await operation(token);
            return inner.IsSuccess ? Result.Success(true) : Result.Failure<bool>(inner.Error);
        }, ct);

        return result.IsSuccess ? Result.Success() : Result.Failure(result.Error);
    }
}
```

**Por qué funciona:** `IUnitOfWork` es *Scoped*. En un request, todos los services reciben la misma instancia y, por tanto, el **mismo `AppDbContext`**. `UserManager`/`RoleManager` de Identity también usan ese contexto, así que una transacción abierta con `ExecuteInTransactionAsync` cubre también las operaciones de Identity.

### 5.7 Stored procedures y SQL crudo (siempre parametrizado)
```csharp
// Resultado sin entidad (EF Core 8+): tipo DTO plano.
var rows = await context.Database
    .SqlQuery<{Report}Row>($"EXEC dbo.{StoredProcedure} @From = {from}, @To = {to}")
    .ToListAsync(ct);
```
Los valores interpolados se convierten en parámetros SQL. **Nunca** armar el SQL con `+` ni con `string.Format`. Si un service necesita esto, se expone mediante un método en un repositorio específico (`I{Report}Queries`) implementado en Infrastructure.

---

## 6. Capa API

### 6.1 Controller base — `Api/Controllers/ApiControllerBase.cs`
```csharp
using Microsoft.AspNetCore.Mvc;
using {Project}.Application.Common.Results;

namespace {Project}.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase
{
    protected IActionResult HandleResult<T>(Result<T> result)
        => result.IsSuccess ? Ok(result.Value) : ToProblem(result.Error);

    protected IActionResult HandleResult(Result result)
        => result.IsSuccess ? NoContent() : ToProblem(result.Error);

    protected IActionResult HandleCreated<T>(Result<T> result, string actionName, Func<T, object> routeValues)
        => result.IsSuccess ? CreatedAtAction(actionName, routeValues(result.Value), result.Value) : ToProblem(result.Error);

    /// <summary>Convierte un Error de negocio en ProblemDetails (RFC 9457) con el código HTTP correcto.</summary>
    protected IActionResult ToProblem(Error error)
    {
        var status = error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status400BadRequest
        };

        ProblemDetails problem = error.Details is { Count: > 0 }
            ? new ValidationProblemDetails(error.Details.ToDictionary(d => d.Key, d => d.Value))
            : new ProblemDetails();

        problem.Status = status;
        problem.Title = error.Message;
        problem.Extensions["code"] = error.Code;
        problem.Extensions["traceId"] = HttpContext.TraceIdentifier;

        return new ObjectResult(problem) { StatusCode = status };
    }
}
```

| `ErrorType` | HTTP |
|---|---|
| (éxito con valor) | 200 OK / 201 Created |
| (éxito sin valor) | 204 No Content |
| `Validation` | 400 |
| `Unauthorized` | 401 |
| `Forbidden` | 403 |
| `NotFound` | 404 |
| `Conflict` | 409 |
| `Failure` | 400 |
| Excepción no controlada | 500 (handler global) |

### 6.2 Validación automática — `Api/Filters/ValidationFilter.cs`
```csharp
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace {Project}.Api.Filters;

/// <summary>Ejecuta el IValidator&lt;T&gt; registrado para cada argumento de la acción; si falla responde 400.</summary>
public sealed class ValidationFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var services = context.HttpContext.RequestServices;

        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null) continue;

            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
            if (services.GetService(validatorType) is not IValidator validator) continue;

            var result = await validator.ValidateAsync(new ValidationContext<object>(argument), context.HttpContext.RequestAborted);
            if (result.IsValid) continue;

            foreach (var error in result.Errors)
                context.ModelState.AddModelError(error.PropertyName, error.ErrorMessage);

            var factory = services.GetRequiredService<ProblemDetailsFactory>();
            var problem = factory.CreateValidationProblemDetails(context.HttpContext, context.ModelState, StatusCodes.Status400BadRequest);
            context.Result = new ObjectResult(problem) { StatusCode = StatusCodes.Status400BadRequest };
            return;
        }

        await next();
    }
}
```

### 6.3 Manejo global de excepciones — `Api/Errors/GlobalExceptionHandler.cs`
```csharp
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace {Project}.Api.Errors;

public sealed class GlobalExceptionHandler(IProblemDetailsService problemDetails, ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken ct)
    {
        var (status, title) = exception switch
        {
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "El registro fue modificado por otro usuario. Recargue e intente de nuevo."),
            OperationCanceledException => (499, "Solicitud cancelada por el cliente."),
            _ => (StatusCodes.Status500InternalServerError, "Ocurrió un error interno.")
        };

        if (status == StatusCodes.Status500InternalServerError)
            logger.LogError(exception, "Excepción no controlada en {Path}", httpContext.Request.Path);
        else
            logger.LogWarning(exception, "Excepción controlada ({Status}) en {Path}", status, httpContext.Request.Path);

        httpContext.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails { Status = status, Title = title }   // nunca exponer exception.Message al cliente
        });
    }
}
```

### 6.4 OpenAPI con esquema Bearer — `Api/OpenApi/BearerSecuritySchemeTransformer.cs`
```csharp
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace {Project}.Api.OpenApi;

/// <summary>Agrega el esquema JWT Bearer al documento para probar endpoints protegidos desde Scalar.</summary>
internal sealed class BearerSecuritySchemeTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken ct)
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Access token JWT obtenido en /api/auth/login"
        };

        document.Security ??= [];
        document.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", document)] = []
        });

        return Task.CompletedTask;
    }
}
```
> .NET 10 usa **Microsoft.OpenApi 2.x** (namespace `Microsoft.OpenApi`, sin `.Models`). Si el compilador marca diferencias de API, ajustar a la versión instalada.

---

## 7. Autorización por permisos

### 7.1 Catálogo — `Application/Common/Security/Permissions.cs`
```csharp
using System.Reflection;

namespace {Project}.Application.Common.Security;

public static class AppRoles
{
    public const string Admin = "Admin";
    public const string User = "User";

    /// <summary>Roles de sistema: no se pueden renombrar ni eliminar.</summary>
    public static readonly IReadOnlyList<string> System = [Admin, User];
}

/// <summary>
/// Catálogo de permisos. Se guardan como claims "permission" en el ROL (AspNetRoleClaims)
/// y viajan en el access token. Agregar una clase anidada por cada entidad de negocio.
/// </summary>
public static class Permissions
{
    public const string ClaimType = "permission";

    // ---- Fijos (Auth) ----
    public static class Users
    {
        public const string Read = "users.read";
        public const string Manage = "users.manage";
    }

    public static class Roles
    {
        public const string Read = "roles.read";
        public const string Manage = "roles.manage";
    }

    // ---- Plantilla por entidad de negocio (sección 9) ----
    // public static class {Entities}
    // {
    //     public const string Read = "{entities}.read";
    //     public const string Write = "{entities}.write";
    //     public const string Delete = "{entities}.delete";
    // }

    /// <summary>Todos los permisos declarados arriba (por reflexión).</summary>
    public static IReadOnlyList<string> All { get; } = typeof(Permissions)
        .GetNestedTypes(BindingFlags.Public | BindingFlags.Static)
        .SelectMany(t => t.GetFields(BindingFlags.Public | BindingFlags.Static))
        .Where(f => f.IsLiteral && f.FieldType == typeof(string))
        .Select(f => (string)f.GetRawConstantValue()!)
        .ToList();

    /// <summary>Permisos iniciales del rol User (Admin pasa todas las políticas). Completar según el dominio.</summary>
    public static readonly IReadOnlyList<string> DefaultUserPermissions = [ /* {Entities}.Read, ... */ ];
}
```

### 7.2 Atributo y proveedor de políticas — `Api/Authorization/`
```csharp
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using {Project}.Application.Common.Security;

namespace {Project}.Api.Authorization;

/// <summary>[HasPermission(Permissions.{Entities}.Read)] → política "permission:{entities}.read".</summary>
public sealed class HasPermissionAttribute(string permission) : AuthorizeAttribute(PolicyPrefix + permission)
{
    public const string PolicyPrefix = "permission:";
}

/// <summary>Crea las políticas de permiso al vuelo: no hay que registrar una política por permiso.</summary>
public sealed class PermissionPolicyProvider(IOptions<AuthorizationOptions> options) : DefaultAuthorizationPolicyProvider(options)
{
    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (!policyName.StartsWith(HasPermissionAttribute.PolicyPrefix, StringComparison.OrdinalIgnoreCase))
            return await base.GetPolicyAsync(policyName);

        var permission = policyName[HasPermissionAttribute.PolicyPrefix.Length..];

        return new AuthorizationPolicyBuilder(JwtBearerDefaults.AuthenticationScheme)
            .RequireAuthenticatedUser()
            .RequireAssertion(ctx => ctx.User.IsInRole(AppRoles.Admin)                 // Admin pasa todo
                                  || ctx.User.HasClaim(Permissions.ClaimType, permission))
            .Build();
    }
}
```

**Cómo fluye:**
1. Un permiso es un claim `permission` asociado a un **rol** (tabla `AspNetRoleClaims`).
2. Al hacer login o refresh se leen los roles del usuario y los permisos de esos roles, y se meten en el access token.
3. `[HasPermission(...)]` valida el claim.
4. Los cambios de roles o permisos aplican en el siguiente refresh (máximo `AccessTokenMinutes`). Para efecto inmediato, revocar las sesiones del usuario.

---

## 8. Autenticación con Identity local + JWT

### 8.1 Resumen de endpoints

| Método | Ruta | Auth | Descripción |
|---|---|---|---|
| POST | `/api/auth/register` | Anónimo | Crea usuario (rol `User`) y envía confirmación por email. No revela si el email ya existe. |
| POST | `/api/auth/confirm-email` | Anónimo | Confirma email con `userId` + `token`. |
| POST | `/api/auth/resend-confirmation` | Anónimo | Reenvía confirmación (respuesta siempre 204). |
| POST | `/api/auth/login` | Anónimo | Valida credenciales y lockout. Devuelve tokens o `requiresTwoFactor`. |
| POST | `/api/auth/login/2fa` | Anónimo | Completa login con código TOTP o código de recuperación. |
| POST | `/api/auth/refresh` | Anónimo | Rota el refresh token y devuelve un par nuevo. |
| POST | `/api/auth/logout` | Anónimo | Revoca la sesión del refresh token enviado. |
| POST | `/api/auth/forgot-password` | Anónimo | Envía enlace de reset (respuesta siempre 204). |
| POST | `/api/auth/reset-password` | Anónimo | Define nueva contraseña con el token y revoca todas las sesiones. |
| POST | `/api/auth/confirm-email-change` | Anónimo | Confirma el cambio de email (enlace enviado al email nuevo). |
| GET / PUT | `/api/account/me` | Usuario | Ver o editar perfil. |
| POST | `/api/account/change-password` | Usuario | Cambia contraseña, revoca sesiones y devuelve tokens nuevos. |
| POST | `/api/account/change-email` | Usuario | Solicita cambio de email (requiere contraseña). |
| GET | `/api/account/sessions` | Usuario | Lista sesiones o dispositivos activos. |
| DELETE | `/api/account/sessions/{id}` | Usuario | Revoca una sesión. |
| POST | `/api/account/logout-all` | Usuario | Revoca todas las sesiones. |
| GET | `/api/account/2fa` | Usuario | Estado de 2FA. |
| POST | `/api/account/2fa/setup` | Usuario | Genera clave y URI `otpauth://` para el QR. |
| POST | `/api/account/2fa/enable` | Usuario | Verifica código, activa 2FA y devuelve códigos de recuperación. |
| POST | `/api/account/2fa/disable` | Usuario | Desactiva 2FA (requiere contraseña). |
| POST | `/api/account/2fa/recovery-codes` | Usuario | Regenera códigos de recuperación. |
| GET | `/api/users` | `users.read` | Listado paginado con búsqueda, filtro por rol y estado. |
| GET | `/api/users/{id}` | `users.read` | Detalle. |
| POST | `/api/users` | `users.manage` | Crea usuario y le envía enlace para definir contraseña. |
| POST | `/api/users/{id}/lock` · `/unlock` | `users.manage` | Bloquea o desbloquea. |
| POST | `/api/users/{id}/activate` · `/deactivate` | `users.manage` | Activa o desactiva la cuenta. |
| PUT | `/api/users/{id}/roles` | `users.manage` | Reemplaza los roles. |
| POST | `/api/users/{id}/send-password-reset` | `users.manage` | Envía enlace de reset. |
| POST | `/api/users/{id}/revoke-sessions` | `users.manage` | Cierra todas sus sesiones. |
| GET | `/api/roles` · `/api/roles/{id}` · `/api/roles/permissions` | `roles.read` | Roles, detalle y catálogo de permisos. |
| POST / PUT / DELETE | `/api/roles` · `/api/roles/{id}` | `roles.manage` | CRUD de roles (los de sistema están protegidos). |
| PUT | `/api/roles/{id}/permissions` | `roles.manage` | Reemplaza los permisos del rol. |

**Flujo de tokens:**
- **Access token**: JWT firmado HS256, duración 15 min. Lleva los claims `sub`, `email`, `name`, `jti`, `role` (varios) y `permission` (varios).
- **Refresh token**: 64 bytes aleatorios, duración 7 días. En BD se guarda **solo su SHA-256**.
- **Rotación**: cada refresh revoca el token usado y crea uno nuevo de la misma *familia* (sesión).
- **Detección de reuso**: si llega un token ya rotado, se revoca la familia entera (posible robo).
- **2FA**: el login devuelve un JWT intermedio de 5 min con audiencia `{Audience}:2fa`. Ese token no sirve como access token y solo se canjea en `/login/2fa`.

### 8.2 Entidades de Identity — `Infrastructure/Identity/IdentityEntities.cs`
```csharp
using Microsoft.AspNetCore.Identity;

namespace {Project}.Infrastructure.Identity;

public class AppUser : IdentityUser<Guid>
{
    public AppUser() => Id = Guid.CreateVersion7();

    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public bool IsActive { get; set; } = true;          // desactivación administrativa (distinta del lockout)
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }

    public ICollection<RefreshToken> RefreshTokens { get; } = new List<RefreshToken>();

    public string FullName => $"{FirstName} {LastName}".Trim();
}

public class AppRole : IdentityRole<Guid>
{
    public AppRole() => Id = Guid.CreateVersion7();
    public AppRole(string name) : this() => Name = name;

    public string? Description { get; set; }
}

/// <summary>
/// Refresh token persistido. Solo se guarda el HASH (SHA-256), nunca el valor.
/// FamilyId agrupa la cadena de rotaciones de una misma sesión/dispositivo.
/// </summary>
public class RefreshToken
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;

    public string TokenHash { get; set; } = string.Empty;
    public Guid FamilyId { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? RevokedReason { get; set; }
    public string? ReplacedByTokenHash { get; set; }

    public string? CreatedByIp { get; set; }
    public string? UserAgent { get; set; }

    public bool IsActive(DateTime utcNow) => RevokedAt is null && ExpiresAt > utcNow;
}
```

### 8.3 Opciones — `Infrastructure/Identity/Options.cs`
```csharp
using System.ComponentModel.DataAnnotations;

namespace {Project}.Infrastructure.Identity;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required] public string Issuer { get; init; } = string.Empty;
    [Required] public string Audience { get; init; } = string.Empty;

    /// <summary>Mínimo 32 caracteres. Va en user-secrets / variables de entorno / Key Vault, NUNCA en appsettings.json.</summary>
    [Required, MinLength(32)] public string SigningKey { get; init; } = string.Empty;

    [Range(1, 120)] public int AccessTokenMinutes { get; init; } = 15;
    [Range(1, 90)] public int RefreshTokenDays { get; init; } = 7;
    [Range(1, 15)] public int TwoFactorChallengeMinutes { get; init; } = 5;

    /// <summary>Audiencia exclusiva del token intermedio de 2FA (no sirve como access token).</summary>
    public string TwoFactorAudience => $"{Audience}:2fa";
}

public sealed class AppUrlOptions
{
    public const string SectionName = "App";

    /// <summary>URL del frontend; los enlaces de los emails apuntan aquí.</summary>
    [Required, Url] public string ClientUrl { get; init; } = string.Empty;

    /// <summary>Nombre mostrado en apps autenticadoras (Google/Microsoft Authenticator).</summary>
    [Required] public string AppName { get; init; } = "{Project}";
}

public sealed class SeedOptions
{
    public const string SectionName = "Seed";

    public bool ApplyMigrationsOnStartup { get; init; }
    public string? AdminEmail { get; init; }
    public string? AdminPassword { get; init; }   // user-secrets
}
```

### 8.4 `TokenService` — `Infrastructure/Identity/TokenService.cs`
```csharp
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using {Project}.Application.Common.Security;

namespace {Project}.Infrastructure.Identity;

public interface ITokenService
{
    (string Token, DateTime ExpiresAt) CreateAccessToken(AppUser user, IEnumerable<string> roles, IEnumerable<string> permissions);
    string CreateTwoFactorChallengeToken(Guid userId);
    Task<Guid?> ValidateTwoFactorChallengeTokenAsync(string token);
    string GenerateRefreshToken();
    string HashToken(string rawToken);
}

public sealed class TokenService(IOptions<JwtOptions> options, TimeProvider clock) : ITokenService
{
    private readonly JwtOptions _jwt = options.Value;
    private readonly JsonWebTokenHandler _handler = new();

    private SymmetricSecurityKey SigningKey => new(Encoding.UTF8.GetBytes(_jwt.SigningKey));

    public (string Token, DateTime ExpiresAt) CreateAccessToken(AppUser user, IEnumerable<string> roles, IEnumerable<string> permissions)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var expires = now.AddMinutes(_jwt.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Name, user.FullName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimNames.Role, r)));
        claims.AddRange(permissions.Distinct().Select(p => new Claim(Permissions.ClaimType, p)));

        var token = _handler.CreateToken(new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = _jwt.Issuer,
            Audience = _jwt.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = expires,
            SigningCredentials = new SigningCredentials(SigningKey, SecurityAlgorithms.HmacSha256)
        });

        return (token, expires);
    }

    public string CreateTwoFactorChallengeToken(Guid userId)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        return _handler.CreateToken(new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity([new Claim(JwtRegisteredClaimNames.Sub, userId.ToString())]),
            Issuer = _jwt.Issuer,
            Audience = _jwt.TwoFactorAudience,      // audiencia distinta: el JwtBearer de la API lo rechaza
            IssuedAt = now,
            NotBefore = now,
            Expires = now.AddMinutes(_jwt.TwoFactorChallengeMinutes),
            SigningCredentials = new SigningCredentials(SigningKey, SecurityAlgorithms.HmacSha256)
        });
    }

    public async Task<Guid?> ValidateTwoFactorChallengeTokenAsync(string token)
    {
        var result = await _handler.ValidateTokenAsync(token, new TokenValidationParameters
        {
            ValidIssuer = _jwt.Issuer,
            ValidAudience = _jwt.TwoFactorAudience,
            IssuerSigningKey = SigningKey,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        });

        if (!result.IsValid || !result.Claims.TryGetValue(JwtRegisteredClaimNames.Sub, out var sub))
            return null;

        return Guid.TryParse(sub?.ToString(), out var userId) ? userId : null;
    }

    public string GenerateRefreshToken() => Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(64));

    public string HashToken(string rawToken) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
}

/// <summary>Nombres de claims usados en toda la app (MapInboundClaims = false: se usan tal cual).</summary>
public static class ClaimNames
{
    public const string Subject = JwtRegisteredClaimNames.Sub;
    public const string Email = JwtRegisteredClaimNames.Email;
    public const string Name = JwtRegisteredClaimNames.Name;
    public const string Role = "role";
}
```

### 8.5 Usuario actual — `Infrastructure/Identity/CurrentUserService.cs`
```csharp
using {Project}.Application.Abstractions.Services;
using {Project}.Application.Common.Security;

namespace {Project}.Infrastructure.Identity;

public sealed class CurrentUserService(IHttpContextAccessor accessor) : ICurrentUserService
{
    private HttpContext? Context => accessor.HttpContext;

    public Guid? UserId => Guid.TryParse(Context?.User.FindFirst(ClaimNames.Subject)?.Value, out var id) ? id : null;
    public string? Email => Context?.User.FindFirst(ClaimNames.Email)?.Value;
    public bool IsAuthenticated => Context?.User.Identity?.IsAuthenticated ?? false;
    public bool IsInRole(string role) => Context?.User.IsInRole(role) ?? false;
    public bool HasPermission(string permission)
        => IsInRole(AppRoles.Admin) || (Context?.User.HasClaim(Permissions.ClaimType, permission) ?? false);
    public string? IpAddress => Context?.Connection.RemoteIpAddress?.ToString();
    public string? UserAgent => Context?.Request.Headers.UserAgent.ToString();
}
```

### 8.6 Contratos de Auth — `Application/Features/Auth/AuthContracts.cs`
```csharp
using {Project}.Application.Common.Paging;
using {Project}.Application.Common.Results;

namespace {Project}.Application.Features.Auth;

// =====================  DTOs: autenticación (anónimos)  =====================

public sealed record RegisterRequest(string Email, string Password, string? FirstName, string? LastName);
public sealed record ConfirmEmailRequest(Guid UserId, string Token);
public sealed record EmailRequest(string Email);
public sealed record LoginRequest(string Email, string Password);
public sealed record TwoFactorLoginRequest(string TwoFactorToken, string Code, bool IsRecoveryCode = false);
public sealed record RefreshTokenRequest(string RefreshToken);
public sealed record ResetPasswordRequest(string Email, string Token, string NewPassword);
public sealed record ConfirmEmailChangeRequest(Guid UserId, string NewEmail, string Token);

public sealed record AuthTokens(
    string AccessToken,
    DateTime AccessTokenExpiresAt,
    string RefreshToken,
    DateTime RefreshTokenExpiresAt,
    string TokenType = "Bearer");

/// <summary>Si RequiresTwoFactor = true, el cliente llama a /auth/login/2fa con TwoFactorToken + código.</summary>
public sealed record LoginResponse(bool RequiresTwoFactor, string? TwoFactorToken, AuthTokens? Tokens)
{
    public static LoginResponse TwoFactorRequired(string token) => new(true, token, null);
    public static LoginResponse Authenticated(AuthTokens tokens) => new(false, null, tokens);
}

// =====================  DTOs: cuenta del usuario autenticado  =====================

public sealed record UserProfileDto(
    Guid Id, string Email, string? FirstName, string? LastName, string? PhoneNumber,
    bool EmailConfirmed, bool TwoFactorEnabled, IReadOnlyList<string> Roles, IReadOnlyList<string> Permissions);

public sealed record UpdateProfileRequest(string? FirstName, string? LastName, string? PhoneNumber);
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public sealed record ChangeEmailRequest(string NewEmail, string CurrentPassword);
public sealed record SessionDto(Guid Id, DateTime LastActivityAt, DateTime ExpiresAt, string? IpAddress, string? UserAgent);
public sealed record TwoFactorStatusDto(bool IsEnabled, bool HasAuthenticator, int RecoveryCodesLeft);
public sealed record TwoFactorSetupDto(string SharedKey, string AuthenticatorUri);
public sealed record TwoFactorCodeRequest(string Code);
public sealed record DisableTwoFactorRequest(string Password);
public sealed record RecoveryCodesDto(IReadOnlyList<string> RecoveryCodes);

// =====================  DTOs: administración  =====================

public sealed class UserFilter : PaginationParams
{
    public string? Search { get; init; }
    public bool? IsActive { get; init; }
    public string? Role { get; init; }
}

public sealed record UserSummaryDto(Guid Id, string Email, string FullName, bool IsActive, bool EmailConfirmed,
    bool IsLockedOut, IReadOnlyList<string> Roles);

public sealed record UserDetailDto(Guid Id, string Email, string? FirstName, string? LastName, string? PhoneNumber,
    bool IsActive, bool EmailConfirmed, bool TwoFactorEnabled, DateTimeOffset? LockoutEnd, int AccessFailedCount,
    DateTime CreatedAt, DateTime? LastLoginAt, IReadOnlyList<string> Roles);

public sealed record CreateUserRequest(string Email, string? FirstName, string? LastName, IReadOnlyList<string> Roles);
public sealed record LockUserRequest(DateTimeOffset? Until);
public sealed record SetRolesRequest(IReadOnlyList<string> Roles);

public sealed record RoleDto(Guid Id, string Name, string? Description, int UserCount, bool IsSystem);
public sealed record RoleDetailDto(Guid Id, string Name, string? Description, bool IsSystem, IReadOnlyList<string> Permissions);
public sealed record CreateRoleRequest(string Name, string? Description, IReadOnlyList<string> Permissions);
public sealed record UpdateRoleRequest(string Name, string? Description);
public sealed record SetPermissionsRequest(IReadOnlyList<string> Permissions);

// =====================  Errores  =====================

public static class AuthErrors
{
    public static readonly Error InvalidCredentials = Error.Unauthorized("Auth.InvalidCredentials", "Email o contraseña incorrectos.");
    public static readonly Error LockedOut = Error.Forbidden("Auth.LockedOut", "Cuenta bloqueada temporalmente. Intente más tarde.");
    public static readonly Error AccountDisabled = Error.Forbidden("Auth.AccountDisabled", "La cuenta está desactivada.");
    public static readonly Error EmailNotConfirmed = Error.Forbidden("Auth.EmailNotConfirmed", "Debe confirmar su email antes de iniciar sesión.");
    public static readonly Error InvalidTwoFactorToken = Error.Unauthorized("Auth.InvalidTwoFactorToken", "La sesión de verificación expiró. Inicie sesión de nuevo.");
    public static readonly Error InvalidTwoFactorCode = Error.Unauthorized("Auth.InvalidTwoFactorCode", "Código de verificación inválido.");
    public static readonly Error InvalidRefreshToken = Error.Unauthorized("Auth.InvalidRefreshToken", "Refresh token inválido o expirado.");
    public static readonly Error InvalidToken = Error.Validation("Auth.InvalidToken", "El enlace es inválido o expiró.");
    public static readonly Error InvalidPassword = Error.Validation("Auth.InvalidPassword", "La contraseña actual es incorrecta.");
    public static readonly Error EmailInUse = Error.Conflict("Auth.EmailInUse", "Ese email ya está en uso.");
    public static readonly Error TwoFactorNotEnabled = Error.Validation("Auth.TwoFactorNotEnabled", "La verificación en dos pasos no está activa.");
    public static readonly Error TwoFactorAlreadyEnabled = Error.Conflict("Auth.TwoFactorAlreadyEnabled", "La verificación en dos pasos ya está activa.");
    public static readonly Error AuthenticatorNotConfigured = Error.Validation("Auth.AuthenticatorNotConfigured", "Primero configure la app autenticadora (/2fa/setup).");
    public static readonly Error NotAuthenticated = Error.Unauthorized("Auth.NotAuthenticated", "No autenticado.");
    public static readonly Error SessionNotFound = Error.NotFound("Auth.SessionNotFound", "Sesión no encontrada.");
}

public static class UserAdminErrors
{
    public static Error NotFound(Guid id) => Error.NotFound("User.NotFound", $"No existe el usuario {id}.");
    public static Error RolesNotFound(IEnumerable<string> roles) => Error.Validation("User.RolesNotFound", $"Roles inexistentes: {string.Join(", ", roles)}.");
    public static readonly Error CannotModifySelf = Error.Forbidden("User.CannotModifySelf", "No puede aplicar esta acción sobre su propia cuenta.");
}

public static class RoleErrors
{
    public static Error NotFound(Guid id) => Error.NotFound("Role.NotFound", $"No existe el rol {id}.");
    public static Error NameInUse(string name) => Error.Conflict("Role.NameInUse", $"Ya existe el rol '{name}'.");
    public static Error UnknownPermissions(IEnumerable<string> p) => Error.Validation("Role.UnknownPermissions", $"Permisos inexistentes: {string.Join(", ", p)}.");
    public static readonly Error SystemRole = Error.Forbidden("Role.SystemRole", "Los roles de sistema no se pueden renombrar ni eliminar.");
    public static readonly Error HasUsers = Error.Conflict("Role.HasUsers", "El rol tiene usuarios asignados.");
}

// =====================  Servicios  =====================

public interface IAuthService
{
    Task<Result> RegisterAsync(RegisterRequest request, CancellationToken ct);
    Task<Result> ConfirmEmailAsync(ConfirmEmailRequest request, CancellationToken ct);
    Task<Result> ResendConfirmationAsync(EmailRequest request, CancellationToken ct);
    Task<Result<LoginResponse>> LoginAsync(LoginRequest request, CancellationToken ct);
    Task<Result<AuthTokens>> LoginWithTwoFactorAsync(TwoFactorLoginRequest request, CancellationToken ct);
    Task<Result<AuthTokens>> RefreshAsync(RefreshTokenRequest request, CancellationToken ct);
    Task<Result> LogoutAsync(RefreshTokenRequest request, CancellationToken ct);
    Task<Result> ForgotPasswordAsync(EmailRequest request, CancellationToken ct);
    Task<Result> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct);
    Task<Result> ConfirmEmailChangeAsync(ConfirmEmailChangeRequest request, CancellationToken ct);
}

/// <summary>Operaciones del usuario autenticado sobre su propia cuenta.</summary>
public interface IAccountService
{
    Task<Result<UserProfileDto>> GetProfileAsync(CancellationToken ct);
    Task<Result<UserProfileDto>> UpdateProfileAsync(UpdateProfileRequest request, CancellationToken ct);
    Task<Result<AuthTokens>> ChangePasswordAsync(ChangePasswordRequest request, CancellationToken ct);
    Task<Result> RequestEmailChangeAsync(ChangeEmailRequest request, CancellationToken ct);
    Task<Result<IReadOnlyList<SessionDto>>> GetSessionsAsync(CancellationToken ct);
    Task<Result> RevokeSessionAsync(Guid sessionId, CancellationToken ct);
    Task<Result> LogoutAllAsync(CancellationToken ct);
    Task<Result<TwoFactorStatusDto>> GetTwoFactorStatusAsync(CancellationToken ct);
    Task<Result<TwoFactorSetupDto>> SetupAuthenticatorAsync(CancellationToken ct);
    Task<Result<RecoveryCodesDto>> EnableTwoFactorAsync(TwoFactorCodeRequest request, CancellationToken ct);
    Task<Result> DisableTwoFactorAsync(DisableTwoFactorRequest request, CancellationToken ct);
    Task<Result<RecoveryCodesDto>> RegenerateRecoveryCodesAsync(CancellationToken ct);
}

public interface IUserAdminService
{
    Task<Result<PagedResult<UserSummaryDto>>> GetPagedAsync(UserFilter filter, CancellationToken ct);
    Task<Result<UserDetailDto>> GetByIdAsync(Guid id, CancellationToken ct);
    Task<Result<UserDetailDto>> CreateAsync(CreateUserRequest request, CancellationToken ct);
    Task<Result> LockAsync(Guid id, LockUserRequest request, CancellationToken ct);
    Task<Result> UnlockAsync(Guid id, CancellationToken ct);
    Task<Result> SetActiveAsync(Guid id, bool isActive, CancellationToken ct);
    Task<Result> SetRolesAsync(Guid id, SetRolesRequest request, CancellationToken ct);
    Task<Result> SendPasswordResetAsync(Guid id, CancellationToken ct);
    Task<Result> RevokeSessionsAsync(Guid id, CancellationToken ct);
}

public interface IRoleService
{
    Task<Result<IReadOnlyList<RoleDto>>> GetAllAsync(CancellationToken ct);
    Task<Result<RoleDetailDto>> GetByIdAsync(Guid id, CancellationToken ct);
    Task<Result<RoleDetailDto>> CreateAsync(CreateRoleRequest request, CancellationToken ct);
    Task<Result<RoleDetailDto>> UpdateAsync(Guid id, UpdateRoleRequest request, CancellationToken ct);
    Task<Result> DeleteAsync(Guid id, CancellationToken ct);
    Task<Result<RoleDetailDto>> SetPermissionsAsync(Guid id, SetPermissionsRequest request, CancellationToken ct);
    IReadOnlyList<string> GetAvailablePermissions();
}
```

### 8.7 Validadores de Auth — `Application/Features/Auth/AuthValidators.cs`
```csharp
using FluentValidation;

namespace {Project}.Application.Features.Auth;

// La complejidad de contraseña la aplica Identity (IdentityOptions.Password);
// aquí solo se valida forma y presencia para responder 400 antes de tocar BD.

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8).MaximumLength(128);
        RuleFor(x => x.FirstName).MaximumLength(100);
        RuleFor(x => x.LastName).MaximumLength(100);
    }
}

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
    }
}

public sealed class EmailRequestValidator : AbstractValidator<EmailRequest>
{
    public EmailRequestValidator() => RuleFor(x => x.Email).NotEmpty().EmailAddress();
}

public sealed class TwoFactorLoginRequestValidator : AbstractValidator<TwoFactorLoginRequest>
{
    public TwoFactorLoginRequestValidator()
    {
        RuleFor(x => x.TwoFactorToken).NotEmpty();
        RuleFor(x => x.Code).NotEmpty().MaximumLength(20);
    }
}

public sealed class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Token).NotEmpty();
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(8).MaximumLength(128);
    }
}

public sealed class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty();
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(8).MaximumLength(128)
            .NotEqual(x => x.CurrentPassword).WithMessage("La nueva contraseña debe ser distinta a la actual.");
    }
}

public sealed class ChangeEmailRequestValidator : AbstractValidator<ChangeEmailRequest>
{
    public ChangeEmailRequestValidator()
    {
        RuleFor(x => x.NewEmail).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.CurrentPassword).NotEmpty();
    }
}

public sealed class TwoFactorCodeRequestValidator : AbstractValidator<TwoFactorCodeRequest>
{
    public TwoFactorCodeRequestValidator() => RuleFor(x => x.Code).NotEmpty().MaximumLength(10);
}

public sealed class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Roles).NotNull();
    }
}

public sealed class CreateRoleRequestValidator : AbstractValidator<CreateRoleRequest>
{
    public CreateRoleRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(50).Matches("^[A-Za-z0-9_-]+$");
        RuleFor(x => x.Description).MaximumLength(250);
        RuleFor(x => x.Permissions).NotNull();
    }
}

public sealed class UpdateRoleRequestValidator : AbstractValidator<UpdateRoleRequest>
{
    public UpdateRoleRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(50).Matches("^[A-Za-z0-9_-]+$");
        RuleFor(x => x.Description).MaximumLength(250);
    }
}
```

### 8.8 Gestión de sesiones — `Infrastructure/Identity/SessionManager.cs`
```csharp
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using {Project}.Application.Abstractions.Persistence;
using {Project}.Application.Abstractions.Services;
using {Project}.Application.Common.Results;
using {Project}.Application.Common.Security;
using {Project}.Application.Common.Specifications;
using {Project}.Application.Features.Auth;
using {Project}.Infrastructure.Persistence;

namespace {Project}.Infrastructure.Identity;

internal sealed class RefreshTokenByHashSpec : Specification<RefreshToken>
{
    public RefreshTokenByHashSpec(string hash) : base(t => t.TokenHash == hash) => EnableTracking();
}

internal sealed class ActiveRefreshTokensByUserSpec : Specification<RefreshToken>
{
    public ActiveRefreshTokensByUserSpec(Guid userId, DateTime now)
        : base(t => t.UserId == userId && t.RevokedAt == null && t.ExpiresAt > now)
        => OrderByDescending(t => t.CreatedAt);
}

/// <summary>
/// Emisión de tokens y ciclo de vida de sesiones (refresh tokens con rotación y detección de reuso).
/// Usa IUnitOfWork para RefreshToken y AppDbContext solo para LEER permisos de las tablas de Identity.
/// </summary>
public sealed class SessionManager(
    IUnitOfWork uow,
    AppDbContext db,
    UserManager<AppUser> userManager,
    ITokenService tokenService,
    ICurrentUserService currentUser,
    IOptions<JwtOptions> jwtOptions,
    TimeProvider clock)
{
    private readonly IRepository<RefreshToken> _tokens = uow.Repository<RefreshToken>();
    private readonly JwtOptions _jwt = jwtOptions.Value;

    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    /// <summary>Crea access + refresh token. familyId null = nueva sesión.</summary>
    public async Task<AuthTokens> IssueTokensAsync(AppUser user, Guid? familyId, CancellationToken ct)
    {
        var (accessToken, accessExpires, refresh) = await CreateTokenPairAsync(user, familyId ?? Guid.CreateVersion7(), ct);
        await uow.SaveChangesAsync(ct);
        return new AuthTokens(accessToken, accessExpires, refresh.Raw, refresh.Entity.ExpiresAt);
    }

    public async Task<Result<AuthTokens>> RotateAsync(string rawToken, CancellationToken ct)
    {
        var stored = await _tokens.FirstOrDefaultAsync(new RefreshTokenByHashSpec(tokenService.HashToken(rawToken)), ct);
        if (stored is null)
            return AuthErrors.InvalidRefreshToken;

        if (stored.RevokedAt is not null)
        {
            // Un token ya rotado se volvió a usar: posible robo. Se revoca toda la familia (sesión).
            if (stored.ReplacedByTokenHash is not null)
                await RevokeFamilyAsync(stored.UserId, stored.FamilyId, "Reuse detected", ct);
            return AuthErrors.InvalidRefreshToken;
        }

        if (stored.ExpiresAt <= Now)
            return AuthErrors.InvalidRefreshToken;

        var user = await userManager.FindByIdAsync(stored.UserId.ToString());
        if (user is null || !user.IsActive || await userManager.IsLockedOutAsync(user))
        {
            await RevokeFamilyAsync(stored.UserId, stored.FamilyId, "User not allowed", ct);
            return AuthErrors.InvalidRefreshToken;
        }

        // Roles/permisos se recalculan en cada refresh: cambios de rol aplican sin re-login.
        var (accessToken, accessExpires, refresh) = await CreateTokenPairAsync(user, stored.FamilyId, ct);

        stored.RevokedAt = Now;
        stored.RevokedReason = "Rotated";
        stored.ReplacedByTokenHash = refresh.Entity.TokenHash;

        await uow.SaveChangesAsync(ct);
        return new AuthTokens(accessToken, accessExpires, refresh.Raw, refresh.Entity.ExpiresAt);
    }

    /// <summary>Revoca la sesión a la que pertenece el refresh token (logout).</summary>
    public async Task RevokeByRawTokenAsync(string rawToken, CancellationToken ct)
    {
        var stored = await _tokens.FirstOrDefaultAsync(new RefreshTokenByHashSpec(tokenService.HashToken(rawToken)), ct);
        if (stored is not null)
            await RevokeFamilyAsync(stored.UserId, stored.FamilyId, "Logout", ct);
    }

    public Task<int> RevokeFamilyAsync(Guid userId, Guid familyId, string reason, CancellationToken ct)
    {
        var now = Now;
        return _tokens.ExecuteUpdateAsync(
            t => t.UserId == userId && t.FamilyId == familyId && t.RevokedAt == null,
            s => s.SetProperty(t => t.RevokedAt, now).SetProperty(t => t.RevokedReason, reason), ct);
    }

    public Task<int> RevokeAllAsync(Guid userId, string reason, CancellationToken ct)
    {
        var now = Now;
        return _tokens.ExecuteUpdateAsync(
            t => t.UserId == userId && t.RevokedAt == null,
            s => s.SetProperty(t => t.RevokedAt, now).SetProperty(t => t.RevokedReason, reason), ct);
    }

    public Task<IReadOnlyList<SessionDto>> GetActiveSessionsAsync(Guid userId, CancellationToken ct)
        => _tokens.ListAsync(new ActiveRefreshTokensByUserSpec(userId, Now),
            t => new SessionDto(t.FamilyId, t.CreatedAt, t.ExpiresAt, t.CreatedByIp, t.UserAgent), ct);

    public async Task<IReadOnlyList<string>> GetPermissionsAsync(IEnumerable<string> roleNames, CancellationToken ct)
    {
        var normalized = roleNames.Select(r => r.ToUpperInvariant()).ToList();
        return await (from rc in db.RoleClaims.AsNoTracking()
                      join r in db.Roles.AsNoTracking() on rc.RoleId equals r.Id
                      where normalized.Contains(r.NormalizedName!) && rc.ClaimType == Permissions.ClaimType
                      select rc.ClaimValue!)
                     .Distinct()
                     .ToListAsync(ct);
    }

    private async Task<(string AccessToken, DateTime AccessExpires, (string Raw, RefreshToken Entity) Refresh)> CreateTokenPairAsync(
        AppUser user, Guid familyId, CancellationToken ct)
    {
        var roles = await userManager.GetRolesAsync(user);
        var permissions = await GetPermissionsAsync(roles, ct);
        var (accessToken, accessExpires) = tokenService.CreateAccessToken(user, roles, permissions);

        var raw = tokenService.GenerateRefreshToken();
        var entity = new RefreshToken
        {
            UserId = user.Id,
            FamilyId = familyId,
            TokenHash = tokenService.HashToken(raw),
            CreatedAt = Now,
            ExpiresAt = Now.AddDays(_jwt.RefreshTokenDays),
            CreatedByIp = currentUser.IpAddress,
            UserAgent = currentUser.UserAgent is { Length: > 512 } ua ? ua[..512] : currentUser.UserAgent
        };
        _tokens.Add(entity);

        return (accessToken, accessExpires, (raw, entity));
    }
}
```

### 8.9 Emails de cuenta — `Infrastructure/Identity/AccountEmails.cs`
```csharp
using System.Net;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using {Project}.Application.Abstractions.Services;

namespace {Project}.Infrastructure.Identity;

/// <summary>
/// Construye y envía los emails de cuenta. Los tokens de Identity se codifican Base64Url para viajar en URLs.
/// Los enlaces apuntan al FRONTEND (App:ClientUrl), que luego llama al endpoint de la API correspondiente.
/// </summary>
public sealed class AccountEmails(IEmailSender sender, IOptions<AppUrlOptions> options)
{
    private readonly AppUrlOptions _app = options.Value;

    public static string EncodeToken(string token) => WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

    public static string? DecodeToken(string encoded)
    {
        try { return Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(encoded)); }
        catch (FormatException) { return null; }
    }

    public Task SendConfirmationAsync(AppUser user, string token, CancellationToken ct)
        => Send(user.Email!, "Confirma tu cuenta",
            $"Confirma tu cuenta: {Link("confirm-email", ("userId", user.Id.ToString()), ("token", EncodeToken(token)))}", ct);

    public Task SendPasswordResetAsync(AppUser user, string token, CancellationToken ct)
        => Send(user.Email!, "Restablecer contraseña",
            $"Restablece tu contraseña: {Link("reset-password", ("email", user.Email!), ("token", EncodeToken(token)))}", ct);

    public Task SendSetPasswordAsync(AppUser user, string token, CancellationToken ct)
        => Send(user.Email!, "Tu cuenta fue creada",
            $"Un administrador creó tu cuenta. Define tu contraseña: {Link("reset-password", ("email", user.Email!), ("token", EncodeToken(token)))}", ct);

    public Task SendChangeEmailAsync(AppUser user, string newEmail, string token, CancellationToken ct)
        => Send(newEmail, "Confirma tu nuevo email",
            $"Confirma el cambio de email: {Link("confirm-email-change", ("userId", user.Id.ToString()), ("newEmail", newEmail), ("token", EncodeToken(token)))}", ct);

    public Task SendAccountExistsAsync(string email, CancellationToken ct)
        => Send(email, "Intento de registro",
            $"Alguien intentó registrarse con tu email, pero ya tienes cuenta. Si olvidaste tu contraseña: {Link("forgot-password")}", ct);

    public Task SendSecurityNoticeAsync(string email, string message, CancellationToken ct)
        => Send(email, "Aviso de seguridad", message, ct);

    private string Link(string path, params (string Key, string Value)[] query)
    {
        var url = $"{_app.ClientUrl.TrimEnd('/')}/{path}";
        return query.Length == 0 ? url : QueryHelpers.AddQueryString(url, query.ToDictionary(q => q.Key, q => (string?)q.Value));
    }

    private Task Send(string to, string subject, string body, CancellationToken ct)
        => sender.SendAsync(to, subject, WebUtility.HtmlEncode(body), ct);
}
```

`Infrastructure/Email/LoggingEmailSender.cs`
```csharp
using {Project}.Application.Abstractions.Services;

namespace {Project}.Infrastructure.Email;

/// <summary>
/// Implementación de DESARROLLO: escribe el email en el log (incluye el enlace con el token).
/// En producción registrar una implementación real (SMTP con MailKit, SendGrid, etc.) con la misma interfaz.
/// </summary>
public sealed class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        logger.LogInformation("EMAIL (dev) To: {To} | Subject: {Subject}\n{Body}", to, subject, htmlBody);
        return Task.CompletedTask;
    }
}
```

### 8.10 `AuthService` — `Infrastructure/Identity/AuthService.cs`
```csharp
using Microsoft.AspNetCore.Identity;
using {Project}.Application.Common.Results;
using {Project}.Application.Common.Security;
using {Project}.Application.Features.Auth;

namespace {Project}.Infrastructure.Identity;

public sealed class AuthService(
    UserManager<AppUser> userManager,
    SessionManager sessions,
    ITokenService tokenService,
    AccountEmails emails,
    TimeProvider clock,
    ILogger<AuthService> logger) : IAuthService
{
    public async Task<Result> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim();

        // No revelar si el email existe: se responde igual y se avisa al dueño real por email.
        if (await userManager.FindByEmailAsync(email) is not null)
        {
            await emails.SendAccountExistsAsync(email, ct);
            return Result.Success();
        }

        var user = new AppUser
        {
            UserName = email,
            Email = email,
            FirstName = request.FirstName?.Trim(),
            LastName = request.LastName?.Trim(),
            CreatedAt = clock.GetUtcNow().UtcDateTime
        };

        var created = await userManager.CreateAsync(user, request.Password);
        if (!created.Succeeded)
            return Error.FromIdentity(created.Errors);

        await userManager.AddToRoleAsync(user, AppRoles.User);

        var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
        await emails.SendConfirmationAsync(user, token, ct);

        logger.LogInformation("Usuario registrado {UserId}", user.Id);
        return Result.Success();
    }

    public async Task<Result> ConfirmEmailAsync(ConfirmEmailRequest request, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(request.UserId.ToString());
        var token = AccountEmails.DecodeToken(request.Token);
        if (user is null || token is null)
            return AuthErrors.InvalidToken;

        if (user.EmailConfirmed)
            return Result.Success();

        var result = await userManager.ConfirmEmailAsync(user, token);
        return result.Succeeded ? Result.Success() : AuthErrors.InvalidToken;
    }

    public async Task<Result> ResendConfirmationAsync(EmailRequest request, CancellationToken ct)
    {
        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is { EmailConfirmed: false, IsActive: true })
        {
            var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
            await emails.SendConfirmationAsync(user, token, ct);
        }
        return Result.Success();   // siempre éxito: no revela existencia
    }

    public async Task<Result<LoginResponse>> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null)
            return AuthErrors.InvalidCredentials;

        if (await userManager.IsLockedOutAsync(user))
            return AuthErrors.LockedOut;

        if (!await userManager.CheckPasswordAsync(user, request.Password))
        {
            await userManager.AccessFailedAsync(user);   // cuenta intentos → lockout automático
            return await userManager.IsLockedOutAsync(user) ? AuthErrors.LockedOut : AuthErrors.InvalidCredentials;
        }

        // Estas verificaciones van DESPUÉS del password para no revelar estado de cuentas ajenas.
        if (!user.IsActive)
            return AuthErrors.AccountDisabled;

        if (userManager.Options.SignIn.RequireConfirmedEmail && !user.EmailConfirmed)
            return AuthErrors.EmailNotConfirmed;

        await userManager.ResetAccessFailedCountAsync(user);

        if (user.TwoFactorEnabled)
            return LoginResponse.TwoFactorRequired(tokenService.CreateTwoFactorChallengeToken(user.Id));

        return LoginResponse.Authenticated(await CompleteLoginAsync(user, ct));
    }

    public async Task<Result<AuthTokens>> LoginWithTwoFactorAsync(TwoFactorLoginRequest request, CancellationToken ct)
    {
        var userId = await tokenService.ValidateTwoFactorChallengeTokenAsync(request.TwoFactorToken);
        if (userId is null)
            return AuthErrors.InvalidTwoFactorToken;

        var user = await userManager.FindByIdAsync(userId.Value.ToString());
        if (user is null || !user.TwoFactorEnabled)
            return AuthErrors.InvalidTwoFactorToken;

        if (!user.IsActive)
            return AuthErrors.AccountDisabled;

        if (await userManager.IsLockedOutAsync(user))
            return AuthErrors.LockedOut;

        var valid = request.IsRecoveryCode
            ? (await userManager.RedeemTwoFactorRecoveryCodeAsync(user, request.Code.Trim())).Succeeded
            : await userManager.VerifyTwoFactorTokenAsync(user, userManager.Options.Tokens.AuthenticatorTokenProvider,
                request.Code.Replace(" ", string.Empty).Replace("-", string.Empty));

        if (!valid)
        {
            await userManager.AccessFailedAsync(user);
            return await userManager.IsLockedOutAsync(user) ? AuthErrors.LockedOut : AuthErrors.InvalidTwoFactorCode;
        }

        await userManager.ResetAccessFailedCountAsync(user);
        return await CompleteLoginAsync(user, ct);
    }

    public Task<Result<AuthTokens>> RefreshAsync(RefreshTokenRequest request, CancellationToken ct)
        => sessions.RotateAsync(request.RefreshToken, ct);

    public async Task<Result> LogoutAsync(RefreshTokenRequest request, CancellationToken ct)
    {
        await sessions.RevokeByRawTokenAsync(request.RefreshToken, ct);
        return Result.Success();   // idempotente
    }

    public async Task<Result> ForgotPasswordAsync(EmailRequest request, CancellationToken ct)
    {
        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is { IsActive: true })
        {
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            await emails.SendPasswordResetAsync(user, token, ct);
        }
        return Result.Success();   // misma respuesta exista o no el email
    }

    public async Task<Result> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct)
    {
        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        var token = AccountEmails.DecodeToken(request.Token);
        if (user is null || token is null)
            return AuthErrors.InvalidToken;

        var result = await userManager.ResetPasswordAsync(user, token, request.NewPassword);
        if (!result.Succeeded)
            return result.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.InvalidToken))
                ? AuthErrors.InvalidToken
                : Error.FromIdentity(result.Errors);

        // Quien recibió el enlace en su buzón demostró ser dueño del email.
        if (!user.EmailConfirmed)
        {
            user.EmailConfirmed = true;
            await userManager.UpdateAsync(user);
        }

        await userManager.ResetAccessFailedCountAsync(user);
        await sessions.RevokeAllAsync(user.Id, "Password reset", ct);
        await emails.SendSecurityNoticeAsync(user.Email!, "Tu contraseña fue restablecida. Si no fuiste tú, contacta soporte.", ct);
        return Result.Success();
    }

    public async Task<Result> ConfirmEmailChangeAsync(ConfirmEmailChangeRequest request, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(request.UserId.ToString());
        var token = AccountEmails.DecodeToken(request.Token);
        if (user is null || token is null)
            return AuthErrors.InvalidToken;

        var oldEmail = user.Email;
        var changed = await userManager.ChangeEmailAsync(user, request.NewEmail, token);
        if (!changed.Succeeded)
            return AuthErrors.InvalidToken;

        // El UserName se mantiene igual al email.
        var renamed = await userManager.SetUserNameAsync(user, request.NewEmail);
        if (!renamed.Succeeded)
            return Error.FromIdentity(renamed.Errors);

        await sessions.RevokeAllAsync(user.Id, "Email changed", ct);
        if (oldEmail is not null)
            await emails.SendSecurityNoticeAsync(oldEmail, $"El email de tu cuenta cambió a {request.NewEmail}.", ct);

        return Result.Success();
    }

    private async Task<AuthTokens> CompleteLoginAsync(AppUser user, CancellationToken ct)
    {
        user.LastLoginAt = clock.GetUtcNow().UtcDateTime;
        await userManager.UpdateAsync(user);
        return await sessions.IssueTokensAsync(user, familyId: null, ct);
    }
}
```

### 8.11 `AccountService` — `Infrastructure/Identity/AccountService.cs`
```csharp
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using {Project}.Application.Abstractions.Services;
using {Project}.Application.Common.Results;
using {Project}.Application.Features.Auth;

namespace {Project}.Infrastructure.Identity;

public sealed class AccountService(
    UserManager<AppUser> userManager,
    SessionManager sessions,
    ICurrentUserService currentUser,
    AccountEmails emails,
    IOptions<AppUrlOptions> appOptions) : IAccountService
{
    private const int RecoveryCodeCount = 10;

    public async Task<Result<UserProfileDto>> GetProfileAsync(CancellationToken ct)
    {
        var user = await GetCurrentUserAsync();
        return user is null ? AuthErrors.NotAuthenticated : await ToProfileAsync(user, ct);
    }

    public async Task<Result<UserProfileDto>> UpdateProfileAsync(UpdateProfileRequest request, CancellationToken ct)
    {
        var user = await GetCurrentUserAsync();
        if (user is null) return AuthErrors.NotAuthenticated;

        user.FirstName = request.FirstName?.Trim();
        user.LastName = request.LastName?.Trim();
        if (user.PhoneNumber != request.PhoneNumber)
        {
            user.PhoneNumber = request.PhoneNumber;
            user.PhoneNumberConfirmed = false;
        }

        var result = await userManager.UpdateAsync(user);
        return result.Succeeded ? await ToProfileAsync(user, ct) : Error.FromIdentity(result.Errors);
    }

    public async Task<Result<AuthTokens>> ChangePasswordAsync(ChangePasswordRequest request, CancellationToken ct)
    {
        var user = await GetCurrentUserAsync();
        if (user is null) return AuthErrors.NotAuthenticated;

        var result = await userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
            return result.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.PasswordMismatch))
                ? AuthErrors.InvalidPassword
                : Error.FromIdentity(result.Errors);

        // Cierra todas las sesiones (incluidas las de otros dispositivos) y entrega una nueva al llamador.
        await sessions.RevokeAllAsync(user.Id, "Password changed", ct);
        await emails.SendSecurityNoticeAsync(user.Email!, "Tu contraseña fue cambiada.", ct);
        return await sessions.IssueTokensAsync(user, familyId: null, ct);
    }

    public async Task<Result> RequestEmailChangeAsync(ChangeEmailRequest request, CancellationToken ct)
    {
        var user = await GetCurrentUserAsync();
        if (user is null) return AuthErrors.NotAuthenticated;

        if (!await userManager.CheckPasswordAsync(user, request.CurrentPassword))
            return AuthErrors.InvalidPassword;

        var newEmail = request.NewEmail.Trim();
        if (string.Equals(newEmail, user.Email, StringComparison.OrdinalIgnoreCase))
            return Result.Success();

        if (await userManager.FindByEmailAsync(newEmail) is not null)
            return AuthErrors.EmailInUse;

        var token = await userManager.GenerateChangeEmailTokenAsync(user, newEmail);
        await emails.SendChangeEmailAsync(user, newEmail, token, ct);   // se confirma en /auth/confirm-email-change
        return Result.Success();
    }

    public async Task<Result<IReadOnlyList<SessionDto>>> GetSessionsAsync(CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return AuthErrors.NotAuthenticated;
        return Result.Success(await sessions.GetActiveSessionsAsync(userId, ct));
    }

    public async Task<Result> RevokeSessionAsync(Guid sessionId, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return AuthErrors.NotAuthenticated;
        var revoked = await sessions.RevokeFamilyAsync(userId, sessionId, "Revoked by user", ct);
        return revoked > 0 ? Result.Success() : AuthErrors.SessionNotFound;
    }

    public async Task<Result> LogoutAllAsync(CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return AuthErrors.NotAuthenticated;
        await sessions.RevokeAllAsync(userId, "Logout all", ct);
        return Result.Success();
    }

    public async Task<Result<TwoFactorStatusDto>> GetTwoFactorStatusAsync(CancellationToken ct)
    {
        var user = await GetCurrentUserAsync();
        if (user is null) return AuthErrors.NotAuthenticated;

        return new TwoFactorStatusDto(
            user.TwoFactorEnabled,
            await userManager.GetAuthenticatorKeyAsync(user) is not null,
            await userManager.CountRecoveryCodesAsync(user));
    }

    public async Task<Result<TwoFactorSetupDto>> SetupAuthenticatorAsync(CancellationToken ct)
    {
        var user = await GetCurrentUserAsync();
        if (user is null) return AuthErrors.NotAuthenticated;
        if (user.TwoFactorEnabled) return AuthErrors.TwoFactorAlreadyEnabled;

        await userManager.ResetAuthenticatorKeyAsync(user);   // clave nueva en cada setup
        var key = (await userManager.GetAuthenticatorKeyAsync(user))!;

        var issuer = UrlEncoder.Default.Encode(appOptions.Value.AppName);
        var account = UrlEncoder.Default.Encode(user.Email!);
        var uri = $"otpauth://totp/{issuer}:{account}?secret={key}&issuer={issuer}&digits=6";

        return new TwoFactorSetupDto(FormatKey(key), uri);   // el frontend genera el QR con "uri"
    }

    public async Task<Result<RecoveryCodesDto>> EnableTwoFactorAsync(TwoFactorCodeRequest request, CancellationToken ct)
    {
        var user = await GetCurrentUserAsync();
        if (user is null) return AuthErrors.NotAuthenticated;
        if (user.TwoFactorEnabled) return AuthErrors.TwoFactorAlreadyEnabled;
        if (await userManager.GetAuthenticatorKeyAsync(user) is null) return AuthErrors.AuthenticatorNotConfigured;

        var code = request.Code.Replace(" ", string.Empty).Replace("-", string.Empty);
        if (!await userManager.VerifyTwoFactorTokenAsync(user, userManager.Options.Tokens.AuthenticatorTokenProvider, code))
            return AuthErrors.InvalidTwoFactorCode;

        await userManager.SetTwoFactorEnabledAsync(user, true);
        var codes = await userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, RecoveryCodeCount);
        await emails.SendSecurityNoticeAsync(user.Email!, "Se activó la verificación en dos pasos en tu cuenta.", ct);

        return new RecoveryCodesDto(codes?.ToList() ?? []);
    }

    public async Task<Result> DisableTwoFactorAsync(DisableTwoFactorRequest request, CancellationToken ct)
    {
        var user = await GetCurrentUserAsync();
        if (user is null) return AuthErrors.NotAuthenticated;
        if (!user.TwoFactorEnabled) return AuthErrors.TwoFactorNotEnabled;

        if (!await userManager.CheckPasswordAsync(user, request.Password))
            return AuthErrors.InvalidPassword;

        await userManager.SetTwoFactorEnabledAsync(user, false);
        await userManager.ResetAuthenticatorKeyAsync(user);   // re-activar exige un setup nuevo
        await emails.SendSecurityNoticeAsync(user.Email!, "Se desactivó la verificación en dos pasos en tu cuenta.", ct);
        return Result.Success();
    }

    public async Task<Result<RecoveryCodesDto>> RegenerateRecoveryCodesAsync(CancellationToken ct)
    {
        var user = await GetCurrentUserAsync();
        if (user is null) return AuthErrors.NotAuthenticated;
        if (!user.TwoFactorEnabled) return AuthErrors.TwoFactorNotEnabled;

        var codes = await userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, RecoveryCodeCount);
        return new RecoveryCodesDto(codes?.ToList() ?? []);
    }

    private async Task<AppUser?> GetCurrentUserAsync()
        => currentUser.UserId is { } id ? await userManager.FindByIdAsync(id.ToString()) : null;

    private async Task<UserProfileDto> ToProfileAsync(AppUser user, CancellationToken ct)
    {
        var roles = await userManager.GetRolesAsync(user);
        var permissions = await sessions.GetPermissionsAsync(roles, ct);
        return new UserProfileDto(user.Id, user.Email!, user.FirstName, user.LastName, user.PhoneNumber,
            user.EmailConfirmed, user.TwoFactorEnabled, roles.ToList(), permissions);
    }

    /// <summary>"abcd efgh ijkl ..." para escribirla a mano en la app autenticadora.</summary>
    private static string FormatKey(string key)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < key.Length; i += 4)
            sb.Append(key.AsSpan(i, Math.Min(4, key.Length - i))).Append(' ');
        return sb.ToString().TrimEnd().ToLowerInvariant();
    }
}
```

### 8.12 `UserAdminService` — `Infrastructure/Identity/UserAdminService.cs`
```csharp
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using {Project}.Application.Abstractions.Persistence;
using {Project}.Application.Abstractions.Services;
using {Project}.Application.Common.Paging;
using {Project}.Application.Common.Results;
using {Project}.Application.Common.Security;
using {Project}.Application.Features.Auth;
using {Project}.Infrastructure.Persistence;

namespace {Project}.Infrastructure.Identity;

public sealed class UserAdminService(
    UserManager<AppUser> userManager,
    RoleManager<AppRole> roleManager,
    AppDbContext db,
    IUnitOfWork uow,
    SessionManager sessions,
    AccountEmails emails,
    ICurrentUserService currentUser,
    TimeProvider clock) : IUserAdminService
{
    public async Task<Result<PagedResult<UserSummaryDto>>> GetPagedAsync(UserFilter filter, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var query = db.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var s = filter.Search.Trim();
            query = query.Where(u => u.Email!.Contains(s) || u.FirstName!.Contains(s) || u.LastName!.Contains(s));
        }

        if (filter.IsActive.HasValue)
            query = query.Where(u => u.IsActive == filter.IsActive);

        if (!string.IsNullOrWhiteSpace(filter.Role))
        {
            var role = filter.Role.Trim().ToUpperInvariant();
            query = query.Where(u => db.UserRoles.Any(ur => ur.UserId == u.Id &&
                                     db.Roles.Any(r => r.Id == ur.RoleId && r.NormalizedName == role)));
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(u => u.Email).ThenBy(u => u.Id)
            .Skip(filter.Skip).Take(filter.PageSize)
            .Select(u => new UserSummaryDto(
                u.Id,
                u.Email!,
                ((u.FirstName ?? "") + " " + (u.LastName ?? "")).Trim(),
                u.IsActive,
                u.EmailConfirmed,
                u.LockoutEnd != null && u.LockoutEnd > now,
                (from ur in db.UserRoles join r in db.Roles on ur.RoleId equals r.Id
                 where ur.UserId == u.Id select r.Name!).ToList()))
            .ToListAsync(ct);

        return new PagedResult<UserSummaryDto>(items, filter.PageNumber, filter.PageSize, total);
    }

    public async Task<Result<UserDetailDto>> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(id.ToString());
        return user is null ? UserAdminErrors.NotFound(id) : await ToDetailAsync(user);
    }

    public async Task<Result<UserDetailDto>> CreateAsync(CreateUserRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim();
        if (await userManager.FindByEmailAsync(email) is not null)
            return AuthErrors.EmailInUse;

        var roles = request.Roles.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var missing = await MissingRolesAsync(roles);
        if (missing.Count > 0)
            return UserAdminErrors.RolesNotFound(missing);

        var user = new AppUser
        {
            UserName = email,
            Email = email,
            FirstName = request.FirstName?.Trim(),
            LastName = request.LastName?.Trim(),
            CreatedAt = clock.GetUtcNow().UtcDateTime
        };

        // Usuario + roles en una transacción (UserManager comparte el DbContext del UnitOfWork).
        var created = await uow.ExecuteInTransactionAsync(async _ =>
        {
            var result = await userManager.CreateAsync(user);   // sin contraseña: la define el usuario
            if (!result.Succeeded) return Result.Failure(Error.FromIdentity(result.Errors));

            if (roles.Count > 0)
            {
                result = await userManager.AddToRolesAsync(user, roles);
                if (!result.Succeeded) return Result.Failure(Error.FromIdentity(result.Errors));
            }
            return Result.Success();
        }, ct);

        if (created.IsFailure)
            return created.Error;

        // Efecto externo FUERA de la transacción.
        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        await emails.SendSetPasswordAsync(user, token, ct);

        return await ToDetailAsync(user);
    }

    public async Task<Result> LockAsync(Guid id, LockUserRequest request, CancellationToken ct)
    {
        if (id == currentUser.UserId) return UserAdminErrors.CannotModifySelf;
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null) return UserAdminErrors.NotFound(id);

        await userManager.SetLockoutEnabledAsync(user, true);
        await userManager.SetLockoutEndDateAsync(user, request.Until ?? DateTimeOffset.MaxValue);
        await sessions.RevokeAllAsync(user.Id, "Locked by admin", ct);
        return Result.Success();
    }

    public async Task<Result> UnlockAsync(Guid id, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null) return UserAdminErrors.NotFound(id);

        await userManager.SetLockoutEndDateAsync(user, null);
        await userManager.ResetAccessFailedCountAsync(user);
        return Result.Success();
    }

    public async Task<Result> SetActiveAsync(Guid id, bool isActive, CancellationToken ct)
    {
        if (!isActive && id == currentUser.UserId) return UserAdminErrors.CannotModifySelf;
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null) return UserAdminErrors.NotFound(id);

        user.IsActive = isActive;
        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded) return Error.FromIdentity(result.Errors);

        if (!isActive)
            await sessions.RevokeAllAsync(user.Id, "Deactivated by admin", ct);
        return Result.Success();
    }

    public async Task<Result> SetRolesAsync(Guid id, SetRolesRequest request, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null) return UserAdminErrors.NotFound(id);

        var target = request.Roles.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var missing = await MissingRolesAsync(target);
        if (missing.Count > 0) return UserAdminErrors.RolesNotFound(missing);

        // Un admin no puede quitarse a sí mismo el rol Admin (evita quedarse sin administradores).
        if (id == currentUser.UserId && !target.Contains(AppRoles.Admin, StringComparer.OrdinalIgnoreCase))
            return UserAdminErrors.CannotModifySelf;

        var current = await userManager.GetRolesAsync(user);
        var toRemove = current.Except(target, StringComparer.OrdinalIgnoreCase).ToList();
        var toAdd = target.Except(current, StringComparer.OrdinalIgnoreCase).ToList();

        // Los nuevos permisos llegan al usuario en su próximo refresh de token.
        return await uow.ExecuteInTransactionAsync(async _ =>
        {
            if (toRemove.Count > 0)
            {
                var removed = await userManager.RemoveFromRolesAsync(user, toRemove);
                if (!removed.Succeeded) return Result.Failure(Error.FromIdentity(removed.Errors));
            }
            if (toAdd.Count > 0)
            {
                var added = await userManager.AddToRolesAsync(user, toAdd);
                if (!added.Succeeded) return Result.Failure(Error.FromIdentity(added.Errors));
            }
            return Result.Success();
        }, ct);
    }

    public async Task<Result> SendPasswordResetAsync(Guid id, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null) return UserAdminErrors.NotFound(id);

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        await emails.SendPasswordResetAsync(user, token, ct);
        return Result.Success();
    }

    public async Task<Result> RevokeSessionsAsync(Guid id, CancellationToken ct)
    {
        if (await userManager.FindByIdAsync(id.ToString()) is null) return UserAdminErrors.NotFound(id);
        await sessions.RevokeAllAsync(id, "Revoked by admin", ct);
        return Result.Success();
    }

    private async Task<List<string>> MissingRolesAsync(IEnumerable<string> roles)
    {
        var missing = new List<string>();
        foreach (var role in roles)
            if (!await roleManager.RoleExistsAsync(role)) missing.Add(role);
        return missing;
    }

    private async Task<UserDetailDto> ToDetailAsync(AppUser user)
    {
        var roles = await userManager.GetRolesAsync(user);
        return new UserDetailDto(user.Id, user.Email!, user.FirstName, user.LastName, user.PhoneNumber,
            user.IsActive, user.EmailConfirmed, user.TwoFactorEnabled, user.LockoutEnd, user.AccessFailedCount,
            user.CreatedAt, user.LastLoginAt, roles.ToList());
    }
}
```

### 8.13 `RoleService` — `Infrastructure/Identity/RoleService.cs`
```csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using {Project}.Application.Abstractions.Persistence;
using {Project}.Application.Common.Results;
using {Project}.Application.Common.Security;
using {Project}.Application.Features.Auth;
using {Project}.Infrastructure.Persistence;

namespace {Project}.Infrastructure.Identity;

public sealed class RoleService(RoleManager<AppRole> roleManager, AppDbContext db, IUnitOfWork uow) : IRoleService
{
    public async Task<Result<IReadOnlyList<RoleDto>>> GetAllAsync(CancellationToken ct)
    {
        var rows = await db.Roles.AsNoTracking()
            .OrderBy(r => r.Name)
            .Select(r => new { r.Id, r.Name, r.Description, UserCount = db.UserRoles.Count(ur => ur.RoleId == r.Id) })
            .ToListAsync(ct);

        IReadOnlyList<RoleDto> result = rows
            .Select(r => new RoleDto(r.Id, r.Name!, r.Description, r.UserCount, IsSystem(r.Name)))
            .ToList();
        return Result.Success(result);
    }

    public async Task<Result<RoleDetailDto>> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var role = await roleManager.FindByIdAsync(id.ToString());
        return role is null ? RoleErrors.NotFound(id) : await ToDetailAsync(role);
    }

    public async Task<Result<RoleDetailDto>> CreateAsync(CreateRoleRequest request, CancellationToken ct)
    {
        var unknown = request.Permissions.Except(Permissions.All).ToList();
        if (unknown.Count > 0) return RoleErrors.UnknownPermissions(unknown);

        var name = request.Name.Trim();
        if (await roleManager.RoleExistsAsync(name)) return RoleErrors.NameInUse(name);

        var role = new AppRole(name) { Description = request.Description?.Trim() };

        var result = await uow.ExecuteInTransactionAsync(async _ =>
        {
            var created = await roleManager.CreateAsync(role);
            if (!created.Succeeded) return Result.Failure(Error.FromIdentity(created.Errors));

            foreach (var permission in request.Permissions.Distinct())
            {
                var added = await roleManager.AddClaimAsync(role, new Claim(Permissions.ClaimType, permission));
                if (!added.Succeeded) return Result.Failure(Error.FromIdentity(added.Errors));
            }
            return Result.Success();
        }, ct);

        return result.IsFailure ? result.Error : await ToDetailAsync(role);
    }

    public async Task<Result<RoleDetailDto>> UpdateAsync(Guid id, UpdateRoleRequest request, CancellationToken ct)
    {
        var role = await roleManager.FindByIdAsync(id.ToString());
        if (role is null) return RoleErrors.NotFound(id);

        var newName = request.Name.Trim();
        var renaming = !string.Equals(role.Name, newName, StringComparison.OrdinalIgnoreCase);

        if (renaming && IsSystem(role.Name)) return RoleErrors.SystemRole;
        if (renaming && await roleManager.RoleExistsAsync(newName)) return RoleErrors.NameInUse(newName);

        role.Name = newName;
        role.Description = request.Description?.Trim();
        var result = await roleManager.UpdateAsync(role);
        return result.Succeeded ? await ToDetailAsync(role) : Error.FromIdentity(result.Errors);
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct)
    {
        var role = await roleManager.FindByIdAsync(id.ToString());
        if (role is null) return RoleErrors.NotFound(id);
        if (IsSystem(role.Name)) return RoleErrors.SystemRole;
        if (await db.UserRoles.AnyAsync(ur => ur.RoleId == id, ct)) return RoleErrors.HasUsers;

        var result = await roleManager.DeleteAsync(role);
        return result.Succeeded ? Result.Success() : Error.FromIdentity(result.Errors);
    }

    public async Task<Result<RoleDetailDto>> SetPermissionsAsync(Guid id, SetPermissionsRequest request, CancellationToken ct)
    {
        var role = await roleManager.FindByIdAsync(id.ToString());
        if (role is null) return RoleErrors.NotFound(id);

        var target = request.Permissions.Distinct().ToList();
        var unknown = target.Except(Permissions.All).ToList();
        if (unknown.Count > 0) return RoleErrors.UnknownPermissions(unknown);

        var currentClaims = (await roleManager.GetClaimsAsync(role)).Where(c => c.Type == Permissions.ClaimType).ToList();
        var toRemove = currentClaims.Where(c => !target.Contains(c.Value)).ToList();
        var toAdd = target.Except(currentClaims.Select(c => c.Value)).ToList();

        // Los usuarios del rol reciben los cambios en su próximo refresh de token.
        var result = await uow.ExecuteInTransactionAsync(async _ =>
        {
            foreach (var claim in toRemove)
            {
                var removed = await roleManager.RemoveClaimAsync(role, claim);
                if (!removed.Succeeded) return Result.Failure(Error.FromIdentity(removed.Errors));
            }
            foreach (var permission in toAdd)
            {
                var added = await roleManager.AddClaimAsync(role, new Claim(Permissions.ClaimType, permission));
                if (!added.Succeeded) return Result.Failure(Error.FromIdentity(added.Errors));
            }
            return Result.Success();
        }, ct);

        return result.IsFailure ? result.Error : await ToDetailAsync(role);
    }

    public IReadOnlyList<string> GetAvailablePermissions() => Permissions.All;

    private static bool IsSystem(string? roleName)
        => roleName is not null && AppRoles.System.Contains(roleName, StringComparer.OrdinalIgnoreCase);

    private async Task<RoleDetailDto> ToDetailAsync(AppRole role)
    {
        var permissions = (await roleManager.GetClaimsAsync(role))
            .Where(c => c.Type == Permissions.ClaimType)
            .Select(c => c.Value)
            .OrderBy(v => v)
            .ToList();
        return new RoleDetailDto(role.Id, role.Name!, role.Description, IsSystem(role.Name), permissions);
    }
}
```

### 8.14 Seed inicial (idempotente) — `Infrastructure/Persistence/DatabaseSeeder.cs`
```csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using {Project}.Application.Common.Security;
using {Project}.Infrastructure.Identity;

namespace {Project}.Infrastructure.Persistence;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var options = sp.GetRequiredService<IOptions<SeedOptions>>().Value;
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(DatabaseSeeder));

        if (options.ApplyMigrationsOnStartup)
            await sp.GetRequiredService<AppDbContext>().Database.MigrateAsync(ct);

        var roleManager = sp.GetRequiredService<RoleManager<AppRole>>();
        await EnsureRoleAsync(roleManager, AppRoles.Admin, "Administrador del sistema", []);   // Admin pasa todas las políticas
        await EnsureRoleAsync(roleManager, AppRoles.User, "Usuario estándar", Permissions.DefaultUserPermissions);

        if (string.IsNullOrWhiteSpace(options.AdminEmail) || string.IsNullOrWhiteSpace(options.AdminPassword))
        {
            logger.LogWarning("Seed:AdminEmail/AdminPassword no configurados; no se crea usuario administrador.");
            return;
        }

        var userManager = sp.GetRequiredService<UserManager<AppUser>>();
        var admin = await userManager.FindByEmailAsync(options.AdminEmail);
        if (admin is null)
        {
            admin = new AppUser
            {
                UserName = options.AdminEmail,
                Email = options.AdminEmail,
                EmailConfirmed = true,
                FirstName = "Admin",
                CreatedAt = DateTime.UtcNow
            };
            var created = await userManager.CreateAsync(admin, options.AdminPassword);
            if (!created.Succeeded)
                throw new InvalidOperationException("No se pudo crear el admin: " + string.Join("; ", created.Errors.Select(e => e.Description)));
            logger.LogInformation("Usuario administrador creado: {Email}", options.AdminEmail);
        }

        if (!await userManager.IsInRoleAsync(admin, AppRoles.Admin))
            await userManager.AddToRoleAsync(admin, AppRoles.Admin);
    }

    /// <summary>Crea el rol si no existe y AGREGA los permisos faltantes (no quita los personalizados).</summary>
    private static async Task EnsureRoleAsync(RoleManager<AppRole> roleManager, string name, string description, IEnumerable<string> permissions)
    {
        var role = await roleManager.FindByNameAsync(name);
        if (role is null)
        {
            role = new AppRole(name) { Description = description };
            await roleManager.CreateAsync(role);
        }

        var existing = (await roleManager.GetClaimsAsync(role))
            .Where(c => c.Type == Permissions.ClaimType)
            .Select(c => c.Value)
            .ToHashSet();

        foreach (var permission in permissions.Where(p => !existing.Contains(p)))
            await roleManager.AddClaimAsync(role, new Claim(Permissions.ClaimType, permission));
    }
}
```

### 8.15 Controllers de Auth

`Api/Controllers/AuthController.cs`
```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using {Project}.Application.Features.Auth;

namespace {Project}.Api.Controllers;

[AllowAnonymous]
[EnableRateLimiting(RateLimitPolicies.Auth)]
public sealed class AuthController(IAuthService auth) : ApiControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken ct)
        => HandleResult(await auth.RegisterAsync(request, ct));

    [HttpPost("confirm-email")]
    public async Task<IActionResult> ConfirmEmail(ConfirmEmailRequest request, CancellationToken ct)
        => HandleResult(await auth.ConfirmEmailAsync(request, ct));

    [HttpPost("resend-confirmation")]
    public async Task<IActionResult> ResendConfirmation(EmailRequest request, CancellationToken ct)
        => HandleResult(await auth.ResendConfirmationAsync(request, ct));

    [HttpPost("login")]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct)
        => HandleResult(await auth.LoginAsync(request, ct));

    [HttpPost("login/2fa")]
    [ProducesResponseType<AuthTokens>(StatusCodes.Status200OK)]
    public async Task<IActionResult> LoginTwoFactor(TwoFactorLoginRequest request, CancellationToken ct)
        => HandleResult(await auth.LoginWithTwoFactorAsync(request, ct));

    [HttpPost("refresh")]
    [ProducesResponseType<AuthTokens>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Refresh(RefreshTokenRequest request, CancellationToken ct)
        => HandleResult(await auth.RefreshAsync(request, ct));

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(RefreshTokenRequest request, CancellationToken ct)
        => HandleResult(await auth.LogoutAsync(request, ct));

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(EmailRequest request, CancellationToken ct)
        => HandleResult(await auth.ForgotPasswordAsync(request, ct));

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request, CancellationToken ct)
        => HandleResult(await auth.ResetPasswordAsync(request, ct));

    [HttpPost("confirm-email-change")]
    public async Task<IActionResult> ConfirmEmailChange(ConfirmEmailChangeRequest request, CancellationToken ct)
        => HandleResult(await auth.ConfirmEmailChangeAsync(request, ct));
}

public static class RateLimitPolicies
{
    public const string Auth = "auth";
}
```

`Api/Controllers/AccountController.cs`
```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using {Project}.Application.Features.Auth;

namespace {Project}.Api.Controllers;

[Authorize]
public sealed class AccountController(IAccountService account) : ApiControllerBase
{
    [HttpGet("me")]
    public async Task<IActionResult> GetProfile(CancellationToken ct) => HandleResult(await account.GetProfileAsync(ct));

    [HttpPut("me")]
    public async Task<IActionResult> UpdateProfile(UpdateProfileRequest request, CancellationToken ct)
        => HandleResult(await account.UpdateProfileAsync(request, ct));

    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken ct)
        => HandleResult(await account.ChangePasswordAsync(request, ct));

    [HttpPost("change-email")]
    public async Task<IActionResult> ChangeEmail(ChangeEmailRequest request, CancellationToken ct)
        => HandleResult(await account.RequestEmailChangeAsync(request, ct));

    [HttpGet("sessions")]
    public async Task<IActionResult> GetSessions(CancellationToken ct) => HandleResult(await account.GetSessionsAsync(ct));

    [HttpDelete("sessions/{sessionId:guid}")]
    public async Task<IActionResult> RevokeSession(Guid sessionId, CancellationToken ct)
        => HandleResult(await account.RevokeSessionAsync(sessionId, ct));

    [HttpPost("logout-all")]
    public async Task<IActionResult> LogoutAll(CancellationToken ct) => HandleResult(await account.LogoutAllAsync(ct));

    [HttpGet("2fa")]
    public async Task<IActionResult> TwoFactorStatus(CancellationToken ct) => HandleResult(await account.GetTwoFactorStatusAsync(ct));

    [HttpPost("2fa/setup")]
    public async Task<IActionResult> SetupAuthenticator(CancellationToken ct) => HandleResult(await account.SetupAuthenticatorAsync(ct));

    [HttpPost("2fa/enable")]
    public async Task<IActionResult> EnableTwoFactor(TwoFactorCodeRequest request, CancellationToken ct)
        => HandleResult(await account.EnableTwoFactorAsync(request, ct));

    [HttpPost("2fa/disable")]
    public async Task<IActionResult> DisableTwoFactor(DisableTwoFactorRequest request, CancellationToken ct)
        => HandleResult(await account.DisableTwoFactorAsync(request, ct));

    [HttpPost("2fa/recovery-codes")]
    public async Task<IActionResult> RegenerateRecoveryCodes(CancellationToken ct)
        => HandleResult(await account.RegenerateRecoveryCodesAsync(ct));
}
```

`Api/Controllers/UsersController.cs`
```csharp
using Microsoft.AspNetCore.Mvc;
using {Project}.Api.Authorization;
using {Project}.Application.Common.Security;
using {Project}.Application.Features.Auth;

namespace {Project}.Api.Controllers;

public sealed class UsersController(IUserAdminService users) : ApiControllerBase
{
    [HttpGet, HasPermission(Permissions.Users.Read)]
    public async Task<IActionResult> GetPaged([FromQuery] UserFilter filter, CancellationToken ct)
        => HandleResult(await users.GetPagedAsync(filter, ct));

    [HttpGet("{id:guid}"), HasPermission(Permissions.Users.Read)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => HandleResult(await users.GetByIdAsync(id, ct));

    [HttpPost, HasPermission(Permissions.Users.Manage)]
    public async Task<IActionResult> Create(CreateUserRequest request, CancellationToken ct)
        => HandleCreated(await users.CreateAsync(request, ct), nameof(GetById), u => new { id = u.Id });

    [HttpPost("{id:guid}/lock"), HasPermission(Permissions.Users.Manage)]
    public async Task<IActionResult> Lock(Guid id, LockUserRequest request, CancellationToken ct)
        => HandleResult(await users.LockAsync(id, request, ct));

    [HttpPost("{id:guid}/unlock"), HasPermission(Permissions.Users.Manage)]
    public async Task<IActionResult> Unlock(Guid id, CancellationToken ct)
        => HandleResult(await users.UnlockAsync(id, ct));

    [HttpPost("{id:guid}/activate"), HasPermission(Permissions.Users.Manage)]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct)
        => HandleResult(await users.SetActiveAsync(id, true, ct));

    [HttpPost("{id:guid}/deactivate"), HasPermission(Permissions.Users.Manage)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
        => HandleResult(await users.SetActiveAsync(id, false, ct));

    [HttpPut("{id:guid}/roles"), HasPermission(Permissions.Users.Manage)]
    public async Task<IActionResult> SetRoles(Guid id, SetRolesRequest request, CancellationToken ct)
        => HandleResult(await users.SetRolesAsync(id, request, ct));

    [HttpPost("{id:guid}/send-password-reset"), HasPermission(Permissions.Users.Manage)]
    public async Task<IActionResult> SendPasswordReset(Guid id, CancellationToken ct)
        => HandleResult(await users.SendPasswordResetAsync(id, ct));

    [HttpPost("{id:guid}/revoke-sessions"), HasPermission(Permissions.Users.Manage)]
    public async Task<IActionResult> RevokeSessions(Guid id, CancellationToken ct)
        => HandleResult(await users.RevokeSessionsAsync(id, ct));
}
```

`Api/Controllers/RolesController.cs`
```csharp
using Microsoft.AspNetCore.Mvc;
using {Project}.Api.Authorization;
using {Project}.Application.Common.Security;
using {Project}.Application.Features.Auth;

namespace {Project}.Api.Controllers;

public sealed class RolesController(IRoleService roles) : ApiControllerBase
{
    [HttpGet, HasPermission(Permissions.Roles.Read)]
    public async Task<IActionResult> GetAll(CancellationToken ct) => HandleResult(await roles.GetAllAsync(ct));

    [HttpGet("permissions"), HasPermission(Permissions.Roles.Read)]
    public IActionResult GetAvailablePermissions() => Ok(roles.GetAvailablePermissions());

    [HttpGet("{id:guid}"), HasPermission(Permissions.Roles.Read)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) => HandleResult(await roles.GetByIdAsync(id, ct));

    [HttpPost, HasPermission(Permissions.Roles.Manage)]
    public async Task<IActionResult> Create(CreateRoleRequest request, CancellationToken ct)
        => HandleCreated(await roles.CreateAsync(request, ct), nameof(GetById), r => new { id = r.Id });

    [HttpPut("{id:guid}"), HasPermission(Permissions.Roles.Manage)]
    public async Task<IActionResult> Update(Guid id, UpdateRoleRequest request, CancellationToken ct)
        => HandleResult(await roles.UpdateAsync(id, request, ct));

    [HttpDelete("{id:guid}"), HasPermission(Permissions.Roles.Manage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) => HandleResult(await roles.DeleteAsync(id, ct));

    [HttpPut("{id:guid}/permissions"), HasPermission(Permissions.Roles.Manage)]
    public async Task<IActionResult> SetPermissions(Guid id, SetPermissionsRequest request, CancellationToken ct)
        => HandleResult(await roles.SetPermissionsAsync(id, request, ct));
}
```

### 8.16 Contrato con el frontend (resumen para la IA que haga el cliente)
1. **Login**:
   - Si responde `tokens`, se guardan.
   - Si responde `requiresTwoFactor: true`, se pide el código y se llama `/login/2fa` con `twoFactorToken`.
2. Cada request lleva `Authorization: Bearer {accessToken}`.
3. Ante un **401** se llama `/auth/refresh` **una sola vez**, con bloqueo para evitar refresh concurrentes. Si falla, se hace logout local.
4. Cada refresh devuelve un refresh token **nuevo**. Usar uno viejo revoca la sesión entera.
5. Los enlaces de email llegan al frontend (`/confirm-email`, `/reset-password`, `/confirm-email-change`). El frontend lee el query string y hace POST al endpoint correspondiente.

---

## 9. Entidades de negocio (plantillas)

> **IMPORTANTE para la IA:** esta sección no define ninguna entidad real. Es el **molde** para cada entidad que el usuario pida.
> - Reemplazar `{Entity}`, `{Entities}`, `{entity}`, `{entities}` y `{Parent}` por los nombres reales.
> - `Name` y `Code` son campos **representativos**: sustituirlos por las propiedades reales del dominio.
> - Crear **solo** las entidades acordadas con el usuario (ver 0.1). No crear entidades de ejemplo.
> - Cada entidad vive en su carpeta `Application/Features/{Entities}/`.

### 9.1 Entidad — `Domain/Entities/{Entity}.cs`
```csharp
using {Project}.Domain.Common;

namespace {Project}.Domain.Entities;

public class {Entity} : BaseEntity
{
    // Propiedades reales del dominio. Name/Code son representativas.
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;      // si existe un identificador de negocio único
    public bool IsActive { get; set; } = true;           // bool que arranca en true: inicializador C#, no HasDefaultValue

    // Relación N:1 (si aplica)
    public int {Parent}Id { get; set; }
    public {Parent} {Parent} { get; set; } = null!;

    // Relación 1:N (si aplica)
    // public ICollection<{Child}> {Children} { get; } = new List<{Child}>();
}
```

### 9.2 Configuración EF — `Infrastructure/Persistence/Configurations/{Entity}Configuration.cs`
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using {Project}.Domain.Entities;

namespace {Project}.Infrastructure.Persistence.Configurations;

public sealed class {Entity}Configuration : BaseEntityConfiguration<{Entity}>
{
    public override void Configure(EntityTypeBuilder<{Entity}> builder)
    {
        base.Configure(builder);
        builder.ToTable("{Entities}");

        builder.Property(e => e.Name).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Code).HasMaxLength(50).IsRequired();
        // decimales: builder.Property(e => e.{Monto}).HasPrecision(18, 2);

        // Único solo entre los no eliminados (soft delete).
        builder.HasIndex(e => e.Code).IsUnique().HasFilter("[IsDeleted] = 0");

        builder.HasOne(e => e.{Parent})
               .WithMany(/* p => p.{Entities} */)
               .HasForeignKey(e => e.{Parent}Id)
               .OnDelete(DeleteBehavior.Restrict);   // con soft delete, nunca Cascade en negocio
    }
}
```
Y en `AppDbContext`: `public DbSet<{Entity}> {Entities} => Set<{Entity}>();`

### 9.3 Contratos, errores y mapeo — `Application/Features/{Entities}/{Entity}Contracts.cs`
```csharp
using System.Linq.Expressions;
using {Project}.Application.Common.Paging;
using {Project}.Application.Common.Results;
using {Project}.Domain.Entities;

namespace {Project}.Application.Features.{Entities};

// ---------- DTOs ----------

public sealed record {Entity}Dto(
    int Id,
    string Name,
    string Code,
    bool IsActive,
    int {Parent}Id,
    string {Parent}Name,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    string RowVersion);      // Base64: el cliente la devuelve en el Update (concurrencia)

public sealed record Create{Entity}Request(string Name, string Code, int {Parent}Id);

public sealed record Update{Entity}Request(string Name, bool IsActive, int {Parent}Id, string RowVersion);

/// <summary>Filtros + paginación (se enlaza desde query string).</summary>
public sealed class {Entity}Filter : PaginationParams
{
    public string? Search { get; init; }
    public int? {Parent}Id { get; init; }
    public bool? IsActive { get; init; }
    public string? SortBy { get; init; }          // valores permitidos definidos en la spec
    public bool SortDescending { get; init; }
}

// ---------- Errores (nunca strings sueltos en el service) ----------

public static class {Entity}Errors
{
    public static Error NotFound(int id) => Error.NotFound("{Entity}.NotFound", $"No existe el registro {id}.");
    public static Error CodeAlreadyExists(string code) => Error.Conflict("{Entity}.CodeAlreadyExists", $"Ya existe un registro con código '{code}'.");
    public static Error {Parent}NotFound(int id) => Error.Validation("{Entity}.{Parent}NotFound", $"El {Parent} {id} no existe.");
    public static readonly Error ConcurrencyConflict =
        Error.Conflict("{Entity}.Concurrency", "El registro fue modificado por otro usuario. Recargue e intente de nuevo.");
}

// ---------- Mapeo manual (sin AutoMapper) ----------

public static class {Entity}Mappings
{
    /// <summary>Proyección traducible a SQL: usar con repo.ListAsync / PagedListAsync / FirstOrDefaultAsync(spec, Projection).</summary>
    public static readonly Expression<Func<{Entity}, {Entity}Dto>> Projection = e => new {Entity}Dto(
        e.Id, e.Name, e.Code, e.IsActive,
        e.{Parent}Id, e.{Parent}.Name,
        e.CreatedAt, e.UpdatedAt,
        Convert.ToBase64String(e.RowVersion));

    public static {Entity} ToEntity(this Create{Entity}Request request) => new()
    {
        Name = request.Name.Trim(),
        Code = request.Code.Trim().ToUpperInvariant(),
        {Parent}Id = request.{Parent}Id
    };

    public static void ApplyTo(this Update{Entity}Request request, {Entity} entity)
    {
        entity.Name = request.Name.Trim();
        entity.IsActive = request.IsActive;
        entity.{Parent}Id = request.{Parent}Id;
    }
}
```

### 9.4 Especificaciones — `Application/Features/{Entities}/{Entity}Specifications.cs`
```csharp
using {Project}.Application.Common.Specifications;
using {Project}.Domain.Entities;

namespace {Project}.Application.Features.{Entities};

public sealed class {Entity}ByIdSpec : Specification<{Entity}>
{
    public {Entity}ByIdSpec(int id, bool tracking = false) : base(e => e.Id == id)
    {
        Include(e => e.{Parent});
        if (tracking) EnableTracking();
    }
}

/// <summary>Búsqueda paginada con filtros dinámicos y orden estable.</summary>
public sealed class {Entities}ByFilterSpec : Specification<{Entity}>
{
    public {Entities}ByFilterSpec({Entity}Filter filter)
    {
        var search = filter.Search?.Trim();

        Where(PredicateBuilder.True<{Entity}>()
            .AndIf(!string.IsNullOrEmpty(search), e => e.Name.Contains(search!) || e.Code.Contains(search!))
            .AndIf(filter.{Parent}Id.HasValue, e => e.{Parent}Id == filter.{Parent}Id)
            .AndIf(filter.IsActive.HasValue, e => e.IsActive == filter.IsActive));

        // Lista blanca de campos ordenables (nunca ordenar por un string arbitrario del cliente).
        switch (filter.SortBy?.ToLowerInvariant())
        {
            case "createdat":
                if (filter.SortDescending) OrderByDescending(e => e.CreatedAt); else OrderBy(e => e.CreatedAt);
                break;
            default:
                if (filter.SortDescending) OrderByDescending(e => e.Name); else OrderBy(e => e.Name);
                break;
        }

        OrderBy(e => e.Id);   // desempate: paginación determinista
        ApplyPaging(filter);
    }
}
```

### 9.5 Validadores — `Application/Features/{Entities}/{Entity}Validators.cs`
```csharp
using FluentValidation;

namespace {Project}.Application.Features.{Entities};

public sealed class Create{Entity}RequestValidator : AbstractValidator<Create{Entity}Request>
{
    public Create{Entity}RequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
        RuleFor(x => x.{Parent}Id).GreaterThan(0);
    }
}

public sealed class Update{Entity}RequestValidator : AbstractValidator<Update{Entity}Request>
{
    public Update{Entity}RequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.{Parent}Id).GreaterThan(0);
        RuleFor(x => x.RowVersion).NotEmpty();
    }
}
```
Regla: el **validador** revisa forma (vacíos, longitudes, rangos). El **service** revisa reglas que necesitan BD (duplicados, existencia de FKs, estados).

### 9.6 Service — `Application/Features/{Entities}/{Entity}Service.cs`
```csharp
using {Project}.Application.Abstractions.Persistence;
using {Project}.Application.Common.Paging;
using {Project}.Application.Common.Results;
using {Project}.Domain.Entities;

namespace {Project}.Application.Features.{Entities};

public interface I{Entity}Service
{
    Task<Result<{Entity}Dto>> GetByIdAsync(int id, CancellationToken ct);
    Task<Result<PagedResult<{Entity}Dto>>> GetPagedAsync({Entity}Filter filter, CancellationToken ct);
    Task<Result<{Entity}Dto>> CreateAsync(Create{Entity}Request request, CancellationToken ct);
    Task<Result<{Entity}Dto>> UpdateAsync(int id, Update{Entity}Request request, CancellationToken ct);
    Task<Result> DeleteAsync(int id, CancellationToken ct);
}

public sealed class {Entity}Service(IUnitOfWork uow) : I{Entity}Service
{
    private readonly IRepository<{Entity}> _repo = uow.Repository<{Entity}>();
    private readonly IRepository<{Parent}> _parents = uow.Repository<{Parent}>();

    public async Task<Result<{Entity}Dto>> GetByIdAsync(int id, CancellationToken ct)
    {
        var dto = await _repo.FirstOrDefaultAsync(new {Entity}ByIdSpec(id), {Entity}Mappings.Projection, ct);
        return dto is null ? {Entity}Errors.NotFound(id) : dto;
    }

    public async Task<Result<PagedResult<{Entity}Dto>>> GetPagedAsync({Entity}Filter filter, CancellationToken ct)
        => await _repo.PagedListAsync(new {Entities}ByFilterSpec(filter), {Entity}Mappings.Projection, filter, ct);

    public async Task<Result<{Entity}Dto>> CreateAsync(Create{Entity}Request request, CancellationToken ct)
    {
        var code = request.Code.Trim().ToUpperInvariant();

        if (await _repo.AnyAsync(e => e.Code == code, ct))
            return {Entity}Errors.CodeAlreadyExists(code);

        if (!await _parents.AnyAsync(p => p.Id == request.{Parent}Id, ct))
            return {Entity}Errors.{Parent}NotFound(request.{Parent}Id);

        var entity = request.ToEntity();
        _repo.Add(entity);
        await uow.SaveChangesAsync(ct);          // la auditoría la pone el interceptor

        return await GetByIdAsync(entity.Id, ct);
    }

    public async Task<Result<{Entity}Dto>> UpdateAsync(int id, Update{Entity}Request request, CancellationToken ct)
    {
        var entity = await _repo.FirstOrDefaultAsync(new {Entity}ByIdSpec(id, tracking: true), ct);
        if (entity is null)
            return {Entity}Errors.NotFound(id);

        // Concurrencia optimista: el cliente envía la RowVersion que leyó.
        if (!entity.RowVersion.AsSpan().SequenceEqual(Convert.FromBase64String(request.RowVersion)))
            return {Entity}Errors.ConcurrencyConflict;

        if (entity.{Parent}Id != request.{Parent}Id && !await _parents.AnyAsync(p => p.Id == request.{Parent}Id, ct))
            return {Entity}Errors.{Parent}NotFound(request.{Parent}Id);

        request.ApplyTo(entity);                 // entidad trackeada: no hace falta llamar Update()
        await uow.SaveChangesAsync(ct);          // DbUpdateConcurrencyException → 409 en el handler global

        return await GetByIdAsync(id, ct);
    }

    public async Task<Result> DeleteAsync(int id, CancellationToken ct)
    {
        var entity = await _repo.GetByIdAsync(id, ct);
        if (entity is null)
            return {Entity}Errors.NotFound(id);

        _repo.Remove(entity);                    // soft delete vía interceptor
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
```

### 9.7 Controller — `Api/Controllers/{Entities}Controller.cs`
```csharp
using Microsoft.AspNetCore.Mvc;
using {Project}.Api.Authorization;
using {Project}.Application.Common.Paging;
using {Project}.Application.Common.Security;
using {Project}.Application.Features.{Entities};

namespace {Project}.Api.Controllers;

public sealed class {Entities}Controller(I{Entity}Service service) : ApiControllerBase
{
    [HttpGet, HasPermission(Permissions.{Entities}.Read)]
    [ProducesResponseType<PagedResult<{Entity}Dto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPaged([FromQuery] {Entity}Filter filter, CancellationToken ct)
        => HandleResult(await service.GetPagedAsync(filter, ct));

    [HttpGet("{id:int}"), HasPermission(Permissions.{Entities}.Read)]
    [ProducesResponseType<{Entity}Dto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
        => HandleResult(await service.GetByIdAsync(id, ct));

    [HttpPost, HasPermission(Permissions.{Entities}.Write)]
    [ProducesResponseType<{Entity}Dto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(Create{Entity}Request request, CancellationToken ct)
        => HandleCreated(await service.CreateAsync(request, ct), nameof(GetById), dto => new { id = dto.Id });

    [HttpPut("{id:int}"), HasPermission(Permissions.{Entities}.Write)]
    [ProducesResponseType<{Entity}Dto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(int id, Update{Entity}Request request, CancellationToken ct)
        => HandleResult(await service.UpdateAsync(id, request, ct));

    [HttpDelete("{id:int}"), HasPermission(Permissions.{Entities}.Delete)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
        => HandleResult(await service.DeleteAsync(id, ct));
}
```

### 9.8 Patrones adicionales (aplicar solo si el dominio los requiere)

**A. Operación de varios pasos en una transacción**
```csharp
public Task<Result<{Entity}Dto>> CreateWithDetailsAsync(Create{Entity}Request request, CancellationToken ct)
    => uow.ExecuteInTransactionAsync<{Entity}Dto>(async token =>
    {
        if (await _repo.AnyAsync(e => e.Code == request.Code, token))
            return {Entity}Errors.CodeAlreadyExists(request.Code);

        var entity = request.ToEntity();
        _repo.Add(entity);
        await uow.SaveChangesAsync(token);       // obtener Id dentro de la transacción

        // ... agregar hijos con entity.Id, afectar otras tablas, etc.

        return new {Entity}Dto(/* ... */);       // SaveChanges final + Commit los hace ExecuteInTransactionAsync
    }, ct);
// Emails o llamadas HTTP: DESPUÉS, fuera de la transacción, y solo si el Result fue exitoso.
```

**B. "Solo uno activo" (un único registro con `{Flag} = true`)**
```csharp
public Task<Result> Set{Flag}Async(int id, CancellationToken ct)
    => uow.ExecuteInTransactionAsync(async token =>
    {
        var entity = await _repo.GetByIdAsync(id, token);
        if (entity is null) return {Entity}Errors.NotFound(id);

        // Excluir la propia fila: si ExecuteUpdate la tocara, cambiaría su RowVersion
        // y el SaveChanges posterior fallaría por concurrencia.
        var now = clock.GetUtcNow().UtcDateTime;   // inyectar TimeProvider clock e ICurrentUserService currentUser
        var userId = currentUser.UserId;
        await _repo.ExecuteUpdateAsync(e => e.{Flag} && e.Id != id, s => s
            .SetProperty(e => e.{Flag}, false)
            .SetProperty(e => e.UpdatedAt, now)          // ExecuteUpdate no pasa por el interceptor
            .SetProperty(e => e.UpdatedBy, userId), token);

        entity.{Flag} = true;
        return Result.Success();
    }, ct);
```
Con garantía en BD: `builder.HasIndex(e => e.{Flag}).IsUnique().HasFilter("[{Flag}] = 1 AND [IsDeleted] = 0");`. Si el "uno activo" es por padre, el índice es `HasIndex(e => new { e.{Parent}Id, e.{Flag} })` con el mismo filtro.

**C. Varias entidades casi idénticas** (mismas columnas y mismas reglas): no duplicar services ni usar `switch` sobre strings.
- **Opción 1 (preferida):** una sola tabla con discriminador (TPH) y un `enum` de tipo.
- **Opción 2:** clase base común `{Base}Entity` y un service genérico `CrudService<TEntity>` con la lógica compartida; cada tipo solo añade lo propio.
- **Opción 3:** handlers registrados por tipo (`IDictionary<{Tipo}Enum, I{Tipo}Handler>`) resueltos por DI.

**D. Reportes / stored procedures**: DTO plano + `SqlQuery<T>` parametrizado (sección 5.7) dentro de una clase `I{Report}Queries` en Infrastructure. Nunca concatenar SQL.

**E. Listado sin paginar** (combos o dropdowns): spec con `OrderBy` y sin `ApplyPaging`, más una proyección mínima `(Id, Name)`.

### 9.9 Registrar la entidad
1. `Permissions.{Entities}` (Read/Write/Delete) en `Permissions.cs`. Si corresponde, añadir `{Entities}.Read` a `DefaultUserPermissions`.
2. `services.AddScoped<I{Entity}Service, {Entity}Service>();` en `AddApplication()`.
3. Los validadores se registran solos (escaneo de assembly).
4. Migración: `dotnet ef migrations add Add{Entity} -o Infrastructure/Persistence/Migrations`.

---

## 10. DependencyInjection, Program.cs y configuración

### 10.1 `DependencyInjection.cs`
```csharp
using System.Text;
using System.Threading.RateLimiting;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using {Project}.Api.Authorization;
using {Project}.Api.Controllers;
using {Project}.Api.Errors;
using {Project}.Api.Filters;
using {Project}.Api.OpenApi;
using {Project}.Application.Abstractions.Persistence;
using {Project}.Application.Abstractions.Services;
using {Project}.Application.Features.Auth;
using {Project}.Infrastructure.Email;
using {Project}.Infrastructure.Identity;
using {Project}.Infrastructure.Persistence;
using {Project}.Infrastructure.Persistence.Interceptors;

namespace {Project};

public static class DependencyInjection
{
    public const string CorsPolicy = "Default";

    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<Program>(includeInternalTypes: true);

        // Servicios de negocio: una línea por entidad (sección 9).
        // services.AddScoped<I{Entity}Service, {Entity}Service>();

        return services;
    }

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        // ---- Persistencia ----
        services.AddScoped<AuditableEntityInterceptor>();
        services.AddDbContext<AppDbContext>((sp, options) => options
            .UseSqlServer(configuration.GetConnectionString("Default"), sql => sql.EnableRetryOnFailure())
            .AddInterceptors(sp.GetRequiredService<AuditableEntityInterceptor>()));
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // ---- Opciones (validadas al arrancar) ----
        services.AddOptions<JwtOptions>().BindConfiguration(JwtOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<AppUrlOptions>().BindConfiguration(AppUrlOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<SeedOptions>().BindConfiguration(SeedOptions.SectionName);

        // ---- Identity (local, sin cookies, sin proveedores externos) ----
        services.AddIdentityCore<AppUser>(o =>
            {
                o.User.RequireUniqueEmail = true;
                o.SignIn.RequireConfirmedEmail = true;

                o.Password.RequiredLength = 8;
                o.Password.RequireDigit = true;
                o.Password.RequireLowercase = true;
                o.Password.RequireUppercase = true;
                o.Password.RequireNonAlphanumeric = true;
                o.Password.RequiredUniqueChars = 4;

                o.Lockout.AllowedForNewUsers = true;
                o.Lockout.MaxFailedAccessAttempts = 5;
                o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddRoles<AppRole>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();   // email confirm, reset password, change email, authenticator (TOTP)

        // Vigencia de los tokens de email (confirmación, reset, cambio de email).
        services.Configure<DataProtectionTokenProviderOptions>(o => o.TokenLifespan = TimeSpan.FromHours(3));

        // ---- JWT Bearer ----
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((o, jwtOptions) =>
            {
                var jwt = jwtOptions.Value;
                o.MapInboundClaims = false;   // claims con su nombre JWT ("sub", "role", "permission")
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,   // el token de 2FA usa otra audiencia y es rechazado
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = ClaimNames.Subject,
                    RoleClaimType = ClaimNames.Role
                };
            });

        // ---- Servicios de Identity ----
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<SessionManager>();
        services.AddScoped<AccountEmails>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<IUserAdminService, UserAdminService>();
        services.AddScoped<IRoleService, RoleService>();

        // ---- Email (reemplazar por SMTP/proveedor real en producción) ----
        services.AddScoped<IEmailSender, LoggingEmailSender>();

        return services;
    }

    public static IServiceCollection AddApi(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddControllers(o => o.Filters.Add<ValidationFilter>());
        services.AddProblemDetails();
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddOpenApi(o => o.AddDocumentTransformer<BearerSecuritySchemeTransformer>());

        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddAuthorization();

        services.AddCors(o => o.AddPolicy(CorsPolicy, p => p
            .WithOrigins(configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
            .AllowAnyHeader()
            .AllowAnyMethod()));

        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            o.AddPolicy(RateLimitPolicies.Auth, ctx => RateLimitPartition.GetFixedWindowLimiter(
                ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
        });

        return services;
    }
}
```

### 10.2 `Program.cs`
```csharp
using Scalar.AspNetCore;
using {Project};
using {Project}.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration)
    .AddApi(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();               // /openapi/v1.json
    app.MapScalarApiReference();    // /scalar
}
else
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseCors(DependencyInjection.CorsPolicy);
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.MapControllers();

await DatabaseSeeder.SeedAsync(app.Services);
await app.RunAsync();

public partial class Program;   // para WebApplicationFactory en tests de integración
```

### 10.3 `appsettings.json` (sin secretos)
```json
{
  "ConnectionStrings": {
    "Default": "Server=localhost;Database={Project}Db;Trusted_Connection=True;TrustServerCertificate=True"
  },
  "Jwt": {
    "Issuer": "{Project}.Api",
    "Audience": "{Project}.Client",
    "AccessTokenMinutes": 15,
    "RefreshTokenDays": 7,
    "TwoFactorChallengeMinutes": 5
  },
  "App": {
    "ClientUrl": "https://localhost:5173",
    "AppName": "{Project}"
  },
  "Cors": {
    "AllowedOrigins": [ "https://localhost:5173" ]
  },
  "Seed": {
    "ApplyMigrationsOnStartup": false,
    "AdminEmail": "admin@{project}.local"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore.Database.Command": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

### 10.4 Secretos de desarrollo y base de datos
```bash
dotnet user-secrets set "Jwt:SigningKey" "<cadena aleatoria de 64+ caracteres>"
dotnet user-secrets set "Seed:AdminPassword" "<contraseña que cumpla la política>"
dotnet tool install --global dotnet-ef
dotnet ef migrations add InitialCreate -o Infrastructure/Persistence/Migrations
dotnet ef database update
```
Para generar la llave en PowerShell: `[Convert]::ToBase64String((1..64 | % { Get-Random -Max 256 }) -as [byte[]])`.

---

## 11. Receta: agregar una entidad de negocio

Checklist que la IA sigue por **cada** entidad acordada con el usuario:

1. [ ] `Domain/Entities/{Entity}.cs`, heredando `BaseEntity` (§9.1).
2. [ ] `{Entity}Configuration` con longitudes, precisión, índices filtrados y FKs `Restrict` (§9.2).
3. [ ] `DbSet<{Entity}>` en `AppDbContext`.
4. [ ] `Features/{Entities}/{Entity}Contracts.cs`: DTO, Requests, Filter, `{Entity}Errors`, `{Entity}Mappings` (§9.3).
5. [ ] `{Entity}Specifications.cs`: por Id y por filtro con orden y desempate (§9.4).
6. [ ] `{Entity}Validators.cs` (§9.5).
7. [ ] `I{Entity}Service` / `{Entity}Service` (§9.6), más los patrones de §9.8 si aplican.
8. [ ] `{Entities}Controller` con `[HasPermission]` en cada acción (§9.7).
9. [ ] `Permissions.{Entities}` y, si corresponde, `DefaultUserPermissions` (§9.9).
10. [ ] Registro del service en `AddApplication()`.
11. [ ] Migración `Add{Entity}` y revisión del SQL generado.
12. [ ] Tests del service: crear, duplicado, no encontrado, concurrencia y borrado lógico.

---

## 12. Testing

- **Unit tests de services** (xUnit): `AppDbContext` sobre **SQLite in-memory** (`Microsoft.EntityFrameworkCore.Sqlite`, conexión `DataSource=:memory:` abierta durante el test) + `UnitOfWork` real + `ICurrentUserService` y `TimeProvider` falsos (`Microsoft.Extensions.TimeProvider.Testing` → `FakeTimeProvider`).
  - SQLite no soporta `rowversion`. En tests, configurar `RowVersion` como `IsConcurrencyToken()` con un valor asignado por el test, o usar Testcontainers con SQL Server.
- **Integration tests**: `WebApplicationFactory<Program>`.
  - Reemplazar la cadena de conexión por una BD de test (Testcontainers SQL Server recomendado).
  - Sobrescribir `Jwt:SigningKey` y `Seed:*` por configuración.
  - Obtener un token real llamando a `/api/auth/login` con el admin del seed, o generarlo con `ITokenService` resuelto del contenedor.
- **Casos mínimos de Auth**:
  - Login correcto e incorrecto, y lockout al 5.º intento.
  - Email sin confirmar → 403.
  - Refresh con rotación; reuso de token → 401 y sesión revocada.
  - 2FA: setup, enable, login en dos pasos y login con código de recuperación.
  - Endpoint con permiso: 403 sin el permiso, 200 con el permiso y 200 como Admin.

---

## 13. Producción: checklist

- [ ] **Llaves de Data Protection persistentes**. Firman los tokens de email/reset; si se pierden al reiniciar o hay varias instancias, los enlaces dejan de servir. Ejemplo: `services.AddDataProtection().PersistKeysToDbContext<AppDbContext>()` (paquete `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore`) o Azure Blob/Key Vault.
- [ ] `Jwt:SigningKey` en Key Vault o variables de entorno, con rotación planificada.
- [ ] `IEmailSender` real (SMTP con MailKit, SendGrid, etc.) y plantillas HTML.
- [ ] Job programado (`BackgroundService` o Hangfire) que borre `RefreshTokens` expirados o revocados con más de N días: `ExecuteDeleteAsync`.
- [ ] `ApplyMigrationsOnStartup = false`. Las migraciones van en el pipeline de despliegue (`dotnet ef migrations bundle`).
- [ ] CORS solo con los orígenes reales. HTTPS y HSTS activos.
- [ ] Límite de peticiones en Auth ajustado. Si hay proxy o load balancer, configurar `ForwardedHeaders` para obtener la IP real.
- [ ] Logs estructurados (Serilog/OpenTelemetry) sin datos sensibles (nunca contraseñas, tokens ni códigos 2FA).
- [ ] Health checks (`AddHealthChecks().AddDbContextCheck<AppDbContext>()`).

---

## 14. Anexo: errores que esta arquitectura evita

Lecciones de una implementación anterior del patrón. Explican el **porqué** de las reglas:

| Problema visto | Regla que lo evita |
|---|---|
| Una propiedad `success` hacía `iserror = true ? …` (asignaba en vez de comparar): leerla corrompía la respuesta | `Result` inmutable, sin setters, con `IsSuccess` / `IsFailure` |
| Métodos `Delete` que llamaban `Remove` pero nunca `SaveChanges` | Plantilla de service con `SaveChangesAsync` explícito y tests de borrado |
| `GetByIdAsync` hacía `Entry(null)` y explotaba antes de validar si existía | `FindAsync` devuelve `null` y el service responde `NotFound` |
| Paginación con `Take` antes de `Skip` y `OrderBy` después de paginar | `SpecificationEvaluator` con orden fijo y excepción si se pagina sin orden |
| Parámetros `(take, skip)` en la interfaz y `(skip, take)` en la implementación | Paginación encapsulada en `PaginationParams` |
| SQL de stored procedure armado concatenando strings | Solo `SqlQuery` / `FromSqlInterpolated` parametrizados |
| `TransactionScope` con doble `Dispose`, sin rollback en excepciones y `DbContext` dispuesto por el UoW | `ExecuteInTransactionAsync` con `IDbContextTransaction` y estrategia de reintentos; el UoW no dispone el contexto |
| Guardados fuera de la transacción por empezarla tarde | La transacción envuelve toda la operación en un lambda |
| Auditoría (`CreatedBy`, `UpdatedBy`…) repetida en cada método, a veces con errores | `AuditableEntityInterceptor` |
| `HasDefaultValue(DateTime.Now)`: fecha congelada al generar la migración | `HasDefaultValueSql("SYSUTCDATETIME()")` y `TimeProvider` |
| Métodos `GetAll` que devolvían `null` | Todo devuelve `Result<T>` |
| Páginas o vistas que enlazaban el objeto de respuesta completo (overposting) | Requests dedicados (`Create{Entity}Request` / `Update{Entity}Request`) |
| Un `switch` por strings con 8 tablas casi idénticas y código copiado | Patrones de §9.8-C (TPH, service genérico o handlers) |
| Consultas N+1 de nombres de usuario | Proyecciones con joins en una sola consulta |
| Includes por string sin chequeo de compilación | Includes tipados en `Specification<T>` |
