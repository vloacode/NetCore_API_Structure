using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using KitApi.Application.Abstractions.Persistence;
using KitApi.Application.Abstractions.Services;
using KitApi.Application.Common.Paging;
using KitApi.Application.Common.Results;
using KitApi.Application.Common.Specifications;
using KitApi.Application.Features.Auth;
using KitApi.Infrastructure.Persistence;

namespace KitApi.Infrastructure.Identity;

/// <summary>Evento de la bitácora de seguridad. Solo se inserta (sin auditoría ni soft delete); se borra por retención.</summary>
public class SecurityEvent
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public DateTime OccurredAt { get; set; }
    public string Type { get; set; } = string.Empty;
    public Guid? UserId { get; set; }
    public Guid? ActorId { get; set; }          // quién hizo la acción (un admin, o el mismo usuario)
    public string? IpAddress { get; set; }
    public string? Details { get; set; }
}

public sealed class SecurityEventConfiguration : IEntityTypeConfiguration<SecurityEvent>
{
    public void Configure(EntityTypeBuilder<SecurityEvent> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Type).HasMaxLength(64).IsRequired();
        builder.Property(e => e.IpAddress).HasMaxLength(64);
        builder.Property(e => e.Details).HasMaxLength(500);
        builder.HasIndex(e => new { e.UserId, e.OccurredAt });
        builder.HasIndex(e => new { e.Type, e.OccurredAt });
        builder.HasIndex(e => e.OccurredAt);
    }
}

/// <summary>
/// Escribe con su propio DbContext (scope nuevo): el evento queda guardado aunque la operación que lo originó
/// haga rollback, y no arrastra cambios pendientes del contexto del request.
/// </summary>
public sealed class SecurityAuditLog(
    IServiceScopeFactory scopes,
    ICurrentUserService currentUser,
    TimeProvider clock,
    ILogger<SecurityAuditLog> logger) : ISecurityAuditLog
{
    public async Task WriteAsync(string type, Guid? userId, string? details = null, CancellationToken ct = default)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.SecurityEvents.Add(new SecurityEvent
            {
                OccurredAt = clock.GetUtcNow().UtcDateTime,
                Type = type,
                UserId = userId,
                ActorId = currentUser.UserId,
                IpAddress = currentUser.IpAddress,
                Details = details is { Length: > 500 } ? details[..500] : details
            });
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "No se pudo escribir el evento de seguridad {Type} del usuario {UserId}", type, userId);
        }
    }
}

public sealed class SecurityEventsByFilterSpec : Specification<SecurityEvent>
{
    public SecurityEventsByFilterSpec(SecurityEventFilter filter)
    {
        Where(PredicateBuilder.True<SecurityEvent>()
            .AndIf(filter.UserId.HasValue, e => e.UserId == filter.UserId)
            .AndIf(!string.IsNullOrWhiteSpace(filter.Type), e => e.Type == filter.Type)
            .AndIf(filter.From.HasValue, e => e.OccurredAt >= filter.From)
            .AndIf(filter.To.HasValue, e => e.OccurredAt < filter.To));
        OrderByDescending(e => e.OccurredAt);
        OrderByDescending(e => e.Id);   // desempate: Guid v7 respeta el orden de creación
        ApplyPaging(filter);
    }
}

public sealed class SecurityEventService(IUnitOfWork uow, AppDbContext db) : ISecurityEventService
{
    public async Task<Result<PagedResult<SecurityEventDto>>> GetPagedAsync(SecurityEventFilter filter, CancellationToken ct)
    {
        // El email se resuelve en la consulta (no se guarda en la bitácora: si el usuario lo cambia, se ve el actual).
        var users = db.Users;
        Expression<Func<SecurityEvent, SecurityEventDto>> projection = e => new SecurityEventDto(
            e.Id, e.OccurredAt, e.Type, e.UserId,
            users.Where(u => u.Id == e.UserId).Select(u => u.Email).FirstOrDefault(),
            e.ActorId, e.IpAddress, e.Details);

        return await uow.Repository<SecurityEvent>().PagedListAsync(new SecurityEventsByFilterSpec(filter), projection, filter, ct);
    }
}
