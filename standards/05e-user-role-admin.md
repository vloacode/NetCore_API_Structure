# Autenticación: administración de usuarios y roles

> **Aplica a:** Solo perfil con seguridad  
> **Propósito:** UserAdminService y RoleService.  
> Índice general: `standards/00-INDEX.md`

### `UserAdminService` — `Infrastructure/Identity/UserAdminService.cs`
```csharp
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using {Project}.Application.Abstractions.Persistence;
using {Project}.Application.Abstractions.Services;
using {Project}.Application.Common.Paging;
using {Project}.Application.Common.Results;
using {Project}.Application.Common.Security;
using {Project}.Application.Features.Auth;
using {Project}.Infrastructure.Persistence;

namespace {Project}.Infrastructure.Identity;

public sealed class UserAdminService(
    UserManager<AppUser> userManager,
    RoleManager<AppRole> roleManager,
    AppDbContext db,
    IUnitOfWork uow,
    SessionManager sessions,
    AccountEmails emails,
    ICurrentUserService currentUser,
    TimeProvider clock) : IUserAdminService
{
    public async Task<Result<PagedResult<UserSummaryDto>>> GetPagedAsync(UserFilter filter, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var query = db.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var s = filter.Search.Trim();
            query = query.Where(u => u.Email!.Contains(s) || u.FirstName!.Contains(s) || u.LastName!.Contains(s));
        }

        if (filter.IsActive.HasValue)
            query = query.Where(u => u.IsActive == filter.IsActive);

        if (!string.IsNullOrWhiteSpace(filter.Role))
        {
            var role = filter.Role.Trim().ToUpperInvariant();
            query = query.Where(u => db.UserRoles.Any(ur => ur.UserId == u.Id &&
                                     db.Roles.Any(r => r.Id == ur.RoleId && r.NormalizedName == role)));
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(u => u.Email).ThenBy(u => u.Id)
            .Skip(filter.Skip).Take(filter.PageSize)
            .Select(u => new UserSummaryDto(
                u.Id,
                u.Email!,
                ((u.FirstName ?? "") + " " + (u.LastName ?? "")).Trim(),
                u.IsActive,
                u.EmailConfirmed,
                u.LockoutEnd != null && u.LockoutEnd > now,
                (from ur in db.UserRoles join r in db.Roles on ur.RoleId equals r.Id
                 where ur.UserId == u.Id select r.Name!).ToList()))
            .ToListAsync(ct);

        return new PagedResult<UserSummaryDto>(items, filter.PageNumber, filter.PageSize, total);
    }

    public async Task<Result<UserDetailDto>> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(id.ToString());
        return user is null ? UserAdminErrors.NotFound(id) : await ToDetailAsync(user);
    }

    public async Task<Result<UserDetailDto>> CreateAsync(CreateUserRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim();
        if (await userManager.FindByEmailAsync(email) is not null)
            return AuthErrors.EmailInUse;

        var roles = request.Roles.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var missing = await MissingRolesAsync(roles);
        if (missing.Count > 0)
            return UserAdminErrors.RolesNotFound(missing);

        var user = new AppUser
        {
            UserName = email,
            Email = email,
            FirstName = request.FirstName?.Trim(),
            LastName = request.LastName?.Trim(),
            CreatedAt = clock.GetUtcNow().UtcDateTime
        };

        // Usuario + roles en una transacción (UserManager comparte el DbContext del UnitOfWork).
        var created = await uow.ExecuteInTransactionAsync(async _ =>
        {
            var result = await userManager.CreateAsync(user);   // sin contraseña: la define el usuario
            if (!result.Succeeded) return Result.Failure(Error.FromIdentity(result.Errors));

            if (roles.Count > 0)
            {
                result = await userManager.AddToRolesAsync(user, roles);
                if (!result.Succeeded) return Result.Failure(Error.FromIdentity(result.Errors));
            }
            return Result.Success();
        }, ct);

        if (created.IsFailure)
            return created.Error;

        // Efecto externo FUERA de la transacción.
        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        await emails.SendSetPasswordAsync(user, token, ct);

        return await ToDetailAsync(user);
    }

    public async Task<Result> LockAsync(Guid id, LockUserRequest request, CancellationToken ct)
    {
        if (id == currentUser.UserId) return UserAdminErrors.CannotModifySelf;
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null) return UserAdminErrors.NotFound(id);

        await userManager.SetLockoutEnabledAsync(user, true);
        await userManager.SetLockoutEndDateAsync(user, request.Until ?? DateTimeOffset.MaxValue);
        await sessions.RevokeAllAsync(user.Id, "Locked by admin", ct);
        return Result.Success();
    }

    public async Task<Result> UnlockAsync(Guid id, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null) return UserAdminErrors.NotFound(id);

        await userManager.SetLockoutEndDateAsync(user, null);
        await userManager.ResetAccessFailedCountAsync(user);
        return Result.Success();
    }

    public async Task<Result> SetActiveAsync(Guid id, bool isActive, CancellationToken ct)
    {
        if (!isActive && id == currentUser.UserId) return UserAdminErrors.CannotModifySelf;
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null) return UserAdminErrors.NotFound(id);

        user.IsActive = isActive;
        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded) return Error.FromIdentity(result.Errors);

        if (!isActive)
            await sessions.RevokeAllAsync(user.Id, "Deactivated by admin", ct);
        return Result.Success();
    }

    public async Task<Result> SetRolesAsync(Guid id, SetRolesRequest request, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null) return UserAdminErrors.NotFound(id);

        var target = request.Roles.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var missing = await MissingRolesAsync(target);
        if (missing.Count > 0) return UserAdminErrors.RolesNotFound(missing);

        // Un admin no puede quitarse a sí mismo el rol Admin (evita quedarse sin administradores).
        if (id == currentUser.UserId && !target.Contains(AppRoles.Admin, StringComparer.OrdinalIgnoreCase))
            return UserAdminErrors.CannotModifySelf;

        var current = await userManager.GetRolesAsync(user);
        var toRemove = current.Except(target, StringComparer.OrdinalIgnoreCase).ToList();
        var toAdd = target.Except(current, StringComparer.OrdinalIgnoreCase).ToList();

        // Los nuevos permisos llegan al usuario en su próximo refresh de token.
        return await uow.ExecuteInTransactionAsync(async _ =>
        {
            if (toRemove.Count > 0)
            {
                var removed = await userManager.RemoveFromRolesAsync(user, toRemove);
                if (!removed.Succeeded) return Result.Failure(Error.FromIdentity(removed.Errors));
            }
            if (toAdd.Count > 0)
            {
                var added = await userManager.AddToRolesAsync(user, toAdd);
                if (!added.Succeeded) return Result.Failure(Error.FromIdentity(added.Errors));
            }
            return Result.Success();
        }, ct);
    }

    public async Task<Result> SendPasswordResetAsync(Guid id, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null) return UserAdminErrors.NotFound(id);

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        await emails.SendPasswordResetAsync(user, token, ct);
        return Result.Success();
    }

    public async Task<Result> RevokeSessionsAsync(Guid id, CancellationToken ct)
    {
        if (await userManager.FindByIdAsync(id.ToString()) is null) return UserAdminErrors.NotFound(id);
        await sessions.RevokeAllAsync(id, "Revoked by admin", ct);
        return Result.Success();
    }

    private async Task<List<string>> MissingRolesAsync(IEnumerable<string> roles)
    {
        var missing = new List<string>();
        foreach (var role in roles)
            if (!await roleManager.RoleExistsAsync(role)) missing.Add(role);
        return missing;
    }

    private async Task<UserDetailDto> ToDetailAsync(AppUser user)
    {
        var roles = await userManager.GetRolesAsync(user);
        return new UserDetailDto(user.Id, user.Email!, user.FirstName, user.LastName, user.PhoneNumber,
            user.IsActive, user.EmailConfirmed, user.TwoFactorEnabled, user.LockoutEnd, user.AccessFailedCount,
            user.CreatedAt, user.LastLoginAt, roles.ToList());
    }
}
```

### `RoleService` — `Infrastructure/Identity/RoleService.cs`
```csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using {Project}.Application.Abstractions.Persistence;
using {Project}.Application.Common.Results;
using {Project}.Application.Common.Security;
using {Project}.Application.Features.Auth;
using {Project}.Infrastructure.Persistence;

namespace {Project}.Infrastructure.Identity;

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
```
