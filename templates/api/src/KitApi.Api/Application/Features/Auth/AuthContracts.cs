using KitApi.Application.Common.Paging;
using KitApi.Application.Common.Results;

namespace KitApi.Application.Features.Auth;

// =====================  DTOs: autenticación (anónimos)  =====================

public sealed record RegisterRequest(string Email, string Password, string? FirstName, string? LastName);
public sealed record ConfirmEmailRequest(Guid UserId, string Token);
public sealed record EmailRequest(string Email);
public sealed record LoginRequest(string Email, string Password);
public sealed record TwoFactorLoginRequest(string TwoFactorToken, string Code, bool IsRecoveryCode = false);
public sealed record RefreshTokenRequest(string RefreshToken);
public sealed record ResetPasswordRequest(string Email, string Token, string NewPassword);
public sealed record ConfirmEmailChangeRequest(Guid UserId, string NewEmail, string Token);

public sealed record AuthTokens(
    string AccessToken,
    DateTime AccessTokenExpiresAt,
    string RefreshToken,
    DateTime RefreshTokenExpiresAt,
    string TokenType = "Bearer");

/// <summary>Si RequiresTwoFactor = true, el cliente llama a /auth/login/2fa con TwoFactorToken + código.</summary>
public sealed record LoginResponse(bool RequiresTwoFactor, string? TwoFactorToken, AuthTokens? Tokens)
{
    public static LoginResponse TwoFactorRequired(string token) => new(true, token, null);
    public static LoginResponse Authenticated(AuthTokens tokens) => new(false, null, tokens);
}

// =====================  DTOs: cuenta del usuario autenticado  =====================

public sealed record UserProfileDto(
    Guid Id, string Email, string? FirstName, string? LastName, string? PhoneNumber,
    bool EmailConfirmed, bool TwoFactorEnabled, IReadOnlyList<string> Roles, IReadOnlyList<string> Permissions);

public sealed record UpdateProfileRequest(string? FirstName, string? LastName, string? PhoneNumber);
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public sealed record ChangeEmailRequest(string NewEmail, string CurrentPassword);
public sealed record SessionDto(Guid Id, DateTime LastActivityAt, DateTime ExpiresAt, string? IpAddress, string? UserAgent);
public sealed record TwoFactorStatusDto(bool IsEnabled, bool HasAuthenticator, int RecoveryCodesLeft);
public sealed record TwoFactorSetupDto(string SharedKey, string AuthenticatorUri);
public sealed record TwoFactorCodeRequest(string Code);
public sealed record DisableTwoFactorRequest(string Password);
public sealed record RecoveryCodesDto(IReadOnlyList<string> RecoveryCodes);

// =====================  DTOs: administración  =====================

public sealed class UserFilter : PaginationParams
{
    public string? Search { get; init; }
    public bool? IsActive { get; init; }
    public string? Role { get; init; }
}

public sealed record UserSummaryDto(Guid Id, string Email, string FullName, bool IsActive, bool EmailConfirmed,
    bool IsLockedOut, IReadOnlyList<string> Roles);

public sealed record UserDetailDto(Guid Id, string Email, string? FirstName, string? LastName, string? PhoneNumber,
    bool IsActive, bool EmailConfirmed, bool TwoFactorEnabled, DateTimeOffset? LockoutEnd, int AccessFailedCount,
    DateTime CreatedAt, DateTime? LastLoginAt, IReadOnlyList<string> Roles);

public sealed record CreateUserRequest(string Email, string? FirstName, string? LastName, IReadOnlyList<string> Roles);
public sealed record LockUserRequest(DateTimeOffset? Until);
public sealed record SetRolesRequest(IReadOnlyList<string> Roles);

public sealed record RoleDto(Guid Id, string Name, string? Description, int UserCount, bool IsSystem);
public sealed record RoleDetailDto(Guid Id, string Name, string? Description, bool IsSystem, IReadOnlyList<string> Permissions);
public sealed record CreateRoleRequest(string Name, string? Description, IReadOnlyList<string> Permissions);
public sealed record UpdateRoleRequest(string Name, string? Description);
public sealed record SetPermissionsRequest(IReadOnlyList<string> Permissions);

// =====================  Errores  =====================

public static class AuthErrors
{
    public static readonly Error InvalidCredentials = Error.Unauthorized("Auth.InvalidCredentials", "Email o contraseña incorrectos.");
    public static readonly Error LockedOut = Error.Forbidden("Auth.LockedOut", "Cuenta bloqueada temporalmente. Intente más tarde.");
    public static readonly Error AccountDisabled = Error.Forbidden("Auth.AccountDisabled", "La cuenta está desactivada.");
    public static readonly Error EmailNotConfirmed = Error.Forbidden("Auth.EmailNotConfirmed", "Debe confirmar su email antes de iniciar sesión.");
    public static readonly Error InvalidTwoFactorToken = Error.Unauthorized("Auth.InvalidTwoFactorToken", "La sesión de verificación expiró. Inicie sesión de nuevo.");
    public static readonly Error InvalidTwoFactorCode = Error.Unauthorized("Auth.InvalidTwoFactorCode", "Código de verificación inválido.");
    public static readonly Error InvalidRefreshToken = Error.Unauthorized("Auth.InvalidRefreshToken", "Refresh token inválido o expirado.");
    public static readonly Error InvalidToken = Error.Validation("Auth.InvalidToken", "El enlace es inválido o expiró.");
    public static readonly Error InvalidPassword = Error.Validation("Auth.InvalidPassword", "La contraseña actual es incorrecta.");
    public static readonly Error EmailInUse = Error.Conflict("Auth.EmailInUse", "Ese email ya está en uso.");
    public static readonly Error TwoFactorNotEnabled = Error.Validation("Auth.TwoFactorNotEnabled", "La verificación en dos pasos no está activa.");
    public static readonly Error TwoFactorAlreadyEnabled = Error.Conflict("Auth.TwoFactorAlreadyEnabled", "La verificación en dos pasos ya está activa.");
    public static readonly Error AuthenticatorNotConfigured = Error.Validation("Auth.AuthenticatorNotConfigured", "Primero configure la app autenticadora (/2fa/setup).");
    public static readonly Error NotAuthenticated = Error.Unauthorized("Auth.NotAuthenticated", "No autenticado.");
    public static readonly Error SessionNotFound = Error.NotFound("Auth.SessionNotFound", "Sesión no encontrada.");
}

public static class UserAdminErrors
{
    public static Error NotFound(Guid id) => Error.NotFound("User.NotFound", $"No existe el usuario {id}.");
    public static Error RolesNotFound(IEnumerable<string> roles) => Error.Validation("User.RolesNotFound", $"Roles inexistentes: {string.Join(", ", roles)}.");
    public static readonly Error CannotModifySelf = Error.Forbidden("User.CannotModifySelf", "No puede aplicar esta acción sobre su propia cuenta.");
}

public static class RoleErrors
{
    public static Error NotFound(Guid id) => Error.NotFound("Role.NotFound", $"No existe el rol {id}.");
    public static Error NameInUse(string name) => Error.Conflict("Role.NameInUse", $"Ya existe el rol '{name}'.");
    public static Error UnknownPermissions(IEnumerable<string> p) => Error.Validation("Role.UnknownPermissions", $"Permisos inexistentes: {string.Join(", ", p)}.");
    public static readonly Error SystemRole = Error.Forbidden("Role.SystemRole", "Los roles de sistema no se pueden renombrar ni eliminar.");
    public static readonly Error HasUsers = Error.Conflict("Role.HasUsers", "El rol tiene usuarios asignados.");
}

// =====================  Servicios  =====================

public interface IAuthService
{
    Task<Result> RegisterAsync(RegisterRequest request, CancellationToken ct);
    Task<Result> ConfirmEmailAsync(ConfirmEmailRequest request, CancellationToken ct);
    Task<Result> ResendConfirmationAsync(EmailRequest request, CancellationToken ct);
    Task<Result<LoginResponse>> LoginAsync(LoginRequest request, CancellationToken ct);
    Task<Result<AuthTokens>> LoginWithTwoFactorAsync(TwoFactorLoginRequest request, CancellationToken ct);
    Task<Result<AuthTokens>> RefreshAsync(RefreshTokenRequest request, CancellationToken ct);
    Task<Result> LogoutAsync(RefreshTokenRequest request, CancellationToken ct);
    Task<Result> ForgotPasswordAsync(EmailRequest request, CancellationToken ct);
    Task<Result> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct);
    Task<Result> ConfirmEmailChangeAsync(ConfirmEmailChangeRequest request, CancellationToken ct);
}

/// <summary>Operaciones del usuario autenticado sobre su propia cuenta.</summary>
public interface IAccountService
{
    Task<Result<UserProfileDto>> GetProfileAsync(CancellationToken ct);
    Task<Result<UserProfileDto>> UpdateProfileAsync(UpdateProfileRequest request, CancellationToken ct);
    Task<Result<AuthTokens>> ChangePasswordAsync(ChangePasswordRequest request, CancellationToken ct);
    Task<Result> RequestEmailChangeAsync(ChangeEmailRequest request, CancellationToken ct);
    Task<Result<IReadOnlyList<SessionDto>>> GetSessionsAsync(CancellationToken ct);
    Task<Result> RevokeSessionAsync(Guid sessionId, CancellationToken ct);
    Task<Result> LogoutAllAsync(CancellationToken ct);
    Task<Result<TwoFactorStatusDto>> GetTwoFactorStatusAsync(CancellationToken ct);
    Task<Result<TwoFactorSetupDto>> SetupAuthenticatorAsync(CancellationToken ct);
    Task<Result<RecoveryCodesDto>> EnableTwoFactorAsync(TwoFactorCodeRequest request, CancellationToken ct);
    Task<Result> DisableTwoFactorAsync(DisableTwoFactorRequest request, CancellationToken ct);
    Task<Result<RecoveryCodesDto>> RegenerateRecoveryCodesAsync(CancellationToken ct);
}

public interface IUserAdminService
{
    Task<Result<PagedResult<UserSummaryDto>>> GetPagedAsync(UserFilter filter, CancellationToken ct);
    Task<Result<UserDetailDto>> GetByIdAsync(Guid id, CancellationToken ct);
    Task<Result<UserDetailDto>> CreateAsync(CreateUserRequest request, CancellationToken ct);
    Task<Result> LockAsync(Guid id, LockUserRequest request, CancellationToken ct);
    Task<Result> UnlockAsync(Guid id, CancellationToken ct);
    Task<Result> SetActiveAsync(Guid id, bool isActive, CancellationToken ct);
    Task<Result> SetRolesAsync(Guid id, SetRolesRequest request, CancellationToken ct);
    Task<Result> SendPasswordResetAsync(Guid id, CancellationToken ct);
    Task<Result> RevokeSessionsAsync(Guid id, CancellationToken ct);
}

public interface IRoleService
{
    Task<Result<IReadOnlyList<RoleDto>>> GetAllAsync(CancellationToken ct);
    Task<Result<RoleDetailDto>> GetByIdAsync(Guid id, CancellationToken ct);
    Task<Result<RoleDetailDto>> CreateAsync(CreateRoleRequest request, CancellationToken ct);
    Task<Result<RoleDetailDto>> UpdateAsync(Guid id, UpdateRoleRequest request, CancellationToken ct);
    Task<Result> DeleteAsync(Guid id, CancellationToken ct);
    Task<Result<RoleDetailDto>> SetPermissionsAsync(Guid id, SetPermissionsRequest request, CancellationToken ct);
    IReadOnlyList<string> GetAvailablePermissions();
}
