# Autenticación: tokens, usuario actual, sesiones y emails

> **Aplica a:** Solo perfil con seguridad  
> **Propósito:** TokenService (JWT, 2FA, refresh), CurrentUserService, SessionManager (rotación/reuso) y AccountEmails.  
> Índice general: `standards/00-INDEX.md`

### `TokenService` — `Infrastructure/Identity/TokenService.cs`
```csharp
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using {Project}.Application.Common.Security;

namespace {Project}.Infrastructure.Identity;

public interface ITokenService
{
    (string Token, DateTime ExpiresAt) CreateAccessToken(AppUser user, IEnumerable<string> roles, IEnumerable<string> permissions);
    string CreateTwoFactorChallengeToken(Guid userId);
    Task<Guid?> ValidateTwoFactorChallengeTokenAsync(string token);
    string GenerateRefreshToken();
    string HashToken(string rawToken);
}

public sealed class TokenService(IOptions<JwtOptions> options, TimeProvider clock) : ITokenService
{
    private readonly JwtOptions _jwt = options.Value;
    private readonly JsonWebTokenHandler _handler = new();

    private SymmetricSecurityKey SigningKey => new(Encoding.UTF8.GetBytes(_jwt.SigningKey));

    public (string Token, DateTime ExpiresAt) CreateAccessToken(AppUser user, IEnumerable<string> roles, IEnumerable<string> permissions)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var expires = now.AddMinutes(_jwt.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Name, user.FullName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimNames.Role, r)));
        claims.AddRange(permissions.Distinct().Select(p => new Claim(Permissions.ClaimType, p)));

        var token = _handler.CreateToken(new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = _jwt.Issuer,
            Audience = _jwt.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = expires,
            SigningCredentials = new SigningCredentials(SigningKey, SecurityAlgorithms.HmacSha256)
        });

        return (token, expires);
    }

    public string CreateTwoFactorChallengeToken(Guid userId)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        return _handler.CreateToken(new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity([new Claim(JwtRegisteredClaimNames.Sub, userId.ToString())]),
            Issuer = _jwt.Issuer,
            Audience = _jwt.TwoFactorAudience,      // audiencia distinta: el JwtBearer de la API lo rechaza
            IssuedAt = now,
            NotBefore = now,
            Expires = now.AddMinutes(_jwt.TwoFactorChallengeMinutes),
            SigningCredentials = new SigningCredentials(SigningKey, SecurityAlgorithms.HmacSha256)
        });
    }

    public async Task<Guid?> ValidateTwoFactorChallengeTokenAsync(string token)
    {
        var result = await _handler.ValidateTokenAsync(token, new TokenValidationParameters
        {
            ValidIssuer = _jwt.Issuer,
            ValidAudience = _jwt.TwoFactorAudience,
            IssuerSigningKey = SigningKey,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        });

        if (!result.IsValid || !result.Claims.TryGetValue(JwtRegisteredClaimNames.Sub, out var sub))
            return null;

        return Guid.TryParse(sub?.ToString(), out var userId) ? userId : null;
    }

    public string GenerateRefreshToken() => Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(64));

    public string HashToken(string rawToken) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
}

/// <summary>Nombres de claims usados en toda la app (MapInboundClaims = false: se usan tal cual).</summary>
public static class ClaimNames
{
    public const string Subject = JwtRegisteredClaimNames.Sub;
    public const string Email = JwtRegisteredClaimNames.Email;
    public const string Name = JwtRegisteredClaimNames.Name;
    public const string Role = "role";
}
```

### Usuario actual — `Infrastructure/Identity/CurrentUserService.cs`
```csharp
using {Project}.Application.Abstractions.Services;
using {Project}.Application.Common.Security;

namespace {Project}.Infrastructure.Identity;

public sealed class CurrentUserService(IHttpContextAccessor accessor) : ICurrentUserService
{
    private HttpContext? Context => accessor.HttpContext;

    public Guid? UserId => Guid.TryParse(Context?.User.FindFirst(ClaimNames.Subject)?.Value, out var id) ? id : null;
    public string? Email => Context?.User.FindFirst(ClaimNames.Email)?.Value;
    public bool IsAuthenticated => Context?.User.Identity?.IsAuthenticated ?? false;
    public bool IsInRole(string role) => Context?.User.IsInRole(role) ?? false;
    public bool HasPermission(string permission)
        => IsInRole(AppRoles.Admin) || (Context?.User.HasClaim(Permissions.ClaimType, permission) ?? false);
    public string? IpAddress => Context?.Connection.RemoteIpAddress?.ToString();
    public string? UserAgent => Context?.Request.Headers.UserAgent.ToString();
}
```

### Gestión de sesiones — `Infrastructure/Identity/SessionManager.cs`
```csharp
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using {Project}.Application.Abstractions.Persistence;
using {Project}.Application.Abstractions.Services;
using {Project}.Application.Common.Results;
using {Project}.Application.Common.Security;
using {Project}.Application.Common.Specifications;
using {Project}.Application.Features.Auth;
using {Project}.Infrastructure.Persistence;

namespace {Project}.Infrastructure.Identity;

internal sealed class RefreshTokenByHashSpec : Specification<RefreshToken>
{
    public RefreshTokenByHashSpec(string hash) : base(t => t.TokenHash == hash) => EnableTracking();
}

internal sealed class ActiveRefreshTokensByUserSpec : Specification<RefreshToken>
{
    public ActiveRefreshTokensByUserSpec(Guid userId, DateTime now)
        : base(t => t.UserId == userId && t.RevokedAt == null && t.ExpiresAt > now)
        => OrderByDescending(t => t.CreatedAt);
}

/// <summary>
/// Emisión de tokens y ciclo de vida de sesiones (refresh tokens con rotación y detección de reuso).
/// Usa IUnitOfWork para RefreshToken y AppDbContext solo para LEER permisos de las tablas de Identity.
/// </summary>
public sealed class SessionManager(
    IUnitOfWork uow,
    AppDbContext db,
    UserManager<AppUser> userManager,
    ITokenService tokenService,
    ICurrentUserService currentUser,
    IOptions<JwtOptions> jwtOptions,
    TimeProvider clock)
{
    private readonly IRepository<RefreshToken> _tokens = uow.Repository<RefreshToken>();
    private readonly JwtOptions _jwt = jwtOptions.Value;

    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    /// <summary>Crea access + refresh token. familyId null = nueva sesión.</summary>
    public async Task<AuthTokens> IssueTokensAsync(AppUser user, Guid? familyId, CancellationToken ct)
    {
        var (accessToken, accessExpires, refresh) = await CreateTokenPairAsync(user, familyId ?? Guid.CreateVersion7(), ct);
        await uow.SaveChangesAsync(ct);
        return new AuthTokens(accessToken, accessExpires, refresh.Raw, refresh.Entity.ExpiresAt);
    }

    public async Task<Result<AuthTokens>> RotateAsync(string rawToken, CancellationToken ct)
    {
        var stored = await _tokens.FirstOrDefaultAsync(new RefreshTokenByHashSpec(tokenService.HashToken(rawToken)), ct);
        if (stored is null)
            return AuthErrors.InvalidRefreshToken;

        if (stored.RevokedAt is not null)
        {
            // Un token ya rotado se volvió a usar: posible robo. Se revoca toda la familia (sesión).
            if (stored.ReplacedByTokenHash is not null)
                await RevokeFamilyAsync(stored.UserId, stored.FamilyId, "Reuse detected", ct);
            return AuthErrors.InvalidRefreshToken;
        }

        if (stored.ExpiresAt <= Now)
            return AuthErrors.InvalidRefreshToken;

        var user = await userManager.FindByIdAsync(stored.UserId.ToString());
        if (user is null || !user.IsActive || await userManager.IsLockedOutAsync(user))
        {
            await RevokeFamilyAsync(stored.UserId, stored.FamilyId, "User not allowed", ct);
            return AuthErrors.InvalidRefreshToken;
        }

        // Roles/permisos se recalculan en cada refresh: cambios de rol aplican sin re-login.
        var (accessToken, accessExpires, refresh) = await CreateTokenPairAsync(user, stored.FamilyId, ct);

        stored.RevokedAt = Now;
        stored.RevokedReason = "Rotated";
        stored.ReplacedByTokenHash = refresh.Entity.TokenHash;

        await uow.SaveChangesAsync(ct);
        return new AuthTokens(accessToken, accessExpires, refresh.Raw, refresh.Entity.ExpiresAt);
    }

    /// <summary>Revoca la sesión a la que pertenece el refresh token (logout).</summary>
    public async Task RevokeByRawTokenAsync(string rawToken, CancellationToken ct)
    {
        var stored = await _tokens.FirstOrDefaultAsync(new RefreshTokenByHashSpec(tokenService.HashToken(rawToken)), ct);
        if (stored is not null)
            await RevokeFamilyAsync(stored.UserId, stored.FamilyId, "Logout", ct);
    }

    public Task<int> RevokeFamilyAsync(Guid userId, Guid familyId, string reason, CancellationToken ct)
    {
        var now = Now;
        return _tokens.ExecuteUpdateAsync(
            t => t.UserId == userId && t.FamilyId == familyId && t.RevokedAt == null,
            s => s.SetProperty(t => t.RevokedAt, now).SetProperty(t => t.RevokedReason, reason), ct);
    }

    public Task<int> RevokeAllAsync(Guid userId, string reason, CancellationToken ct)
    {
        var now = Now;
        return _tokens.ExecuteUpdateAsync(
            t => t.UserId == userId && t.RevokedAt == null,
            s => s.SetProperty(t => t.RevokedAt, now).SetProperty(t => t.RevokedReason, reason), ct);
    }

    public Task<IReadOnlyList<SessionDto>> GetActiveSessionsAsync(Guid userId, CancellationToken ct)
        => _tokens.ListAsync(new ActiveRefreshTokensByUserSpec(userId, Now),
            t => new SessionDto(t.FamilyId, t.CreatedAt, t.ExpiresAt, t.CreatedByIp, t.UserAgent), ct);

    public async Task<IReadOnlyList<string>> GetPermissionsAsync(IEnumerable<string> roleNames, CancellationToken ct)
    {
        var normalized = roleNames.Select(r => r.ToUpperInvariant()).ToList();
        return await (from rc in db.RoleClaims.AsNoTracking()
                      join r in db.Roles.AsNoTracking() on rc.RoleId equals r.Id
                      where normalized.Contains(r.NormalizedName!) && rc.ClaimType == Permissions.ClaimType
                      select rc.ClaimValue!)
                     .Distinct()
                     .ToListAsync(ct);
    }

    private async Task<(string AccessToken, DateTime AccessExpires, (string Raw, RefreshToken Entity) Refresh)> CreateTokenPairAsync(
        AppUser user, Guid familyId, CancellationToken ct)
    {
        var roles = await userManager.GetRolesAsync(user);
        var permissions = await GetPermissionsAsync(roles, ct);
        var (accessToken, accessExpires) = tokenService.CreateAccessToken(user, roles, permissions);

        var raw = tokenService.GenerateRefreshToken();
        var entity = new RefreshToken
        {
            UserId = user.Id,
            FamilyId = familyId,
            TokenHash = tokenService.HashToken(raw),
            CreatedAt = Now,
            ExpiresAt = Now.AddDays(_jwt.RefreshTokenDays),
            CreatedByIp = currentUser.IpAddress,
            UserAgent = currentUser.UserAgent is { Length: > 512 } ua ? ua[..512] : currentUser.UserAgent
        };
        _tokens.Add(entity);

        return (accessToken, accessExpires, (raw, entity));
    }
}
```

### Emails de cuenta — `Infrastructure/Identity/AccountEmails.cs`
```csharp
using System.Net;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using {Project}.Application.Abstractions.Services;

namespace {Project}.Infrastructure.Identity;

/// <summary>
/// Construye y envía los emails de cuenta. Los tokens de Identity se codifican Base64Url para viajar en URLs.
/// Los enlaces apuntan al FRONTEND (App:ClientUrl), que luego llama al endpoint de la API correspondiente.
/// </summary>
public sealed class AccountEmails(IEmailSender sender, IOptions<AppUrlOptions> options)
{
    private readonly AppUrlOptions _app = options.Value;

    public static string EncodeToken(string token) => WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

    public static string? DecodeToken(string encoded)
    {
        try { return Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(encoded)); }
        catch (FormatException) { return null; }
    }

    public Task SendConfirmationAsync(AppUser user, string token, CancellationToken ct)
        => Send(user.Email!, "Confirma tu cuenta",
            $"Confirma tu cuenta: {Link("confirm-email", ("userId", user.Id.ToString()), ("token", EncodeToken(token)))}", ct);

    public Task SendPasswordResetAsync(AppUser user, string token, CancellationToken ct)
        => Send(user.Email!, "Restablecer contraseña",
            $"Restablece tu contraseña: {Link("reset-password", ("email", user.Email!), ("token", EncodeToken(token)))}", ct);

    public Task SendSetPasswordAsync(AppUser user, string token, CancellationToken ct)
        => Send(user.Email!, "Tu cuenta fue creada",
            $"Un administrador creó tu cuenta. Define tu contraseña: {Link("reset-password", ("email", user.Email!), ("token", EncodeToken(token)))}", ct);

    public Task SendChangeEmailAsync(AppUser user, string newEmail, string token, CancellationToken ct)
        => Send(newEmail, "Confirma tu nuevo email",
            $"Confirma el cambio de email: {Link("confirm-email-change", ("userId", user.Id.ToString()), ("newEmail", newEmail), ("token", EncodeToken(token)))}", ct);

    public Task SendAccountExistsAsync(string email, CancellationToken ct)
        => Send(email, "Intento de registro",
            $"Alguien intentó registrarse con tu email, pero ya tienes cuenta. Si olvidaste tu contraseña: {Link("forgot-password")}", ct);

    public Task SendSecurityNoticeAsync(string email, string message, CancellationToken ct)
        => Send(email, "Aviso de seguridad", message, ct);

    private string Link(string path, params (string Key, string Value)[] query)
    {
        var url = $"{_app.ClientUrl.TrimEnd('/')}/{path}";
        return query.Length == 0 ? url : QueryHelpers.AddQueryString(url, query.ToDictionary(q => q.Key, q => (string?)q.Value));
    }

    private Task Send(string to, string subject, string body, CancellationToken ct)
        => sender.SendAsync(to, subject, WebUtility.HtmlEncode(body), ct);
}
```

`Infrastructure/Email/LoggingEmailSender.cs`
```csharp
using {Project}.Application.Abstractions.Services;

namespace {Project}.Infrastructure.Email;

/// <summary>
/// Implementación de DESARROLLO: escribe el email en el log (incluye el enlace con el token).
/// En producción registrar una implementación real (SMTP con MailKit, SendGrid, etc.) con la misma interfaz.
/// </summary>
public sealed class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        logger.LogInformation("EMAIL (dev) To: {To} | Subject: {Subject}\n{Body}", to, subject, htmlBody);
        return Task.CompletedTask;
    }
}
```
