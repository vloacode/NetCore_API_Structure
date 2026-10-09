# Autorización por permisos

> **Aplica a:** Solo perfil con seguridad  
> **Propósito:** Catálogo de roles y permisos, atributo HasPermission y proveedor de políticas.  
> Índice general: `standards/00-INDEX.md`

## Autorización por permisos

### Catálogo — `Application/Common/Security/Permissions.cs`
```csharp
using System.Reflection;

namespace {Project}.Application.Common.Security;

public static class AppRoles
{
    public const string Admin = "Admin";
    public const string User = "User";

    /// <summary>Roles de sistema: no se pueden renombrar ni eliminar.</summary>
    public static readonly IReadOnlyList<string> System = [Admin, User];
}

/// <summary>
/// Catálogo de permisos. Se guardan como claims "permission" en el ROL (AspNetRoleClaims)
/// y viajan en el access token. Agregar una clase anidada por cada entidad de negocio.
/// </summary>
public static class Permissions
{
    public const string ClaimType = "permission";

    // ---- Fijos (Auth) ----
    public static class Users
    {
        public const string Read = "users.read";
        public const string Manage = "users.manage";
    }

    public static class Roles
    {
        public const string Read = "roles.read";
        public const string Manage = "roles.manage";
    }

    // ---- Plantilla por entidad de negocio (standards/07) ----
    // public static class {Entities}
    // {
    //     public const string Read = "{entities}.read";
    //     public const string Write = "{entities}.write";
    //     public const string Delete = "{entities}.delete";
    // }

    /// <summary>Todos los permisos declarados arriba (por reflexión).</summary>
    public static IReadOnlyList<string> All { get; } = typeof(Permissions)
        .GetNestedTypes(BindingFlags.Public | BindingFlags.Static)
        .SelectMany(t => t.GetFields(BindingFlags.Public | BindingFlags.Static))
        .Where(f => f.IsLiteral && f.FieldType == typeof(string))
        .Select(f => (string)f.GetRawConstantValue()!)
        .ToList();

    /// <summary>Permisos iniciales del rol User (Admin pasa todas las políticas). Completar según el dominio.</summary>
    public static readonly IReadOnlyList<string> DefaultUserPermissions = [ /* {Entities}.Read, ... */ ];
}
```

### Atributo y proveedor de políticas — `Api/Authorization/`
```csharp
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using {Project}.Application.Common.Security;

namespace {Project}.Api.Authorization;

/// <summary>[HasPermission(Permissions.{Entities}.Read)] → política "permission:{entities}.read".</summary>
public sealed class HasPermissionAttribute(string permission) : AuthorizeAttribute(PolicyPrefix + permission)
{
    public const string PolicyPrefix = "permission:";
}

/// <summary>Crea las políticas de permiso al vuelo: no hay que registrar una política por permiso.</summary>
public sealed class PermissionPolicyProvider(IOptions<AuthorizationOptions> options) : DefaultAuthorizationPolicyProvider(options)
{
    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (!policyName.StartsWith(HasPermissionAttribute.PolicyPrefix, StringComparison.OrdinalIgnoreCase))
            return await base.GetPolicyAsync(policyName);

        var permission = policyName[HasPermissionAttribute.PolicyPrefix.Length..];

        return new AuthorizationPolicyBuilder(JwtBearerDefaults.AuthenticationScheme)
            .RequireAuthenticatedUser()
            .RequireAssertion(ctx => ctx.User.IsInRole(AppRoles.Admin)                 // Admin pasa todo
                                  || ctx.User.HasClaim(Permissions.ClaimType, permission))
            .Build();
    }
}
```

**Cómo fluye:**
1. Un permiso es un claim `permission` asociado a un **rol** (tabla `AspNetRoleClaims`).
2. Al hacer login o refresh se leen los roles del usuario y los permisos de esos roles, y se meten en el access token.
3. `[HasPermission(...)]` valida el claim.
4. Los cambios de roles o permisos aplican en el siguiente refresh (máximo `AccessTokenMinutes`). Para efecto inmediato, revocar las sesiones del usuario.
