using System.Linq.Expressions;
#if (security)
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
#endif
using Microsoft.EntityFrameworkCore;
using KitApi.Domain.Common;
using KitApi.Infrastructure.Persistence.Converters;
#if (security)
using KitApi.Infrastructure.Identity;
#endif
#if (analytics)
using KitApi.Infrastructure.Analytics;
#endif

namespace KitApi.Infrastructure.Persistence;

// Con seguridad hereda de IdentityDbContext (tablas AspNet*); sin seguridad, de DbContext.
public class AppDbContext(DbContextOptions<AppDbContext> options)
#if (security)
    : IdentityDbContext<AppUser, AppRole, Guid>(options)
#endif
#if (!security)
    : DbContext(options)
#endif
{
#if (security)
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
#endif
#if (analytics)
    public DbSet<AnalyticsEvent> AnalyticsEvents => Set<AnalyticsEvent>();
#endif

    // Entidades de negocio: un DbSet por entidad (lo indica `dotnet new kit-entity`). Define el nombre de la tabla.

    // Todas las fechas se guardan y se leen como UTC (Kind = Utc): el JSON sale con "Z" (standards/15).
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<UtcDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);   // primero (con seguridad crea el modelo de Identity)

        // Una clase IEntityTypeConfiguration<T> por entidad (en Features/{Entities}/{Entity}.cs o en Persistence/Configurations).
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
