using KitApi.Application.Abstractions.Services;
using KitApi.Application.Common.Security;
using KitApi.Domain.Common;

namespace KitApi.Infrastructure.Identity;

public sealed class CurrentUserService(IHttpContextAccessor accessor) : ICurrentUserService
{
    private HttpContext? Context => accessor.HttpContext;

    public EntityKey? UserId => EntityKeys.TryParse(Context?.User.FindFirst(ClaimNames.Subject)?.Value, out var id) ? id : null;
    public string? Email => Context?.User.FindFirst(ClaimNames.Email)?.Value;
    public bool IsAuthenticated => Context?.User.Identity?.IsAuthenticated ?? false;
    public bool IsInRole(string role) => Context?.User.IsInRole(role) ?? false;
    public bool HasPermission(string permission)
        => IsInRole(AppRoles.Admin) || (Context?.User.HasClaim(Permissions.ClaimType, permission) ?? false);
    public string? IpAddress => Context?.Connection.RemoteIpAddress?.ToString();
    public string? UserAgent => Context?.Request.Headers.UserAgent.ToString();
}
