using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using KitApi.Application.Common.Security;
using KitApi.Infrastructure.Identity;

namespace KitApi.Infrastructure.Persistence;

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
