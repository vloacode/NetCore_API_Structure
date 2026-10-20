using Microsoft.AspNetCore.Mvc;
using KitApi.Api.Authorization;
using KitApi.Application.Common.Security;
using KitApi.Application.Features.Auth;

namespace KitApi.Api.Controllers;

public sealed class RolesController(IRoleService roles) : ApiControllerBase
{
    [HttpGet, HasPermission(Permissions.Roles.Read)]
    public async Task<IActionResult> GetAll(CancellationToken ct) => HandleResult(await roles.GetAllAsync(ct));

    [HttpGet("permissions"), HasPermission(Permissions.Roles.Read)]
    public IActionResult GetAvailablePermissions() => Ok(roles.GetAvailablePermissions());

    [HttpGet("{id}"), HasPermission(Permissions.Roles.Read)]
    public async Task<IActionResult> GetById(EntityKey id, CancellationToken ct) => HandleResult(await roles.GetByIdAsync(id, ct));

    [HttpPost, HasPermission(Permissions.Roles.Manage)]
    public async Task<IActionResult> Create(CreateRoleRequest request, CancellationToken ct)
        => HandleCreated(await roles.CreateAsync(request, ct), nameof(GetById), r => new { id = r.Id });

    [HttpPut("{id}"), HasPermission(Permissions.Roles.Manage)]
    public async Task<IActionResult> Update(EntityKey id, UpdateRoleRequest request, CancellationToken ct)
        => HandleResult(await roles.UpdateAsync(id, request, ct));

    [HttpDelete("{id}"), HasPermission(Permissions.Roles.Manage)]
    public async Task<IActionResult> Delete(EntityKey id, CancellationToken ct) => HandleResult(await roles.DeleteAsync(id, ct));

    [HttpPut("{id}/permissions"), HasPermission(Permissions.Roles.Manage)]
    public async Task<IActionResult> SetPermissions(EntityKey id, SetPermissionsRequest request, CancellationToken ct)
        => HandleResult(await roles.SetPermissionsAsync(id, request, ct));
}
