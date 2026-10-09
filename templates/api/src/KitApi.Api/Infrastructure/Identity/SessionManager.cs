using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using KitApi.Application.Abstractions.Persistence;
using KitApi.Application.Abstractions.Services;
using KitApi.Application.Common.Results;
using KitApi.Application.Common.Security;
using KitApi.Application.Common.Specifications;
using KitApi.Application.Features.Auth;
using KitApi.Infrastructure.Persistence;

namespace KitApi.Infrastructure.Identity;

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
