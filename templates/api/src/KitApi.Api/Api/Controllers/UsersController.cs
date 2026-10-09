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

    [HttpGet("{id:guid}"), HasPermission(Permissions.Users.Read)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => HandleResult(await users.GetByIdAsync(id, ct));

    [HttpPost, HasPermission(Permissions.Users.Manage)]
    public async Task<IActionResult> Create(CreateUserRequest request, CancellationToken ct)
        => HandleCreated(await users.CreateAsync(request, ct), nameof(GetById), u => new { id = u.Id });

    [HttpPost("{id:guid}/lock"), HasPermission(Permissions.Users.Manage)]
    public async Task<IActionResult> Lock(Guid id, LockUserRequest request, CancellationToken ct)
        => HandleResult(await users.LockAsync(id, request, ct));

    [HttpPost("{id:guid}/unlock"), HasPermission(Permissions.Users.Manage)]
    public async Task<IActionResult> Unlock(Guid id, CancellationToken ct)
        => HandleResult(await users.UnlockAsync(id, ct));

    [HttpPost("{id:guid}/activate"), HasPermission(Permissions.Users.Manage)]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct)
        => HandleResult(await users.SetActiveAsync(id, true, ct));

    [HttpPost("{id:guid}/deactivate"), HasPermission(Permissions.Users.Manage)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
        => HandleResult(await users.SetActiveAsync(id, false, ct));

    [HttpPut("{id:guid}/roles"), HasPermission(Permissions.Users.Manage)]
    public async Task<IActionResult> SetRoles(Guid id, SetRolesRequest request, CancellationToken ct)
        => HandleResult(await users.SetRolesAsync(id, request, ct));

    [HttpPost("{id:guid}/send-password-reset"), HasPermission(Permissions.Users.Manage)]
    public async Task<IActionResult> SendPasswordReset(Guid id, CancellationToken ct)
        => HandleResult(await users.SendPasswordResetAsync(id, ct));

    [HttpPost("{id:guid}/revoke-sessions"), HasPermission(Permissions.Users.Manage)]
    public async Task<IActionResult> RevokeSessions(Guid id, CancellationToken ct)
        => HandleResult(await users.RevokeSessionsAsync(id, ct));
}
