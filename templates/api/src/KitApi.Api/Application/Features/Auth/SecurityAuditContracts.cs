using KitApi.Application.Common.Paging;
using KitApi.Application.Common.Results;

namespace KitApi.Application.Features.Auth;

/// <summary>Tipos de evento de la bitácora de seguridad ("area.accion" en pasado). Nunca strings sueltos.</summary>
public static class SecurityEventTypes
{
    public const string LoginSucceeded = "login.succeeded";
    public const string LoginFailed = "login.failed";
    public const string LoginBlocked = "login.blocked";               // intento sobre una cuenta ya bloqueada
    public const string AccountLocked = "account.locked";             // bloqueo automático por intentos fallidos
    public const string TwoFactorFailed = "login.2fa_failed";
    public const string SessionReuseDetected = "session.reuse_detected";
    public const string SessionsRevoked = "sessions.revoked";
    public const string PasswordChanged = "password.changed";
    public const string PasswordReset = "password.reset";
    public const string EmailChanged = "email.changed";
    public const string TwoFactorEnabled = "2fa.enabled";
    public const string TwoFactorDisabled = "2fa.disabled";
    public const string UserLocked = "user.locked";
    public const string UserUnlocked = "user.unlocked";
    public const string UserActivated = "user.activated";
    public const string UserDeactivated = "user.deactivated";
    public const string UserRolesChanged = "user.roles_changed";
    public const string RolePermissionsChanged = "role.permissions_changed";
    public const string RoleDeleted = "role.deleted";
}

/// <summary>
/// Escribe en la bitácora de seguridad. Nunca hace fallar la operación: si no puede escribir, registra un warning.
/// Quién lo hizo (actor) e IP se toman del request actual.
/// </summary>
public interface ISecurityAuditLog
{
    /// <param name="type">Constante de <see cref="SecurityEventTypes"/>.</param>
    /// <param name="userId">Usuario afectado (null si no se conoce, por ejemplo un email inexistente).</param>
    /// <param name="details">Texto corto sin datos sensibles (nunca contraseñas, tokens ni códigos).</param>
    Task WriteAsync(string type, Guid? userId, string? details = null, CancellationToken ct = default);
}

public sealed record SecurityEventDto(
    Guid Id,
    DateTime OccurredAt,
    string Type,
    Guid? UserId,
    string? UserEmail,
    Guid? ActorId,
    string? IpAddress,
    string? Details);

/// <summary>Filtros de la bitácora (query string). Orden: más recientes primero.</summary>
public sealed class SecurityEventFilter : PaginationParams
{
    public Guid? UserId { get; init; }
    public string? Type { get; init; }
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
}

public interface ISecurityEventService
{
    Task<Result<PagedResult<SecurityEventDto>>> GetPagedAsync(SecurityEventFilter filter, CancellationToken ct);
}
