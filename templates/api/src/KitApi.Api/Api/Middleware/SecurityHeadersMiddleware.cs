namespace KitApi.Api.Middleware;

public static class SecurityHeadersMiddleware
{
    /// <summary>Headers mínimos para una API JSON. La CSP estricta no aplica a /scalar (solo existe en Development).</summary>
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) => app.Use(async (context, next) =>
    {
        var headers = context.Response.Headers;
        headers.XContentTypeOptions = "nosniff";
        headers.XFrameOptions = "DENY";
        headers["Referrer-Policy"] = "no-referrer";
        headers["Cross-Origin-Resource-Policy"] = "same-site";

        if (!context.Request.Path.StartsWithSegments("/scalar"))
            headers.ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";

        await next();
    });
}
