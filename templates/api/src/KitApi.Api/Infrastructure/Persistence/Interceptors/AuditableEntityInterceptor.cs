using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using KitApi.Application.Abstractions.Services;
using KitApi.Domain.Common;

namespace KitApi.Infrastructure.Persistence.Interceptors;

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
