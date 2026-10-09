using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using KitApi.Application.Abstractions.Analytics;
using KitApi.Infrastructure.Persistence;

namespace KitApi.Infrastructure.Analytics;

/// <summary>Destino de eventos. Para enviar a otro servicio (GA4, PostHog…) se implementa esta interfaz con un ADR.</summary>
public interface IAnalyticsSink
{
    Task WriteAsync(IReadOnlyList<AnalyticsEnvelope> batch, CancellationToken ct);
}

public sealed class DatabaseAnalyticsSink(AppDbContext db) : IAnalyticsSink
{
    public async Task WriteAsync(IReadOnlyList<AnalyticsEnvelope> batch, CancellationToken ct)
    {
        db.Set<AnalyticsEvent>().AddRange(batch.Select(e => new AnalyticsEvent
        {
            Name = e.Name,
            OccurredAt = e.OccurredAt,
            AnonymousId = e.AnonymousId,
            Source = e.Source == AnalyticsSource.Client ? "client" : "server",
            Properties = JsonSerializer.Serialize(e.Properties),
            TraceId = e.TraceId
        }));
        await db.SaveChangesAsync(ct);
    }
}

public sealed class LoggingAnalyticsSink(ILogger<LoggingAnalyticsSink> logger) : IAnalyticsSink
{
    public Task WriteAsync(IReadOnlyList<AnalyticsEnvelope> batch, CancellationToken ct)
    {
        foreach (var e in batch)
            logger.LogInformation("ANALYTICS {EventName} {Source} {AnonymousId} {Properties}",
                e.Name, e.Source, e.AnonymousId, JsonSerializer.Serialize(e.Properties));
        return Task.CompletedTask;
    }
}

/// <summary>Vacía la cola en lotes hacia todos los destinos. Un destino que falla no afecta a los demás ni a la API.</summary>
public sealed class AnalyticsDispatcher(
    AnalyticsBuffer queue,
    IServiceScopeFactory scopes,
    IOptions<AnalyticsOptions> options,
    ILogger<AnalyticsDispatcher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var batch = new List<AnalyticsEnvelope>(options.Value.BatchSize);
        while (await queue.Reader.WaitToReadAsync(stoppingToken))
        {
            while (batch.Count < options.Value.BatchSize && queue.Reader.TryRead(out var envelope))
                batch.Add(envelope);

            await using var scope = scopes.CreateAsyncScope();
            foreach (var sink in scope.ServiceProvider.GetServices<IAnalyticsSink>())
            {
                try
                {
                    await sink.WriteAsync(batch, stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogWarning(ex, "Destino de analítica {Sink} falló; lote de {Count} eventos descartado", sink.GetType().Name, batch.Count);
                }
            }
            batch.Clear();
        }
    }
}

/// <summary>Borra una vez al día los eventos más antiguos que Analytics:RetentionDays.</summary>
public sealed class AnalyticsRetentionJob(
    IServiceScopeFactory scopes,
    IOptions<AnalyticsOptions> options,
    TimeProvider clock,
    ILogger<AnalyticsRetentionJob> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(24), clock);
        do
        {
            try
            {
                var limit = clock.GetUtcNow().UtcDateTime.AddDays(-options.Value.RetentionDays);
                await using var scope = scopes.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var deleted = await db.Set<AnalyticsEvent>().Where(e => e.OccurredAt < limit).ExecuteDeleteAsync(stoppingToken);
                if (deleted > 0)
                    logger.LogInformation("Retención de analítica: {Deleted} eventos borrados", deleted);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Falló la limpieza de eventos de analítica");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
