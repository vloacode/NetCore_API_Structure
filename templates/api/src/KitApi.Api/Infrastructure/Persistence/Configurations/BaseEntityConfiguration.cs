using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using KitApi.Domain.Common;

namespace KitApi.Infrastructure.Persistence.Configurations;

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
