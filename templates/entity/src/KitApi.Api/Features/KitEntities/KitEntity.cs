using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using KitApi.Domain.Common;
#if (hasParent)
using KitApi.Features.ParentFeature;
#endif
using KitApi.Infrastructure.Persistence;
using KitApi.Infrastructure.Persistence.Configurations;

namespace KitApi.Features.KitEntities;

// REGISTRO (copiar una vez y borrar este comentario):
//   Infrastructure/Persistence/AppDbContext.cs  ->  using KitApi.Features.KitEntities;  +  public DbSet<KitEntity> KitEntities => Set<KitEntity>();
#if (security)
//   Application/Common/Security/Permissions.cs  ->  public static class KitEntities { public const string Read = "kitentities.read"; public const string Write = "kitentities.write"; public const string Delete = "kitentities.delete"; }
#endif
//   Después: dotnet format, dotnet build y dotnet ef migrations add AddKitEntity.

public class KitEntity : BaseEntity
{
    // Name y Code son representativos: reemplazarlos por las propiedades reales del dominio (docs/05-domain-model.md).
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;      // identificador de negocio único (quitar si no existe)
    public bool IsActive { get; set; } = true;            // bool que arranca en true: inicializador C#, no HasDefaultValue
#if (hasParent)

    public int KitParentId { get; set; }
    public KitParent KitParent { get; set; } = null!;
#endif
}

public sealed class KitEntityConfiguration : BaseEntityConfiguration<KitEntity>
{
    public override void Configure(EntityTypeBuilder<KitEntity> builder)
    {
        base.Configure(builder);
        // Sin ToTable: el nombre sale del DbSet (KitEntities) y en PostgreSQL se convierte a snake_case.

        builder.Property(e => e.Name).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Code).HasMaxLength(50).IsRequired();
        // Decimales siempre con precisión: builder.Property(e => e.Amount).HasPrecision(18, 2);

        // Único solo entre los no eliminados (soft delete).
        builder.HasIndex(e => e.Code).IsUnique().HasFilter(SqlDialect.NotDeleted);
#if (hasParent)

        builder.HasOne(e => e.KitParent)
               .WithMany()
               .HasForeignKey(e => e.KitParentId)
               .OnDelete(DeleteBehavior.Restrict);   // con soft delete, nunca Cascade en negocio
#endif
    }
}
