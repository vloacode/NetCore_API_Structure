# Autenticación: visión general, entidades y opciones

> **Aplica a:** Solo perfil con seguridad (Security = enabled)  
> **Propósito:** Endpoints de Auth, flujo de tokens, entidades de Identity y opciones de configuración. Índice de los archivos 05a–05f.  
> Índice general: `standards/00-INDEX.md`

## Autenticación con Identity local + JWT

### Resumen de endpoints

| Método | Ruta | Auth | Descripción |
|---|---|---|---|
| POST | `/api/auth/register` | Anónimo | Crea usuario (rol `User`) y envía confirmación por email. No revela si el email ya existe. |
| POST | `/api/auth/confirm-email` | Anónimo | Confirma email con `userId` + `token`. |
| POST | `/api/auth/resend-confirmation` | Anónimo | Reenvía confirmación (respuesta siempre 204). |
| POST | `/api/auth/login` | Anónimo | Valida credenciales y lockout. Devuelve tokens o `requiresTwoFactor`. |
| POST | `/api/auth/login/2fa` | Anónimo | Completa login con código TOTP o código de recuperación. |
| POST | `/api/auth/refresh` | Anónimo | Rota el refresh token y devuelve un par nuevo. |
| POST | `/api/auth/logout` | Anónimo | Revoca la sesión del refresh token enviado. |
| POST | `/api/auth/forgot-password` | Anónimo | Envía enlace de reset (respuesta siempre 204). |
| POST | `/api/auth/reset-password` | Anónimo | Define nueva contraseña con el token y revoca todas las sesiones. |
| POST | `/api/auth/confirm-email-change` | Anónimo | Confirma el cambio de email (enlace enviado al email nuevo). |
| GET / PUT | `/api/account/me` | Usuario | Ver o editar perfil. |
| POST | `/api/account/change-password` | Usuario | Cambia contraseña, revoca sesiones y devuelve tokens nuevos. |
| POST | `/api/account/change-email` | Usuario | Solicita cambio de email (requiere contraseña). |
| GET | `/api/account/sessions` | Usuario | Lista sesiones o dispositivos activos. |
| DELETE | `/api/account/sessions/{id}` | Usuario | Revoca una sesión. |
| POST | `/api/account/logout-all` | Usuario | Revoca todas las sesiones. |
| GET | `/api/account/2fa` | Usuario | Estado de 2FA. |
| POST | `/api/account/2fa/setup` | Usuario | Genera clave y URI `otpauth://` para el QR. |
| POST | `/api/account/2fa/enable` | Usuario | Verifica código, activa 2FA y devuelve códigos de recuperación. |
| POST | `/api/account/2fa/disable` | Usuario | Desactiva 2FA (requiere contraseña). |
| POST | `/api/account/2fa/recovery-codes` | Usuario | Regenera códigos de recuperación. |
| GET | `/api/users` | `users.read` | Listado paginado con búsqueda, filtro por rol y estado. |
| GET | `/api/users/{id}` | `users.read` | Detalle. |
| POST | `/api/users` | `users.manage` | Crea usuario y le envía enlace para definir contraseña. |
| POST | `/api/users/{id}/lock` · `/unlock` | `users.manage` | Bloquea o desbloquea. |
| POST | `/api/users/{id}/activate` · `/deactivate` | `users.manage` | Activa o desactiva la cuenta. |
| PUT | `/api/users/{id}/roles` | `users.manage` | Reemplaza los roles. |
| POST | `/api/users/{id}/send-password-reset` | `users.manage` | Envía enlace de reset. |
| POST | `/api/users/{id}/revoke-sessions` | `users.manage` | Cierra todas sus sesiones. |
| GET | `/api/roles` · `/api/roles/{id}` · `/api/roles/permissions` | `roles.read` | Roles, detalle y catálogo de permisos. |
| POST / PUT / DELETE | `/api/roles` · `/api/roles/{id}` | `roles.manage` | CRUD de roles (los de sistema están protegidos). |
| PUT | `/api/roles/{id}/permissions` | `roles.manage` | Reemplaza los permisos del rol. |

**Flujo de tokens:**
- **Access token**: JWT firmado HS256, duración 15 min. Lleva los claims `sub`, `email`, `name`, `jti`, `role` (varios) y `permission` (varios).
- **Refresh token**: 64 bytes aleatorios, duración 7 días. En BD se guarda **solo su SHA-256**.
- **Rotación**: cada refresh revoca el token usado y crea uno nuevo de la misma *familia* (sesión).
- **Detección de reuso**: si llega un token ya rotado, se revoca la familia entera (posible robo).
- **2FA**: el login devuelve un JWT intermedio de 5 min con audiencia `{Audience}:2fa`. Ese token no sirve como access token y solo se canjea en `/login/2fa`.

### Entidades de Identity — `Infrastructure/Identity/IdentityEntities.cs`
```csharp
using Microsoft.AspNetCore.Identity;

namespace {Project}.Infrastructure.Identity;

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
```

### Opciones — `Infrastructure/Identity/Options.cs`
```csharp
using System.ComponentModel.DataAnnotations;

namespace {Project}.Infrastructure.Identity;

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
    [Required] public string AppName { get; init; } = "{Project}";
}

public sealed class SeedOptions
{
    public const string SectionName = "Seed";

    public string? AdminEmail { get; init; }
    public string? AdminPassword { get; init; }   // user-secrets
}
```
