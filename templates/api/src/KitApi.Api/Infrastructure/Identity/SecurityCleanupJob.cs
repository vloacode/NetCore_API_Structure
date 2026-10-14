using Microsoft.EntityFrameworkCore;
using KitApi.Infrastructure.Persistence;

namespace KitApi.Infrastructure.Identity;

/// <summary>
/// Limpieza diaria de seguridad:
/// - Refresh tokens vencidos (rotados, revocados o expirados): un token vencido ya no sirve ni para detectar reuso.
/// - Eventos de la bitácora más viejos que SecurityAudit:RetentionDays (365 por defecto).
/// Sin esto, ambas tablas crecen para siempre.
/// </summary>
public sealed class SecurityCleanupJob(
    IServiceScopeFactory scopes,
    IConfiguration configuration,
    TimeProvider clock,
    ILogger<SecurityCleanupJob> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(24), clock);
        do
        {
            try
            {
                var now = clock.GetUtcNow().UtcDateTime;
                await using var scope = scopes.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var tokenLimit = now.AddDays(-1);   // margen de un día sobre la expiración
                var tokens = await db.RefreshTokens.Where(t => t.ExpiresAt < tokenLimit).ExecuteDeleteAsync(stoppingToken);

                var auditLimit = now.AddDays(-configuration.GetValue("SecurityAudit:RetentionDays", 365));
                var events = await db.SecurityEvents.Where(e => e.OccurredAt < auditLimit).ExecuteDeleteAsync(stoppingToken);

                if (tokens + events > 0)
                    logger.LogInformation("Limpieza de seguridad: {Tokens} refresh tokens vencidos y {Events} eventos de bitácora borrados", tokens, events);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Falló la limpieza de seguridad");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
