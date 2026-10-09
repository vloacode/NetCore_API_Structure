using Microsoft.AspNetCore.Identity;

namespace KitApi.Infrastructure.Identity;

public class AppUser : IdentityUser<Guid>
{
    public AppUser() => Id = Guid.CreateVersion7();

    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public bool IsActive { get; set; } = true;          // desactivación administrativa (distinta del lockout)
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }

    public ICollection<RefreshToken> RefreshTokens { get; } = new List<RefreshToken>();

    public string FullName => $"{FirstName} {LastName}".Trim();
}

public class AppRole : IdentityRole<Guid>
{
    public AppRole() => Id = Guid.CreateVersion7();
    public AppRole(string name) : this() => Name = name;

    public string? Description { get; set; }
}

/// <summary>
/// Refresh token persistido. Solo se guarda el HASH (SHA-256), nunca el valor.
/// FamilyId agrupa la cadena de rotaciones de una misma sesión/dispositivo.
/// </summary>
public class RefreshToken
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;

    public string TokenHash { get; set; } = string.Empty;
    public Guid FamilyId { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? RevokedReason { get; set; }
    public string? ReplacedByTokenHash { get; set; }

    public string? CreatedByIp { get; set; }
    public string? UserAgent { get; set; }

    public bool IsActive(DateTime utcNow) => RevokedAt is null && ExpiresAt > utcNow;
}
