using Microsoft.EntityFrameworkCore;
using KitApi.Infrastructure.Persistence;

namespace KitApi.Infrastructure.Identity;

/// <summary>
/// Borra una vez al día los refresh tokens vencidos (rotados, revocados o simplemente expirados).
/// Un token vencido ya no sirve ni para detectar reuso: se rechaza por expirado. Sin esto, la tabla crece para siempre.
/// </summary>
public sealed class RefreshTokenCleanupJob(
    IServiceScopeFactory scopes,
    TimeProvider clock,
    ILogger<RefreshTokenCleanupJob> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(24), clock);
        do
        {
            try
            {
                var limit = clock.GetUtcNow().UtcDateTime.AddDays(-1);   // margen de un día sobre la expiración
                await using var scope = scopes.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var deleted = await db.RefreshTokens.Where(t => t.ExpiresAt < limit).ExecuteDeleteAsync(stoppingToken);
                if (deleted > 0)
                    logger.LogInformation("Limpieza de sesiones: {Deleted} refresh tokens vencidos borrados", deleted);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Falló la limpieza de refresh tokens vencidos");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
