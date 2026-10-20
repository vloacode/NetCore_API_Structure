namespace KitApi.Domain.Common;

public interface IAuditable
{
    DateTime CreatedAt { get; set; }
    EntityKey? CreatedBy { get; set; }
    DateTime? UpdatedAt { get; set; }
    EntityKey? UpdatedBy { get; set; }
}

public interface ISoftDelete
{
    bool IsDeleted { get; set; }
    DateTime? DeletedAt { get; set; }
    EntityKey? DeletedBy { get; set; }
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
    public EntityKey? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public EntityKey? UpdatedBy { get; set; }

    // Soft delete: Remove() se convierte en IsDeleted = true en el interceptor.
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public EntityKey? DeletedBy { get; set; }

    // Concurrencia optimista: el interceptor genera un valor nuevo en cada insert/update;
    // EF lo incluye en el WHERE del UPDATE, así que un cambio concurrente provoca DbUpdateConcurrencyException.
    public Guid RowVersion { get; set; } = Guid.NewGuid();
}

/// <summary>
/// Entidad con la PK del proyecto: EntityKey (Guid, int o long según se eligió al crear la solución;
/// alias global en Directory.Build.props). Las FK hacia otras entidades y hacia usuarios usan el mismo EntityKey.
/// </summary>
public abstract class BaseEntity : BaseEntity<EntityKey>;
