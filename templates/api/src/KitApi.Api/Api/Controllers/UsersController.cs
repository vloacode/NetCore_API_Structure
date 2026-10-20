using Microsoft.AspNetCore.Mvc;
using KitApi.Api.Authorization;
using KitApi.Application.Common.Security;
using KitApi.Application.Features.Auth;

namespace KitApi.Api.Controllers;

public sealed class UsersController(IUserAdminService users) : ApiControllerBase
{
    [HttpGet, HasPermission(Permissions.Users.Read)]
    public async Task<IActionResult> GetPaged([FromQuery] UserFilter filter, CancellationToken ct)
        => HandleResult(await users.GetPagedAsync(filter, ct));

    [HttpGet("{id}"), HasPermission(Permissions.Users.Read)]
    public async Task<IActionResult> GetById(EntityKey id, CancellationToken ct)
        => HandleResult(await users.GetByIdAsync(id, ct));

    [HttpPost, HasPermission(Permissions.Users.Manage)]
    public async Task<IActionResult> Create(CreateUserRequest request, CancellationToken ct)
        => HandleCreated(await users.CreateAsync(request, ct), nameof(GetById), u => new { id = u.Id });

    [HttpPost("{id}/lock"), HasPermission(Permissions.Users.Manage)]
    public async Task<IActionResult> Lock(EntityKey id, LockUserRequest request, CancellationToken ct)
        => HandleResult(await users.LockAsync(id, request, ct));

    [HttpPost("{id}/unlock"), HasPermission(Permissions.Users.Manage)]
    public async Task<IActionResult> Unlock(EntityKey id, CancellationToken ct)
        => HandleResult(await users.UnlockAsync(id, ct));

    [HttpPost("{id}/activate"), HasPermission(Permissions.Users.Manage)]
    public async Task<IActionResult> Activate(EntityKey id, CancellationToken ct)
        => HandleResult(await users.SetActiveAsync(id, true, ct));

    [HttpPost("{id}/deactivate"), HasPermission(Permissions.Users.Manage)]
    public async Task<IActionResult> Deactivate(EntityKey id, CancellationToken ct)
        => HandleResult(await users.SetActiveAsync(id, false, ct));

    [HttpPut("{id}/roles"), HasPermission(Permissions.Users.Manage)]
    public async Task<IActionResult> SetRoles(EntityKey id, SetRolesRequest request, CancellationToken ct)
        => HandleResult(await users.SetRolesAsync(id, request, ct));

    [HttpPost("{id}/send-password-reset"), HasPermission(Permissions.Users.Manage)]
    public async Task<IActionResult> SendPasswordReset(EntityKey id, CancellationToken ct)
        => HandleResult(await users.SendPasswordResetAsync(id, ct));

    [HttpPost("{id}/revoke-sessions"), HasPermission(Permissions.Users.Manage)]
    public async Task<IActionResult> RevokeSessions(EntityKey id, CancellationToken ct)
        => HandleResult(await users.RevokeSessionsAsync(id, ct));
}
