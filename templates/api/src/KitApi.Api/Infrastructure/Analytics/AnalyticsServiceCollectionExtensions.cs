using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using KitApi.Application.Abstractions.Analytics;
using KitApi.Application.Features.Analytics;

namespace KitApi.Infrastructure.Analytics;

public static class AnalyticsServiceCollectionExtensions
{
    public const string RateLimitPolicy = "analytics";

    public static IServiceCollection AddProductAnalytics(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AnalyticsOptions>().BindConfiguration(AnalyticsOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();
        services.AddSingleton<AnalyticsBuffer>();
        services.AddScoped<IAnalyticsTracker, AnalyticsTracker>();
        services.AddScoped<IAnalyticsIngestionService, AnalyticsIngestionService>();
        services.AddScoped<IAnalyticsSink, DatabaseAnalyticsSink>();
        if (configuration.GetValue<bool>("Analytics:LogEvents"))
            services.AddScoped<IAnalyticsSink, LoggingAnalyticsSink>();
        // Otro destino (GA4, PostHog…): services.AddScoped<IAnalyticsSink, <Destino>>(); con ADR.

        services.AddHostedService<AnalyticsDispatcher>();
        services.AddHostedService<AnalyticsRetentionJob>();

        // Límite propio para el endpoint de eventos del frontend (por IP).
        services.Configure<RateLimiterOptions>(o => o.AddPolicy(RateLimitPolicy, ctx => RateLimitPartition.GetFixedWindowLimiter(
            ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 60, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 })));

        return services;
    }
}
