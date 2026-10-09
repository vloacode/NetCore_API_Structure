using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using Microsoft.Extensions.Options;
using KitApi.Application.Abstractions.Analytics;
using KitApi.Application.Abstractions.Services;

namespace KitApi.Infrastructure.Analytics;

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
