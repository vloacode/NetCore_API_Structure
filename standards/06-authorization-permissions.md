# Autorización por permisos

> **Aplica a:** Solo con seguridad (`--security true`)  
> **Propósito:** Roles, catálogo de permisos, `HasPermission` y proveedor de políticas.  
> **Código:** [`Permissions.cs`](../templates/api/src/KitApi.Api/Application/Common/Security/Permissions.cs) · [`PermissionAuthorization.cs`](../templates/api/src/KitApi.Api/Api/Authorization/PermissionAuthorization.cs)

## Cómo fluye
1. Un permiso es un claim `permission` asociado a un **rol** (tabla `AspNetRoleClaims`).
2. Al hacer login o refresh se leen los roles del usuario y sus permisos, y se incluyen en el access token.
3. `[HasPermission(Permissions.{Entities}.Read)]` pide la política `permission:{entities}.read`; `PermissionPolicyProvider` la crea al vuelo y valida el claim. El rol `Admin` pasa todas.
4. Los cambios de roles o permisos aplican en el siguiente refresh (máximo `AccessTokenMinutes`). Para efecto inmediato, revocar las sesiones del usuario.

## Catálogo
- `AppRoles`: `Admin` y `User` son roles de sistema (no se renombran ni eliminan).
- `Permissions`: una clase anidada por recurso con constantes `Read`, `Write`, `Delete` (o las que pida el dominio). `Permissions.All` las descubre por reflexión; el seeder las asigna al rol `Admin`.
- `Permissions.DefaultUserPermissions`: lo que recibe el rol `User` al crearse. Completar según el dominio.

## Por entidad nueva
`dotnet new kit-entity` deja en el comentario **REGISTRO** de `{Entity}.cs` la clase a pegar en `Permissions.cs`:
```csharp
public static class Invoices { public const string Read = "invoices.read"; public const string Write = "invoices.write"; public const string Delete = "invoices.delete"; }
```
Después, decidir con el usuario si `User` recibe alguno (agregarlo a `DefaultUserPermissions`) y actualizar `docs/07-permissions-matrix.md`.

## Reglas
- Autorizar por **permiso**, nunca por nombre de rol en el código (`[Authorize(Roles = "...")]` está prohibido).
- **Cada** acción de un controller con seguridad lleva `[HasPermission]` (o `[AllowAnonymous]` explícito y justificado).
- Si el recurso tiene dueño, el permiso no basta: el filtro por dueño va en la spec (OWASP API1, `standards/09`).
