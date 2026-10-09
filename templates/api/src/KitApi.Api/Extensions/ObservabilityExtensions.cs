using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace KitApi.Extensions;

public static class ObservabilityExtensions
{
    /// <summary>
    /// OpenTelemetry: trazas, métricas y logs (standards/10). El exportador OTLP solo se activa si existe
    /// OTEL_EXPORTER_OTLP_ENDPOINT (dashboard de Aspire, Seq, Jaeger, un collector…); sin él no se envía nada.
    /// </summary>
    public static IServiceCollection AddObservability(this IServiceCollection services, IConfiguration configuration)
    {
        var otel = services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService("KitApi.Api"))
            .WithTracing(t => t
                .AddAspNetCoreInstrumentation(o => o.Filter = ctx => !ctx.Request.Path.StartsWithSegments("/health"))
                .AddHttpClientInstrumentation())
            .WithMetrics(m => m
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation());

        if (!string.IsNullOrWhiteSpace(configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
            otel.UseOtlpExporter();

        return services;
    }
}
