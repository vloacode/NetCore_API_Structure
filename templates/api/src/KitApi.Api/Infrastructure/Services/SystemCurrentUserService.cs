using KitApi.Application.Abstractions.Services;

namespace KitApi.Infrastructure.Services;

/// <summary>API sin autenticación: no hay usuario. La auditoría registra fechas y deja *By en null.</summary>
public sealed class SystemCurrentUserService(IHttpContextAccessor accessor) : ICurrentUserService
{
    public Guid? UserId => null;
    public string? Email => null;
    public bool IsAuthenticated => false;
    public bool IsInRole(string role) => false;
    public bool HasPermission(string permission) => false;
    public string? IpAddress => accessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
    public string? UserAgent => accessor.HttpContext?.Request.Headers.UserAgent.ToString();
}
