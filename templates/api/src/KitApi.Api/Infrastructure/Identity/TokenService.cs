using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using KitApi.Application.Common.Security;

namespace KitApi.Infrastructure.Identity;

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
