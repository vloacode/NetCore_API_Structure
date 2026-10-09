using System.Reflection;

namespace KitApi.Application.Common.Security;

public static class AppRoles
{
    public const string Admin = "Admin";
    public const string User = "User";

    /// <summary>Roles de sistema: no se pueden renombrar ni eliminar.</summary>
    public static readonly IReadOnlyList<string> System = [Admin, User];
}

/// <summary>
/// Catálogo de permisos. Se guardan como claims "permission" en el ROL (AspNetRoleClaims)
/// y viajan en el access token. Agregar una clase anidada por cada entidad de negocio.
/// </summary>
public static class Permissions
{
    public const string ClaimType = "permission";

    // ---- Fijos (Auth) ----
    public static class Users
    {
        public const string Read = "users.read";
        public const string Manage = "users.manage";
    }

    public static class Roles
    {
        public const string Read = "roles.read";
        public const string Manage = "roles.manage";
    }

    // ---- Entidades de negocio: una clase anidada por entidad (la imprime `dotnet new kit-entity`) ----

    /// <summary>Todos los permisos declarados arriba (por reflexión).</summary>
    public static IReadOnlyList<string> All { get; } = typeof(Permissions)
        .GetNestedTypes(BindingFlags.Public | BindingFlags.Static)
        .SelectMany(t => t.GetFields(BindingFlags.Public | BindingFlags.Static))
        .Where(f => f.IsLiteral && f.FieldType == typeof(string))
        .Select(f => (string)f.GetRawConstantValue()!)
        .ToList();

    /// <summary>Permisos iniciales del rol User (Admin pasa todas las políticas). Completar según el dominio.</summary>
    public static readonly IReadOnlyList<string> DefaultUserPermissions = [];
}
