# Observabilidad

> **Aplica a:** Todos los perfiles  
> **Propósito:** Logging estructurado, trazas y métricas con OpenTelemetry, health checks y correlación de peticiones.  
> Índice general: `standards/00-INDEX.md`

## Principios
1. Usar **`ILogger<T>`** de Microsoft.Extensions.Logging en todo el código. Nunca `Console.WriteLine`.
2. Logs **estructurados**: plantillas con marcadores (`"Pedido {OrderId} creado"`), nunca interpolación (`$"..."`).
3. **Correlación**: cada petición tiene un `traceId` W3C que viaja en logs, trazas, ProblemDetails y en el header `X-Trace-Id`.
4. **OpenTelemetry** es el estándar para trazas, métricas y logs. El destino (Azure Monitor, Grafana, Seq, Jaeger) se elige por configuración.
5. **Nunca loguear datos sensibles** (`standards/09`).

## Niveles de log

| Nivel | Cuándo | Ejemplo |
|---|---|---|
| `Trace` / `Debug` | Diagnóstico en desarrollo | Valores intermedios de un cálculo |
| `Information` | Eventos de negocio relevantes | "Usuario {UserId} registrado", "Factura {InvoiceId} emitida" |
| `Warning` | Algo inesperado que el sistema maneja | Login fallido, cuenta bloqueada, reintento a un tercero, 409 por concurrencia |
| `Error` | Falló una operación | Excepción no controlada, tercero caído tras reintentos |
| `Critical` | El servicio no puede funcionar | Sin conexión a BD al arrancar |

Configuración base en `appsettings.json` (`standards/01a`): `Default: Information`, `Microsoft.AspNetCore: Warning`, comandos de EF en `Warning`.

## Logging de alto rendimiento (rutas calientes)
Para logs que se ejecutan muchas veces, usar el generador de código:
```csharp
public static partial class {Entity}Log
{
    [LoggerMessage(Level = LogLevel.Information, Message = "{Entity} {Id} creado por {UserId}")]
    public static partial void Created(ILogger logger, int id, Guid? userId);
}
// Uso: {Entity}Log.Created(logger, entity.Id, currentUser.UserId);
```

## Header `X-Trace-Id` — `Api/Middleware/TraceIdHeaderMiddleware.cs`
```csharp
using System.Diagnostics;

namespace {Project}.Api.Middleware;

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
```
El header se expone por CORS (`standards/01a`). ProblemDetails trae el mismo valor en `traceId`.

## OpenTelemetry
Paquetes:
```bash
dotnet add package OpenTelemetry.Extensions.Hosting
dotnet add package OpenTelemetry.Instrumentation.AspNetCore
dotnet add package OpenTelemetry.Instrumentation.Http
dotnet add package OpenTelemetry.Instrumentation.Runtime
dotnet add package OpenTelemetry.Exporter.OpenTelemetryProtocol
# Opcional (versión prerelease): OpenTelemetry.Instrumentation.EntityFrameworkCore
```

Registro en `AddPersistence` o en un método `AddObservability(IConfiguration)`:
```csharp
services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("{Project}.Api"))
    .WithTracing(t => t
        .AddAspNetCoreInstrumentation(o => o.Filter = ctx => !ctx.Request.Path.StartsWithSegments("/health"))
        .AddHttpClientInstrumentation()
        .AddOtlpExporter())
    .WithMetrics(m => m
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddOtlpExporter())
    .WithLogging(l => l.AddOtlpExporter());
```
- El destino OTLP se configura con variables estándar: `OTEL_EXPORTER_OTLP_ENDPOINT`. En desarrollo, el dashboard de .NET Aspire o Seq sirven como receptor local.
- **Azure Monitor / Application Insights**: reemplazar los exportadores OTLP por el paquete `Azure.Monitor.OpenTelemetry.AspNetCore` y `services.AddOpenTelemetry().UseAzureMonitor()`, con la connection string en la configuración.
- Referencia: https://learn.microsoft.com/dotnet/core/diagnostics/observability-with-otel

### Métricas de negocio propias
```csharp
public sealed class {Project}Metrics
{
    public const string MeterName = "{Project}.Api";
    private readonly Counter<long> _{entities}Created;

    public {Project}Metrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);
        _{entities}Created = meter.CreateCounter<long>("{entities}.created");
    }

    public void {Entity}Created() => _{entities}Created.Add(1);
}
// Registro: services.AddSingleton<{Project}Metrics>(); y en WithMetrics: .AddMeter({Project}Metrics.MeterName)
```

## Serilog (opcional)
Si el equipo prefiere Serilog (sinks a archivo, Seq o Elasticsearch):
```csharp
// dotnet add package Serilog.AspNetCore
builder.Services.AddSerilog((sp, lc) => lc.ReadFrom.Configuration(builder.Configuration).ReadFrom.Services(sp));
app.UseSerilogRequestLogging();   // un log por petición con método, ruta, status y duración
```
OpenTelemetry y Serilog pueden convivir. Elegir uno como fuente principal y registrarlo en un ADR.

## Health checks
Ya registrados en `standards/01a`:

| Endpoint | Qué verifica | Lo usa |
|---|---|---|
| `/health/live` | Que el proceso responde (no ejecuta checks) | Liveness probe: reiniciar si falla |
| `/health/ready` | Checks con tag `ready` (base de datos) | Readiness probe y balanceador: no enviar tráfico si falla |

Agregar un check por cada dependencia crítica (Redis, almacenamiento, APIs externas) con `tags: ["ready"]`. No exponer detalles internos en la respuesta pública.

## Qué loguear obligatoriamente

| Evento | Nivel | Campos |
|---|---|---|
| Login fallido / lockout / 2FA inválido `[SEC]` | Warning | `UserId` (si existe), `Ip` |
| Reuso de refresh token detectado `[SEC]` | Warning | `UserId`, `FamilyId` |
| Acceso denegado (403) | Warning | `UserId`, ruta, permiso |
| Excepción no controlada | Error | `traceId`, ruta (lo hace `GlobalExceptionHandler`) |
| Llamada a tercero fallida tras reintentos | Error | servicio, operación, duración |
| Operaciones de negocio clave | Information | ids de negocio, `UserId` |

## Alertas recomendadas (producción)
- Tasa de 5xx > 1 % durante 5 minutos.
- Latencia p95 por encima del objetivo de `docs/04-non-functional-requirements.md`.
- Pico de logins fallidos o lockouts (posible ataque).
- `/health/ready` fallando.
- Errores de conexión a la base de datos.

Para investigar incidentes, seguir `ai/workflows/log-analysis.md` y los runbooks de `docs/runbooks/`.
