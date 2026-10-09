using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using KitApi.Infrastructure.Analytics;

namespace KitApi.Infrastructure.Persistence.Configurations;

public sealed class AnalyticsEventConfiguration : IEntityTypeConfiguration<AnalyticsEvent>
{
    public void Configure(EntityTypeBuilder<AnalyticsEvent> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).HasMaxLength(100).IsRequired();
        builder.Property(e => e.AnonymousId).HasMaxLength(64);
        builder.Property(e => e.Source).HasMaxLength(10).IsRequired();
        builder.Property(e => e.TraceId).HasMaxLength(32);
#if (sqlserver)
        builder.Property(e => e.Properties).HasColumnType("nvarchar(max)").IsRequired();
        builder.ToTable(t => t.HasCheckConstraint("CK_AnalyticsEvents_Properties_IsJson", "ISJSON([Properties]) = 1"));
#endif
#if (postgresql)
        builder.Property(e => e.Properties).HasColumnType("jsonb").IsRequired();
#endif
        builder.HasIndex(e => new { e.Name, e.OccurredAt });
        builder.HasIndex(e => e.OccurredAt);
    }
}
