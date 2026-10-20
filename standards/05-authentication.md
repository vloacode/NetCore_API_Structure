# Autenticación: Identity local + JWT

> **Aplica a:** Solo con seguridad (`--security true`)  
> **Propósito:** Endpoints, flujo de tokens, piezas y reglas de Auth. Permisos: `standards/06`. Contrato con el frontend: `standards/15`.  
> **Código:** [`Infrastructure/Identity/`](../templates/api/src/KitApi.Api/Infrastructure/Identity/) · [`Application/Features/Auth/`](../templates/api/src/KitApi.Api/Application/Features/Auth/) · [`Api/Controllers/`](../templates/api/src/KitApi.Api/Api/Controllers/) · [`Extensions/SecurityExtensions.cs`](../templates/api/src/KitApi.Api/Extensions/SecurityExtensions.cs)

El módulo viene **completo** en la plantilla. En un proyecto normal no se reescribe: se configura y, si hace falta, se extiende (por ejemplo, campos nuevos en `AppUser`).

## Endpoints

| Método | Ruta | Auth | Descripción |
|---|---|---|---|
| POST | `/api/auth/register` | Anónimo | Crea usuario (rol `User`) y envía confirmación. No revela si el email ya existe. |
| POST | `/api/auth/confirm-email` · `/resend-confirmation` | Anónimo | Confirma el email / reenvía (siempre 204). |
| POST | `/api/auth/login` | Anónimo | Credenciales + lockout. Devuelve tokens o `requiresTwoFactor`. |
| POST | `/api/auth/login/2fa` | Anónimo | Completa el login con código TOTP o de recuperación. |
| POST | `/api/auth/refresh` · `/logout` | Anónimo | Rota el refresh token / revoca la sesión. |
| POST | `/api/auth/forgot-password` · `/reset-password` | Anónimo | Enlace de reset (siempre 204) / nueva contraseña y revocación de sesiones. |
| POST | `/api/auth/confirm-email-change` | Anónimo | Confirma el cambio de email. |
| GET / PUT | `/api/account/me` | Usuario | Ver o editar perfil. |
| POST | `/api/account/change-password` · `/change-email` | Usuario | Cambia contraseña (revoca sesiones, devuelve tokens) / solicita cambio de email. |
| GET / DELETE | `/api/account/sessions` · `/sessions/{id}` · POST `/logout-all` | Usuario | Sesiones y dispositivos. |
| GET / POST | `/api/account/2fa` · `/2fa/setup` · `/enable` · `/disable` · `/recovery-codes` | Usuario | 2FA TOTP con QR (`otpauth://`) y códigos de recuperación. |
| GET | `/api/users` · `/api/users/{id}` | `users.read` | Listado paginado y detalle. |
| POST / PUT | `/api/users`, `/{id}/lock`, `/unlock`, `/activate`, `/deactivate`, `/roles`, `/send-password-reset`, `/revoke-sessions` | `users.manage` | Administración de usuarios. |
| GET | `/api/roles` · `/{id}` · `/permissions` | `roles.read` | Roles y catálogo de permisos. |
| POST / PUT / DELETE | `/api/roles`, `/{id}`, `/{id}/permissions` | `roles.manage` | CRUD de roles (los de sistema están protegidos). |
| GET | `/api/security-events` | `audit.read` | Bitácora de seguridad paginada (filtros: `userId`, `type`, `from`, `to`). |

## Flujo de tokens
- **Access token**: JWT HS256, 15 min (`Jwt:AccessTokenMinutes`). Claims `sub`, `email`, `name`, `jti`, `role` y `permission` (varios).
- **Refresh token**: 64 bytes aleatorios, 7 días. En BD se guarda **solo su SHA-256**.
- **Rotación**: cada refresh revoca el token usado y crea otro de la misma *familia* (sesión).
- **Detección de reuso**: si llega un token ya rotado, se revoca la familia entera (posible robo).
- **2FA**: el login devuelve un JWT intermedio de 5 min con audiencia `{Audience}:2fa`. No sirve como access token; solo se canjea en `/login/2fa`.

## Piezas

| Pieza | Responsabilidad |
|---|---|
| `AppUser`, `AppRole`, `RefreshToken` (`IdentityEntities.cs`) | Usuario y rol con la llave del proyecto (`EntityKey`: Guid, int o long), rol con descripción (los de sistema están en `AppRoles.System`), sesiones |
| `JwtOptions`, `AppUrlOptions`, `SeedOptions` (`Options.cs`) | Configuración validada al arrancar (`ValidateOnStart`) |
| `TokenService` | Firma access tokens, tokens de 2FA y genera/hashea refresh tokens |
| `SessionManager` | Crea, rota, revoca y detecta reuso de refresh tokens |
| `CurrentUserService` | `UserId`, email y permisos del request (lo usa la auditoría) |
| `AccountEmails` | Arma enlaces al frontend (`App:ClientUrl`) y envía con `IEmailSender` |
| `AuthService`, `AccountService`, `UserAdminService`, `RoleService` | Casos de uso; devuelven `Result` |
| `DatabaseSeeder` | Idempotente: roles de sistema, permisos y admin inicial (`Seed:*`) |
| `SecurityAuditLog` (`ISecurityAuditLog`) | Bitácora de seguridad en la tabla `SecurityEvents`: quién, a quién, qué, cuándo y desde qué IP |
| `SecurityCleanupJob` | Una vez al día borra los refresh tokens vencidos y los eventos más viejos que `SecurityAudit:RetentionDays` (365) |

## Reglas
1. Identity **local**: `AddIdentityCore` sin cookies ni proveedores externos. Email confirmado obligatorio para iniciar sesión.
2. Política de contraseñas: mínimo 8, dígito, minúscula, mayúscula, símbolo, 4 caracteres únicos. Lockout: 5 intentos, 15 min.
3. Respuestas que no revelan si un email existe (registro, reenvío, forgot-password).
4. Cambiar o resetear la contraseña **revoca todas las sesiones**.
5. `MapInboundClaims = false`: los claims conservan su nombre JWT (`sub`, `role`, `permission`).
6. Llave de firma (`Jwt:SigningKey`, 32+ caracteres) y `Seed:AdminPassword` solo en user-secrets o variables de entorno.
7. Los endpoints de Auth usan la política de rate limit `RateLimitPolicies.Auth` (10/min por IP).
8. Un campo nuevo del usuario: propiedad en `AppUser` + DTO/validador en `AuthContracts`/`AuthValidators` + migración.
9. Emails reales: generar con `--email smtp` (MailKit) o registrar otro `IEmailSender` (SendGrid, SES…) en `PersistenceExtensions` con un ADR.
10. **Bitácora de seguridad**: todo evento de seguridad nuevo se escribe con `audit.WriteAsync(SecurityEventTypes.X, userId, detalles)`, después de que la acción tuvo éxito. Nunca contraseñas, tokens ni códigos en `details`.

## Bitácora de seguridad (`/api/security-events`, permiso `audit.read`)
| Evento | Cuándo |
|---|---|
| `login.succeeded` / `login.failed` / `login.blocked` | Login correcto, fallido (email inexistente o contraseña incorrecta) o sobre una cuenta bloqueada |
| `account.locked`, `login.2fa_failed` | Bloqueo automático por intentos; código de 2FA incorrecto |
| `session.reuse_detected`, `sessions.revoked` | Reuso de un refresh token rotado (posible robo); cierre de todas las sesiones |
| `password.changed`, `password.reset`, `email.changed`, `2fa.enabled`, `2fa.disabled` | Cambios en la cuenta |
| `user.locked`, `user.unlocked`, `user.activated`, `user.deactivated`, `user.roles_changed` | Acciones de un administrador sobre un usuario (`actorId` = el admin) |
| `role.permissions_changed`, `role.deleted` | Cambios en roles |

La escritura usa su propio `DbContext`: el evento queda aunque la operación haga rollback, y un fallo al escribir solo deja un warning (nunca tumba el request).
