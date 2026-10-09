using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
#if (useApiKey)
using KitApi.Api.Authentication;
#endif
using KitApi.Api.Middleware;
using KitApi.Extensions;
using KitApi.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Límites de Kestrel y sin header "Server" (standards/09).
builder.WebHost.ConfigureKestrel(o =>
{
    o.AddServerHeader = false;
    o.Limits.MaxRequestBodySize = 10 * 1024 * 1024;   // 10 MB; subir solo en endpoints de archivos con [RequestSizeLimit]
});

// ---------- Servicios: cada línea es un área (Extensions/) ----------
builder.Services
    .AddPersistence(builder.Configuration)          // EF Core, Unit of Work, auditoría, health check de la BD
#if (security)
    .AddSecurity()                                  // Identity local, JWT, refresh tokens, permisos
#endif
#if (useApiKey)
    .AddApiKeyAuthentication()                      // X-Api-Key para las escrituras (API pública)
#endif
    .AddApplication()                               // validadores + services de Features/ (registro automático)
    .AddApi(builder.Configuration)                  // controllers, ProblemDetails, OpenAPI, CORS, rate limiting
    .AddObservability(builder.Configuration);       // OpenTelemetry

var app = builder.Build();

// ---------- Pipeline HTTP (el orden importa) ----------
if (app.Configuration.GetValue<bool>("ReverseProxy:Enabled"))
    app.UseForwardedHeaders();   // primero: el resto del pipeline (rate limit, logs, HTTPS) ve la IP y el esquema reales

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseTraceIdHeader();                      // X-Trace-Id en cada respuesta (standards/10)
app.UseSecurityHeaders();                    // nosniff, frame, referrer, CSP (standards/09)

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();               // /openapi/v1.json
    app.MapScalarApiReference();    // /scalar
}
else
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseCors(ApiExtensions.CorsPolicy);
#if (security || useApiKey)
app.UseAuthentication();
#endif
app.UseRateLimiter();
#if (security || useApiKey)
app.UseAuthorization();
#endif

app.MapControllers();
app.MapHealthChecks("/health/live", new() { Predicate = _ => false });                      // proceso vivo
app.MapHealthChecks("/health/ready", new() { Predicate = c => c.Tags.Contains("ready") });   // dependencias listas

// ---------- Arranque ----------
// Migraciones al arrancar: solo en desarrollo y tests. En producción van en el pipeline de despliegue (standards/13).
if (app.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup"))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
}

#if (security)
await DatabaseSeeder.SeedAsync(app.Services);   // roles, permisos y admin
#endif
await app.RunAsync();

public partial class Program;   // para WebApplicationFactory en tests de integración
