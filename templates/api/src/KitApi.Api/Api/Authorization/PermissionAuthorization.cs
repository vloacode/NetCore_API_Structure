using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using KitApi.Application.Common.Security;

namespace KitApi.Api.Authorization;

/// <summary>[HasPermission(Permissions.Users.Read)] → política "permission:users.read".</summary>
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
