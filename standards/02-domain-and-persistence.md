# Dominio y persistencia (EF Core)

> **Aplica a:** Todos los perfiles y ambos motores (SQL Server y PostgreSQL; marcas `[MSSQL]` / `[PGSQL]`)  
> **Propósito:** Entidad base, contratos IRepository/IUnitOfWork, AppDbContext, dialecto SQL, configuraciones EF e interceptor de auditoría, soft delete y concurrencia.  
> Índice general: `standards/00-INDEX.md`

## Domain: entidad base

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

/// <summary>Token de concurrencia optimista que maneja la aplicación (igual en SQL Server y PostgreSQL).</summary>
public interface IVersioned
{
    Guid RowVersion { get; set; }
}

public abstract class BaseEntity<TKey> : IAuditable, ISoftDelete, IVersioned
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

    // Concurrencia optimista: el interceptor genera un valor nuevo en cada insert/update;
    // EF lo incluye en el WHERE del UPDATE, así que un cambio concurrente provoca DbUpdateConcurrencyException.
    public Guid RowVersion { get; set; } = Guid.NewGuid();
}

/// <summary>Entidad con PK int identity (default del proyecto).</summary>
public abstract class BaseEntity : BaseEntity<int>;
```

> `CreatedBy`, `UpdatedBy` y `DeletedBy` guardan el `Guid` del `AppUser` autenticado.
>
> **¿Por qué un `Guid` y no `rowversion`?** `rowversion` solo existe en SQL Server (PostgreSQL usaría `xmin`, con otro tipo). Un token que maneja la aplicación funciona igual en ambos motores y es el mismo enfoque de ASP.NET Identity (`ConcurrencyStamp`). Regla: toda actualización masiva con `ExecuteUpdateAsync` también debe asignar `RowVersion = Guid.NewGuid()`.

### Abstracciones de persistencia

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

## Persistencia

### `Infrastructure/Persistence/AppDbContext.cs`
```csharp
using System.Linq.Expressions;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;   // [SEC]
using Microsoft.EntityFrameworkCore;
using {Project}.Domain.Common;
using {Project}.Infrastructure.Identity;                   // [SEC]

namespace {Project}.Infrastructure.Persistence;

// Con seguridad hereda de IdentityDbContext (tablas AspNet*); sin seguridad, de DbContext. Usar solo la línea del perfil.
public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<AppUser, AppRole, Guid>(options)   // [SEC]
    : DbContext(options)                                   // [PUB]
{
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();   // [SEC]

    // Entidades de negocio (standards/07): una línea por entidad.
    // public DbSet<{Entity}> {Entities} => Set<{Entity}>();

    // Todas las fechas se guardan y se leen como UTC (Kind = Utc): el JSON sale con "Z" (standards/15).
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<UtcDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);   // primero (con seguridad crea el modelo de Identity)

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

`Infrastructure/Persistence/Converters/UtcDateTimeConverter.cs`
```csharp
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace {Project}.Infrastructure.Persistence.Converters;

/// <summary>Garantiza Kind = Utc al leer (SQL Server no guarda el Kind) y al escribir (Npgsql exige UTC en timestamptz).</summary>
public sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
    v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
    v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
```
Agregar `using {Project}.Infrastructure.Persistence.Converters;` en `AppDbContext`.

### Dialecto SQL — `Infrastructure/Persistence/SqlDialect.cs`
Fragmentos de SQL que dependen del motor (filtros de índices). Un proyecto usa **un solo motor**: al crearlo se conserva solo la línea del perfil (`Database` en `docs/00-MASTER_CONTEXT.md`).
```csharp
using System.Text.RegularExpressions;   // [PGSQL]

namespace {Project}.Infrastructure.Persistence;

public static partial class SqlDialect
{
    public const string Provider = "SqlServer";   // [MSSQL]
    public const string Provider = "PostgreSQL";  // [PGSQL]

    public const string True = "1";       // [MSSQL]
    public const string True = "true";    // [PGSQL]
    public const string False = "0";      // [MSSQL]
    public const string False = "false";  // [PGSQL]

    /// <summary>Nombre de columna tal como queda en la BD (PostgreSQL usa snake_case con EFCore.NamingConventions).</summary>
    public static string Column(string propertyName) => $"[{propertyName}]";                // [MSSQL]
    public static string Column(string propertyName) => $"\"{ToSnakeCase(propertyName)}\"";  // [PGSQL]

    /// <summary>Filtro de índices únicos que ignoran los registros eliminados (soft delete).</summary>
    public static string NotDeleted => $"{Column("IsDeleted")} = {False}";

    private static string ToSnakeCase(string name) => SnakeCaseRegex().Replace(name, "$1_$2").ToLowerInvariant();  // [PGSQL]

    [GeneratedRegex("([a-z0-9])([A-Z])")]  // [PGSQL]
    private static partial Regex SnakeCaseRegex();  // [PGSQL]
}
```
Uso en configuraciones: `.HasFilter(SqlDialect.NotDeleted)` y, para condiciones propias, `$"{SqlDialect.Column(nameof({Entity}.{Flag}))} = {SqlDialect.True}"`.

### Configuración base — `Infrastructure/Persistence/Configurations/BaseEntityConfiguration.cs`
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using {Project}.Domain.Common;

namespace {Project}.Infrastructure.Persistence.Configurations;

/// <summary>Configuración común de BaseEntity: concurrencia e índice de soft delete. Igual en ambos motores.</summary>
public abstract class BaseEntityConfiguration<T> : IEntityTypeConfiguration<T> where T : BaseEntity
{
    public virtual void Configure(EntityTypeBuilder<T> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.RowVersion).IsConcurrencyToken();
        builder.HasIndex(e => e.IsDeleted);
        // Sin defaults SQL para fechas: el interceptor asigna CreatedAt/UpdatedAt (nunca HasDefaultValue(DateTime.Now)).
    }
}
```

### Configuraciones de Identity `[SEC]` — `Infrastructure/Persistence/Configurations/IdentityConfigurations.cs`
Solo con seguridad.
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using {Project}.Infrastructure.Identity;

namespace {Project}.Infrastructure.Persistence.Configurations;

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
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
    }
}

public sealed class AppRoleConfiguration : IEntityTypeConfiguration<AppRole>
{
    public void Configure(EntityTypeBuilder<AppRole> builder)
        => builder.Property(r => r.Description).HasMaxLength(250);
}
```

> ⚠️ **No usar `HasDefaultValue(true)` en propiedades `bool`.** EF no envía el valor `false` (es el default de C#), así que la BD guardaría `true`. Para que un bool arranque en `true`, usar el inicializador de C# (`= true`).

### Interceptor de auditoría y soft delete — `Infrastructure/Persistence/Interceptors/AuditableEntityInterceptor.cs`
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

            // Concurrencia: valor nuevo en cada escritura. El original (leído) va en el WHERE del UPDATE.
            if (entry.Entity is IVersioned versioned && entry.State is EntityState.Added or EntityState.Modified)
                versioned.RowVersion = Guid.NewGuid();

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
