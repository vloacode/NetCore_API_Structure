using System.Diagnostics;

namespace KitApi.Api.Middleware;

public static class TraceIdHeaderMiddleware
{
    /// <summary>Devuelve el traceId en cada respuesta para que el cliente lo muestre al reportar un problema.</summary>
    public static IApplicationBuilder UseTraceIdHeader(this IApplicationBuilder app) => app.Use(async (context, next) =>
    {
        context.Response.OnStarting(() =>
        {
            context.Response.Headers["X-Trace-Id"] = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;
            return Task.CompletedTask;
        });
        await next();
    });
}
