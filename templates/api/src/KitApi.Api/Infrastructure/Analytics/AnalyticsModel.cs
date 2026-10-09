using System.ComponentModel.DataAnnotations;
using KitApi.Application.Abstractions.Analytics;

namespace KitApi.Infrastructure.Analytics;

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
