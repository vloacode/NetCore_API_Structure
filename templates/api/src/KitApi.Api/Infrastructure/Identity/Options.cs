using System.ComponentModel.DataAnnotations;

namespace KitApi.Infrastructure.Identity;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required] public string Issuer { get; init; } = string.Empty;
    [Required] public string Audience { get; init; } = string.Empty;

    /// <summary>Mínimo 32 caracteres. Va en user-secrets / variables de entorno / Key Vault, NUNCA en appsettings.json.</summary>
    [Required, MinLength(32)] public string SigningKey { get; init; } = string.Empty;

    [Range(1, 120)] public int AccessTokenMinutes { get; init; } = 15;
    [Range(1, 90)] public int RefreshTokenDays { get; init; } = 7;
    [Range(1, 15)] public int TwoFactorChallengeMinutes { get; init; } = 5;

    /// <summary>Audiencia exclusiva del token intermedio de 2FA (no sirve como access token).</summary>
    public string TwoFactorAudience => $"{Audience}:2fa";
}

public sealed class AppUrlOptions
{
    public const string SectionName = "App";

    /// <summary>URL del frontend; los enlaces de los emails apuntan aquí.</summary>
    [Required, Url] public string ClientUrl { get; init; } = string.Empty;

    /// <summary>Nombre mostrado en apps autenticadoras (Google/Microsoft Authenticator).</summary>
    [Required] public string AppName { get; init; } = "KitApi";
}

public sealed class SeedOptions
{
    public const string SectionName = "Seed";

    public string? AdminEmail { get; init; }
    public string? AdminPassword { get; init; }   // user-secrets
}
