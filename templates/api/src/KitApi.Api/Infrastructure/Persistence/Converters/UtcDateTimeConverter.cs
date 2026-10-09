using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace KitApi.Infrastructure.Persistence.Converters;

/// <summary>Garantiza Kind = Utc al leer (SQL Server no guarda el Kind) y al escribir (Npgsql exige UTC en timestamptz).</summary>
public sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
    v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
    v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
