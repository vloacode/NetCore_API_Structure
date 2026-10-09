# Dominio y persistencia (EF Core)

> **Aplica a:** Todos los perfiles  
> **Propósito:** Entidad base, contratos IRepository/IUnitOfWork, AppDbContext, configuraciones EF e interceptor de auditoría/soft delete.  
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

/// <summary>SQL Server no guarda el Kind: al leer se marca como UTC para que se serialice con "Z".</summary>
public sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
    v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
    v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
```
Agregar `using {Project}.Infrastructure.Persistence.Converters;` en `AppDbContext`.

### Configuración base — `Infrastructure/Persistence/Configurations/BaseEntityConfiguration.cs`
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using {Project}.Domain.Common;

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
