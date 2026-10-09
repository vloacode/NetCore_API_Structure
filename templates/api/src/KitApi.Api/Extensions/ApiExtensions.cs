using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using KitApi.Api.Errors;
using KitApi.Api.Filters;
#if (security)
using KitApi.Api.Controllers;
using KitApi.Api.OpenApi;
#endif

namespace KitApi.Extensions;

public static class ApiExtensions
{
    public const string CorsPolicy = "Default";

    /// <summary>Controllers, JSON, ProblemDetails, OpenAPI, CORS, Forwarded Headers y rate limiting.</summary>
    public static IServiceCollection AddApi(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddControllers(o => o.Filters.Add<ValidationFilter>())
            // JSON amigable para el frontend (standards/15): camelCase (default) + enums como texto.
            .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        // Todo ProblemDetails (excepciones, 404/405 vacíos, etc.) lleva el mismo traceId que el header X-Trace-Id (standards/08).
        // Se sobrescribe a propósito: el framework pone por defecto el traceparent completo ("00-…-00").
        services.AddProblemDetails(o => o.CustomizeProblemDetails = ctx =>
            ctx.ProblemDetails.Extensions["traceId"] = Activity.Current?.TraceId.ToString() ?? ctx.HttpContext.TraceIdentifier);
        services.AddExceptionHandler<GlobalExceptionHandler>();
#if (security)
        services.AddOpenApi(o => o.AddDocumentTransformer<BearerSecuritySchemeTransformer>());
#endif
#if (!security)
        services.AddOpenApi();
#endif

        services.AddCors(o => o.AddPolicy(CorsPolicy, p => p
            .WithOrigins(configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
            .AllowAnyHeader()
            .AllowAnyMethod()
            .WithExposedHeaders("X-Trace-Id", "Retry-After", "Content-Disposition")));   // legibles desde el navegador (standards/15)

        // Detrás de un reverse proxy o balanceador (Nginx, YARP, Azure Front Door…): IP real del cliente y esquema HTTPS.
        // Solo se confía en los proxies declarados; se activa con ReverseProxy:Enabled (standards/13a).
        services.Configure<ForwardedHeadersOptions>(o =>
        {
            o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
            o.ForwardLimit = 1;   // un solo salto de proxy: el cliente no puede falsificar su IP con X-Forwarded-For
            foreach (var proxy in configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? [])
                o.KnownProxies.Add(IPAddress.Parse(proxy));
            foreach (var network in configuration.GetSection("ReverseProxy:KnownNetworks").Get<string[]>() ?? [])
                o.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
        });

        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            o.OnRejected = (context, _) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    context.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
                return ValueTask.CompletedTask;
            };

            // Límite global por IP para todos los endpoints (imprescindible en una API pública).
            o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx => RateLimitPartition.GetFixedWindowLimiter(
                ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 100, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));

#if (security)
            // Límite estricto para los endpoints de Auth.
            o.AddPolicy(RateLimitPolicies.Auth, ctx => RateLimitPartition.GetFixedWindowLimiter(
                ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
#endif
        });

        return services;
    }
}
