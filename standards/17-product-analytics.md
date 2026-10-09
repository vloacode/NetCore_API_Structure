# Analítica de producto (uso)

> **Aplica a:** Todos los perfiles, solo si `Capabilities` incluye `product-analytics`. Las líneas `// [CAP:product-analytics]` de otros standards solo existen con la capacidad activa.  
> **Propósito:** Registrar eventos de uso del producto ("¿qué hacen los usuarios?") en la base de datos del proyecto, con privacidad desde el diseño. Destinos, dispatcher, retención y registro: `standards/17b`. Endpoint para el frontend y consultas de KPIs: `standards/17a`.  
> Índice general: `standards/00-INDEX.md`

## Qué es y qué no es
| Es | No es |
|---|---|
| Hechos de negocio confirmados por la API: `user.registered`, `{entity}.created`, `search.no_results` | Telemetría técnica (latencia, errores): eso es `standards/10` |
| Datos propios, exactos (no los bloquea un ad-blocker), consultables con SQL | Analítica web de páginas, sesiones o campañas: eso es GA4 u otra herramienta en el **frontend** |
| Seudónimos: nunca el id real, email ni nombre | Reportes de BI ni tableros listos |

## Cómo funciona
```
Service (después de SaveChanges OK) ─► IAnalyticsTracker.Track() ─► cola en memoria (Channel acotado)
Frontend ─► POST /api/analytics/events (17a) ─┘                          │
                                              AnalyticsDispatcher (BackgroundService, lotes)
                                                           └─► cada IAnalyticsSink: BD (por defecto) · log (dev) · otro (ADR)
```
- `Track` **no bloquea y nunca falla la petición**: si la analítica está apagada o la cola llena, el evento se descarta.
- Se llama **solo después de un guardado exitoso** (fuera de `ExecuteInTransactionAsync`).
- Si un evento no puede perderse nunca (por ejemplo, para facturar), no es analítica: usar outbox (`ai/suggestions-catalog.md`).

## Reglas
1. Nombres `area.accion` en minúsculas y en pasado: `user.registered`, `invoice.paid`, `search.no_results`. Siempre como constante en `AnalyticsEvents`, nunca un string suelto.
2. Propiedades: pocas (máximo 25), simples (texto corto, número, booleano, fecha) y **sin datos personales**. La guardia descarta claves sensibles (`email`, `password`, `token`, `phone`, `name`, `address`, `ip`…) y textos largos.
3. Identidad: `AnonymousId = HMAC-SHA256(Analytics:HashKey, id)`, del usuario autenticado o del `X-Anonymous-Id` que envía el frontend. La llave va en user-secrets o Key Vault.
4. Cada evento nuevo se documenta en `docs/11-product-analytics.md` (flujo `ai/workflows/analytics-event.md`).
5. Retención limitada (`Analytics:RetentionDays`, 90 por defecto); un job borra lo vencido.

## Contratos — `Application/Abstractions/Analytics/Analytics.cs`
```csharp
namespace {Project}.Application.Abstractions.Analytics;

public enum AnalyticsSource { Server, Client }

public interface IAnalyticsTracker
{
    /// <summary>Encola un evento de uso. No bloquea ni lanza: si la analítica está apagada o la cola llena, se descarta.</summary>
    void Track(string eventName, IReadOnlyDictionary<string, object?>? properties = null, AnalyticsSource source = AnalyticsSource.Server);
}

/// <summary>Catálogo de eventos (detalle y propósito de cada uno en docs/11-product-analytics.md).</summary>
public static class AnalyticsEvents
{
    // ---- Eventos del servidor: uno por hecho de negocio que interese medir ----
    // public const string {Entity}Created = "{entity}.created";

    // ---- Eventos que el frontend puede enviar por POST /api/analytics/events ----
    public const string PageViewed = "page.viewed";
    public const string SearchNoResults = "search.no_results";

    public static readonly IReadOnlySet<string> ClientAllowed = new HashSet<string>(StringComparer.Ordinal)
    {
        PageViewed,
        SearchNoResults
    };
}

/// <summary>Evita que lleguen datos personales o valores no aptos a la analítica.</summary>
public static class AnalyticsPropertyGuard
{
    public const int MaxProperties = 25;
    public const int MaxStringLength = 200;

    private static readonly string[] _sensitiveFragments = ["email", "mail", "password", "pass", "token", "secret", "phone", "card", "ssn"];
    private static readonly HashSet<string> _sensitiveKeys = new(StringComparer.OrdinalIgnoreCase)
        { "name", "firstname", "lastname", "fullname", "address", "ip", "ipaddress", "username" };

    public static bool IsSensitiveKey(string key)
        => _sensitiveKeys.Contains(key) || _sensitiveFragments.Any(f => key.Contains(f, StringComparison.OrdinalIgnoreCase));

    public static bool IsAllowedValue(object? value) => value switch
    {
        null or bool or int or long or decimal or double or float or DateTime or DateTimeOffset or Guid => true,
        string s => s.Length <= MaxStringLength,
        _ => false
    };

    /// <summary>Devuelve solo las propiedades permitidas y la lista de claves descartadas.</summary>
    public static Dictionary<string, object?> Sanitize(IReadOnlyDictionary<string, object?>? properties, out List<string> rejected)
    {
        rejected = [];
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (properties is null) return result;

        foreach (var (key, value) in properties)
        {
            if (result.Count >= MaxProperties || IsSensitiveKey(key) || !IsAllowedValue(value))
                rejected.Add(key);
            else
                result[key] = value;
        }
        return result;
    }
}
```

## Opciones y evento — `Infrastructure/Analytics/AnalyticsModel.cs`
```csharp
using System.ComponentModel.DataAnnotations;
using {Project}.Application.Abstractions.Analytics;

namespace {Project}.Infrastructure.Analytics;

public sealed class AnalyticsOptions
{
    public const string SectionName = "Analytics";

    public bool Enabled { get; init; } = true;

    /// <summary>Llave del HMAC para seudonimizar (32+ caracteres). user-secrets / Key Vault, nunca en appsettings.json.</summary>
    [Required, MinLength(32)] public string HashKey { get; init; } = string.Empty;

    [Range(1, 3650)] public int RetentionDays { get; init; } = 90;
    [Range(100, 1_000_000)] public int QueueCapacity { get; init; } = 10_000;
    [Range(1, 1000)] public int BatchSize { get; init; } = 100;

    /// <summary>Además de la BD, escribe cada evento en el log (útil en desarrollo).</summary>
    public bool LogEvents { get; init; }
}

/// <summary>Evento ya procesado (nombre validado, propiedades limpias, identidad seudónima) listo para los destinos.</summary>
public sealed record AnalyticsEnvelope(
    string Name, DateTime OccurredAt, string? AnonymousId, AnalyticsSource Source,
    IReadOnlyDictionary<string, object?> Properties, string? TraceId);

/// <summary>Fila de la tabla de eventos. Solo inserción: sin auditoría, soft delete ni concurrencia.</summary>
public sealed class AnalyticsEvent
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string Name { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
    public string? AnonymousId { get; set; }
    public string Source { get; set; } = string.Empty;
    public string Properties { get; set; } = "{}";   // JSON
    public string? TraceId { get; set; }
}
```

## Configuración EF — `Infrastructure/Persistence/Configurations/AnalyticsEventConfiguration.cs`
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using {Project}.Infrastructure.Analytics;

namespace {Project}.Infrastructure.Persistence.Configurations;

public sealed class AnalyticsEventConfiguration : IEntityTypeConfiguration<AnalyticsEvent>
{
    public void Configure(EntityTypeBuilder<AnalyticsEvent> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).HasMaxLength(100).IsRequired();
        builder.Property(e => e.AnonymousId).HasMaxLength(64);
        builder.Property(e => e.Source).HasMaxLength(10).IsRequired();
        builder.Property(e => e.TraceId).HasMaxLength(32);
        builder.Property(e => e.Properties).HasColumnType("nvarchar(max)").IsRequired();   // [MSSQL]
        builder.ToTable(t => t.HasCheckConstraint("CK_AnalyticsEvents_Properties_IsJson", "ISJSON([Properties]) = 1"));   // [MSSQL]
        builder.Property(e => e.Properties).HasColumnType("jsonb").IsRequired();   // [PGSQL]
        builder.HasIndex(e => new { e.Name, e.OccurredAt });
        builder.HasIndex(e => e.OccurredAt);
    }
}
```
Y en `AppDbContext` (`standards/02`): `public DbSet<AnalyticsEvent> AnalyticsEvents => Set<AnalyticsEvent>();`.

## Tracker, cola y seudónimo — `Infrastructure/Analytics/AnalyticsTracker.cs`
```csharp
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using Microsoft.Extensions.Options;
using {Project}.Application.Abstractions.Analytics;
using {Project}.Application.Abstractions.Services;

namespace {Project}.Infrastructure.Analytics;

/// <summary>Cola en memoria compartida (singleton) entre el tracker y el dispatcher.</summary>
public sealed class AnalyticsBuffer(IOptions<AnalyticsOptions> options)
{
    private readonly Channel<AnalyticsEnvelope> _channel = Channel.CreateBounded<AnalyticsEnvelope>(
        new BoundedChannelOptions(options.Value.QueueCapacity) { FullMode = BoundedChannelFullMode.DropWrite, SingleReader = true });

    public bool TryEnqueue(AnalyticsEnvelope envelope) => _channel.Writer.TryWrite(envelope);
    public ChannelReader<AnalyticsEnvelope> Reader => _channel.Reader;
}

public sealed partial class AnalyticsTracker(
    AnalyticsBuffer queue,
    IOptions<AnalyticsOptions> options,
    ICurrentUserService currentUser,
    IHttpContextAccessor accessor,
    TimeProvider clock,
    ILogger<AnalyticsTracker> logger) : IAnalyticsTracker
{
    public const string AnonymousIdHeader = "X-Anonymous-Id";

    public void Track(string eventName, IReadOnlyDictionary<string, object?>? properties = null, AnalyticsSource source = AnalyticsSource.Server)
    {
        if (!options.Value.Enabled) return;

        if (!EventNameRegex().IsMatch(eventName))
        {
            logger.LogWarning("Evento de analítica con nombre inválido descartado: {EventName}", eventName);
            return;
        }

        var clean = AnalyticsPropertyGuard.Sanitize(properties, out var rejected);
        if (rejected.Count > 0)
            logger.LogWarning("Evento {EventName}: propiedades descartadas {Keys}", eventName, string.Join(", ", rejected));

        var envelope = new AnalyticsEnvelope(eventName, clock.GetUtcNow().UtcDateTime, ResolveAnonymousId(), source,
            clean, Activity.Current?.TraceId.ToString());

        if (!queue.TryEnqueue(envelope))
            logger.LogWarning("Cola de analítica llena: evento {EventName} descartado", eventName);
    }

    /// <summary>Usuario autenticado o X-Anonymous-Id del frontend (GUID), siempre con HMAC: nunca el valor real.</summary>
    private string? ResolveAnonymousId()
    {
        var raw = currentUser.UserId?.ToString();
        if (raw is null && Guid.TryParse(accessor.HttpContext?.Request.Headers[AnonymousIdHeader].ToString(), out var anonymous))
            raw = anonymous.ToString();
        if (raw is null) return null;

        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(options.Value.HashKey), Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexStringLower(hash)[..32];
    }

    [GeneratedRegex("^[a-z0-9_]+(\\.[a-z0-9_]+)+$")]
    private static partial Regex EventNameRegex();
}
```

## Destinos, dispatcher, retención y registro
En `standards/17b-analytics-dispatch.md`.

## Uso en un service
```csharp
// En el constructor: IAnalyticsTracker analytics
_repo.Add(entity);
await uow.SaveChangesAsync(ct);
analytics.Track(AnalyticsEvents.{Entity}Created, new Dictionary<string, object?> { ["plan"] = entity.Plan });   // después del guardado
```
Con `ExecuteInTransactionAsync`, llamar a `Track` **después** de que el método devuelva un `Result` exitoso, nunca dentro.

## Agregar otro destino (GA4, PostHog…)
El kit no trae código de servicios externos: un proyecto que lo necesite agrega una clase `IAnalyticsSink` y la registra, con un ADR. Para **GA4 (Measurement Protocol)** el destino necesita:
- `measurement_id` y `api_secret`, este último en secretos;
- el `client_id` de GA4 que envía el frontend (cookie `_ga`) para unir el evento a la sesión web;
- adaptar los nombres a `snake_case` (máx. 40 caracteres) y respetar máx. 25 parámetros por evento;
- un `HttpClient` tipado con `AddStandardResilienceHandler` (`standards/11`).

Referencia: https://developers.google.com/analytics/devguides/collection/protocol/ga4
