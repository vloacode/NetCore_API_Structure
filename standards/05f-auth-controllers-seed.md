# Autenticación: controllers, seed y contrato con el cliente

> **Aplica a:** Solo perfil con seguridad  
> **Propósito:** DatabaseSeeder, AuthController, AccountController, UsersController, RolesController y contrato con el frontend.  
> Índice general: `standards/00-INDEX.md`

### Seed inicial (idempotente) — `Infrastructure/Persistence/DatabaseSeeder.cs`
```csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using {Project}.Application.Common.Security;
using {Project}.Infrastructure.Identity;

namespace {Project}.Infrastructure.Persistence;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var options = sp.GetRequiredService<IOptions<SeedOptions>>().Value;
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(DatabaseSeeder));

        var roleManager = sp.GetRequiredService<RoleManager<AppRole>>();
        await EnsureRoleAsync(roleManager, AppRoles.Admin, "Administrador del sistema", []);   // Admin pasa todas las políticas
        await EnsureRoleAsync(roleManager, AppRoles.User, "Usuario estándar", Permissions.DefaultUserPermissions);

        if (string.IsNullOrWhiteSpace(options.AdminEmail) || string.IsNullOrWhiteSpace(options.AdminPassword))
        {
            logger.LogWarning("Seed:AdminEmail/AdminPassword no configurados; no se crea usuario administrador.");
            return;
        }

        var userManager = sp.GetRequiredService<UserManager<AppUser>>();
        var admin = await userManager.FindByEmailAsync(options.AdminEmail);
        if (admin is null)
        {
            admin = new AppUser
            {
                UserName = options.AdminEmail,
                Email = options.AdminEmail,
                EmailConfirmed = true,
                FirstName = "Admin",
                CreatedAt = DateTime.UtcNow
            };
            var created = await userManager.CreateAsync(admin, options.AdminPassword);
            if (!created.Succeeded)
                throw new InvalidOperationException("No se pudo crear el admin: " + string.Join("; ", created.Errors.Select(e => e.Description)));
            logger.LogInformation("Usuario administrador creado: {Email}", options.AdminEmail);
        }

        if (!await userManager.IsInRoleAsync(admin, AppRoles.Admin))
            await userManager.AddToRoleAsync(admin, AppRoles.Admin);
    }

    /// <summary>Crea el rol si no existe y AGREGA los permisos faltantes (no quita los personalizados).</summary>
    private static async Task EnsureRoleAsync(RoleManager<AppRole> roleManager, string name, string description, IEnumerable<string> permissions)
    {
        var role = await roleManager.FindByNameAsync(name);
        if (role is null)
        {
            role = new AppRole(name) { Description = description };
            await roleManager.CreateAsync(role);
        }

        var existing = (await roleManager.GetClaimsAsync(role))
            .Where(c => c.Type == Permissions.ClaimType)
            .Select(c => c.Value)
            .ToHashSet();

        foreach (var permission in permissions.Where(p => !existing.Contains(p)))
            await roleManager.AddClaimAsync(role, new Claim(Permissions.ClaimType, permission));
    }
}
```

### Controllers de Auth

`Api/Controllers/AuthController.cs`
```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using {Project}.Application.Features.Auth;

namespace {Project}.Api.Controllers;

[AllowAnonymous]
[EnableRateLimiting(RateLimitPolicies.Auth)]
public sealed class AuthController(IAuthService auth) : ApiControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken ct)
        => HandleResult(await auth.RegisterAsync(request, ct));

    [HttpPost("confirm-email")]
    public async Task<IActionResult> ConfirmEmail(ConfirmEmailRequest request, CancellationToken ct)
        => HandleResult(await auth.ConfirmEmailAsync(request, ct));

    [HttpPost("resend-confirmation")]
    public async Task<IActionResult> ResendConfirmation(EmailRequest request, CancellationToken ct)
        => HandleResult(await auth.ResendConfirmationAsync(request, ct));

    [HttpPost("login")]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct)
        => HandleResult(await auth.LoginAsync(request, ct));

    [HttpPost("login/2fa")]
    [ProducesResponseType<AuthTokens>(StatusCodes.Status200OK)]
    public async Task<IActionResult> LoginTwoFactor(TwoFactorLoginRequest request, CancellationToken ct)
        => HandleResult(await auth.LoginWithTwoFactorAsync(request, ct));

    [HttpPost("refresh")]
    [ProducesResponseType<AuthTokens>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Refresh(RefreshTokenRequest request, CancellationToken ct)
        => HandleResult(await auth.RefreshAsync(request, ct));

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(RefreshTokenRequest request, CancellationToken ct)
        => HandleResult(await auth.LogoutAsync(request, ct));

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(EmailRequest request, CancellationToken ct)
        => HandleResult(await auth.ForgotPasswordAsync(request, ct));

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request, CancellationToken ct)
        => HandleResult(await auth.ResetPasswordAsync(request, ct));

    [HttpPost("confirm-email-change")]
    public async Task<IActionResult> ConfirmEmailChange(ConfirmEmailChangeRequest request, CancellationToken ct)
        => HandleResult(await auth.ConfirmEmailChangeAsync(request, ct));
}

public static class RateLimitPolicies
{
    public const string Auth = "auth";
}
```

`Api/Controllers/AccountController.cs`
```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using {Project}.Application.Features.Auth;

namespace {Project}.Api.Controllers;

[Authorize]
public sealed class AccountController(IAccountService account) : ApiControllerBase
{
    [HttpGet("me")]
    public async Task<IActionResult> GetProfile(CancellationToken ct) => HandleResult(await account.GetProfileAsync(ct));

    [HttpPut("me")]
    public async Task<IActionResult> UpdateProfile(UpdateProfileRequest request, CancellationToken ct)
        => HandleResult(await account.UpdateProfileAsync(request, ct));

    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken ct)
        => HandleResult(await account.ChangePasswordAsync(request, ct));

    [HttpPost("change-email")]
    public async Task<IActionResult> ChangeEmail(ChangeEmailRequest request, CancellationToken ct)
        => HandleResult(await account.RequestEmailChangeAsync(request, ct));

    [HttpGet("sessions")]
    public async Task<IActionResult> GetSessions(CancellationToken ct) => HandleResult(await account.GetSessionsAsync(ct));

    [HttpDelete("sessions/{sessionId:guid}")]
    public async Task<IActionResult> RevokeSession(Guid sessionId, CancellationToken ct)
        => HandleResult(await account.RevokeSessionAsync(sessionId, ct));

    [HttpPost("logout-all")]
    public async Task<IActionResult> LogoutAll(CancellationToken ct) => HandleResult(await account.LogoutAllAsync(ct));

    [HttpGet("2fa")]
    public async Task<IActionResult> TwoFactorStatus(CancellationToken ct) => HandleResult(await account.GetTwoFactorStatusAsync(ct));

    [HttpPost("2fa/setup")]
    public async Task<IActionResult> SetupAuthenticator(CancellationToken ct) => HandleResult(await account.SetupAuthenticatorAsync(ct));

    [HttpPost("2fa/enable")]
    public async Task<IActionResult> EnableTwoFactor(TwoFactorCodeRequest request, CancellationToken ct)
        => HandleResult(await account.EnableTwoFactorAsync(request, ct));

    [HttpPost("2fa/disable")]
    public async Task<IActionResult> DisableTwoFactor(DisableTwoFactorRequest request, CancellationToken ct)
        => HandleResult(await account.DisableTwoFactorAsync(request, ct));

    [HttpPost("2fa/recovery-codes")]
    public async Task<IActionResult> RegenerateRecoveryCodes(CancellationToken ct)
        => HandleResult(await account.RegenerateRecoveryCodesAsync(ct));
}
```

`Api/Controllers/UsersController.cs`
```csharp
using Microsoft.AspNetCore.Mvc;
using {Project}.Api.Authorization;
using {Project}.Application.Common.Security;
using {Project}.Application.Features.Auth;

namespace {Project}.Api.Controllers;

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
```

`Api/Controllers/RolesController.cs`
```csharp
using Microsoft.AspNetCore.Mvc;
using {Project}.Api.Authorization;
using {Project}.Application.Common.Security;
using {Project}.Application.Features.Auth;

namespace {Project}.Api.Controllers;

public sealed class RolesController(IRoleService roles) : ApiControllerBase
{
    [HttpGet, HasPermission(Permissions.Roles.Read)]
    public async Task<IActionResult> GetAll(CancellationToken ct) => HandleResult(await roles.GetAllAsync(ct));

    [HttpGet("permissions"), HasPermission(Permissions.Roles.Read)]
    public IActionResult GetAvailablePermissions() => Ok(roles.GetAvailablePermissions());

    [HttpGet("{id:guid}"), HasPermission(Permissions.Roles.Read)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) => HandleResult(await roles.GetByIdAsync(id, ct));

    [HttpPost, HasPermission(Permissions.Roles.Manage)]
    public async Task<IActionResult> Create(CreateRoleRequest request, CancellationToken ct)
        => HandleCreated(await roles.CreateAsync(request, ct), nameof(GetById), r => new { id = r.Id });

    [HttpPut("{id:guid}"), HasPermission(Permissions.Roles.Manage)]
    public async Task<IActionResult> Update(Guid id, UpdateRoleRequest request, CancellationToken ct)
        => HandleResult(await roles.UpdateAsync(id, request, ct));

    [HttpDelete("{id:guid}"), HasPermission(Permissions.Roles.Manage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) => HandleResult(await roles.DeleteAsync(id, ct));

    [HttpPut("{id:guid}/permissions"), HasPermission(Permissions.Roles.Manage)]
    public async Task<IActionResult> SetPermissions(Guid id, SetPermissionsRequest request, CancellationToken ct)
        => HandleResult(await roles.SetPermissionsAsync(id, request, ct));
}
```

### Contrato con el frontend
El contrato completo para el cliente (login, 2FA, refresh, enlaces de email, permisos) está en `standards/15-frontend-integration.md`, sección "Contrato de autenticación".
