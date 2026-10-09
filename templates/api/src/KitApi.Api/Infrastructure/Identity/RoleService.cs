using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using KitApi.Application.Abstractions.Persistence;
using KitApi.Application.Common.Results;
using KitApi.Application.Common.Security;
using KitApi.Application.Features.Auth;
using KitApi.Infrastructure.Persistence;

namespace KitApi.Infrastructure.Identity;

public sealed class RoleService(RoleManager<AppRole> roleManager, AppDbContext db, IUnitOfWork uow) : IRoleService
{
    public async Task<Result<IReadOnlyList<RoleDto>>> GetAllAsync(CancellationToken ct)
    {
        var rows = await db.Roles.AsNoTracking()
            .OrderBy(r => r.Name)
            .Select(r => new { r.Id, r.Name, r.Description, UserCount = db.UserRoles.Count(ur => ur.RoleId == r.Id) })
            .ToListAsync(ct);

        IReadOnlyList<RoleDto> result = rows
            .Select(r => new RoleDto(r.Id, r.Name!, r.Description, r.UserCount, IsSystem(r.Name)))
            .ToList();
        return Result.Success(result);
    }

    public async Task<Result<RoleDetailDto>> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var role = await roleManager.FindByIdAsync(id.ToString());
        return role is null ? RoleErrors.NotFound(id) : await ToDetailAsync(role);
    }

    public async Task<Result<RoleDetailDto>> CreateAsync(CreateRoleRequest request, CancellationToken ct)
    {
        var unknown = request.Permissions.Except(Permissions.All).ToList();
        if (unknown.Count > 0) return RoleErrors.UnknownPermissions(unknown);

        var name = request.Name.Trim();
        if (await roleManager.RoleExistsAsync(name)) return RoleErrors.NameInUse(name);

        var role = new AppRole(name) { Description = request.Description?.Trim() };

        var result = await uow.ExecuteInTransactionAsync(async _ =>
        {
            var created = await roleManager.CreateAsync(role);
            if (!created.Succeeded) return Result.Failure(Error.FromIdentity(created.Errors));

            foreach (var permission in request.Permissions.Distinct())
            {
                var added = await roleManager.AddClaimAsync(role, new Claim(Permissions.ClaimType, permission));
                if (!added.Succeeded) return Result.Failure(Error.FromIdentity(added.Errors));
            }
            return Result.Success();
        }, ct);

        return result.IsFailure ? result.Error : await ToDetailAsync(role);
    }

    public async Task<Result<RoleDetailDto>> UpdateAsync(Guid id, UpdateRoleRequest request, CancellationToken ct)
    {
        var role = await roleManager.FindByIdAsync(id.ToString());
        if (role is null) return RoleErrors.NotFound(id);

        var newName = request.Name.Trim();
        var renaming = !string.Equals(role.Name, newName, StringComparison.OrdinalIgnoreCase);

        if (renaming && IsSystem(role.Name)) return RoleErrors.SystemRole;
        if (renaming && await roleManager.RoleExistsAsync(newName)) return RoleErrors.NameInUse(newName);

        role.Name = newName;
        role.Description = request.Description?.Trim();
        var result = await roleManager.UpdateAsync(role);
        return result.Succeeded ? await ToDetailAsync(role) : Error.FromIdentity(result.Errors);
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct)
    {
        var role = await roleManager.FindByIdAsync(id.ToString());
        if (role is null) return RoleErrors.NotFound(id);
        if (IsSystem(role.Name)) return RoleErrors.SystemRole;
        if (await db.UserRoles.AnyAsync(ur => ur.RoleId == id, ct)) return RoleErrors.HasUsers;

        var result = await roleManager.DeleteAsync(role);
        return result.Succeeded ? Result.Success() : Error.FromIdentity(result.Errors);
    }

    public async Task<Result<RoleDetailDto>> SetPermissionsAsync(Guid id, SetPermissionsRequest request, CancellationToken ct)
    {
        var role = await roleManager.FindByIdAsync(id.ToString());
        if (role is null) return RoleErrors.NotFound(id);

        var target = request.Permissions.Distinct().ToList();
        var unknown = target.Except(Permissions.All).ToList();
        if (unknown.Count > 0) return RoleErrors.UnknownPermissions(unknown);

        var currentClaims = (await roleManager.GetClaimsAsync(role)).Where(c => c.Type == Permissions.ClaimType).ToList();
        var toRemove = currentClaims.Where(c => !target.Contains(c.Value)).ToList();
        var toAdd = target.Except(currentClaims.Select(c => c.Value)).ToList();

        // Los usuarios del rol reciben los cambios en su próximo refresh de token.
        var result = await uow.ExecuteInTransactionAsync(async _ =>
        {
            foreach (var claim in toRemove)
            {
                var removed = await roleManager.RemoveClaimAsync(role, claim);
                if (!removed.Succeeded) return Result.Failure(Error.FromIdentity(removed.Errors));
            }
            foreach (var permission in toAdd)
            {
                var added = await roleManager.AddClaimAsync(role, new Claim(Permissions.ClaimType, permission));
                if (!added.Succeeded) return Result.Failure(Error.FromIdentity(added.Errors));
            }
            return Result.Success();
        }, ct);

        return result.IsFailure ? result.Error : await ToDetailAsync(role);
    }

    public IReadOnlyList<string> GetAvailablePermissions() => Permissions.All;

    private static bool IsSystem(string? roleName)
        => roleName is not null && AppRoles.System.Contains(roleName, StringComparer.OrdinalIgnoreCase);

    private async Task<RoleDetailDto> ToDetailAsync(AppRole role)
    {
        var permissions = (await roleManager.GetClaimsAsync(role))
            .Where(c => c.Type == Permissions.ClaimType)
            .Select(c => c.Value)
            .OrderBy(v => v)
            .ToList();
        return new RoleDetailDto(role.Id, role.Name!, role.Description, IsSystem(role.Name), permissions);
    }
}
