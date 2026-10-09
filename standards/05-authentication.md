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

## Flujo de tokens
- **Access token**: JWT HS256, 15 min (`Jwt:AccessTokenMinutes`). Claims `sub`, `email`, `name`, `jti`, `role` y `permission` (varios).
- **Refresh token**: 64 bytes aleatorios, 7 días. En BD se guarda **solo su SHA-256**.
- **Rotación**: cada refresh revoca el token usado y crea otro de la misma *familia* (sesión).
- **Detección de reuso**: si llega un token ya rotado, se revoca la familia entera (posible robo).
- **2FA**: el login devuelve un JWT intermedio de 5 min con audiencia `{Audience}:2fa`. No sirve como access token; solo se canjea en `/login/2fa`.

## Piezas

| Pieza | Responsabilidad |
|---|---|
| `AppUser`, `AppRole`, `RefreshToken` (`IdentityEntities.cs`) | Usuario (`Guid`), rol con descripción (los de sistema están en `AppRoles.System`), sesiones |
| `JwtOptions`, `AppUrlOptions`, `SeedOptions` (`Options.cs`) | Configuración validada al arrancar (`ValidateOnStart`) |
| `TokenService` | Firma access tokens, tokens de 2FA y genera/hashea refresh tokens |
| `SessionManager` | Crea, rota, revoca y detecta reuso de refresh tokens |
| `CurrentUserService` | `UserId`, email y permisos del request (lo usa la auditoría) |
| `AccountEmails` | Arma enlaces al frontend (`App:ClientUrl`) y envía con `IEmailSender` |
| `AuthService`, `AccountService`, `UserAdminService`, `RoleService` | Casos de uso; devuelven `Result` |
| `DatabaseSeeder` | Idempotente: roles de sistema, permisos y admin inicial (`Seed:*`) |

## Reglas
1. Identity **local**: `AddIdentityCore` sin cookies ni proveedores externos. Email confirmado obligatorio para iniciar sesión.
2. Política de contraseñas: mínimo 8, dígito, minúscula, mayúscula, símbolo, 4 caracteres únicos. Lockout: 5 intentos, 15 min.
3. Respuestas que no revelan si un email existe (registro, reenvío, forgot-password).
4. Cambiar o resetear la contraseña **revoca todas las sesiones**.
5. `MapInboundClaims = false`: los claims conservan su nombre JWT (`sub`, `role`, `permission`).
6. Llave de firma (`Jwt:SigningKey`, 32+ caracteres) y `Seed:AdminPassword` solo en user-secrets o variables de entorno.
7. Los endpoints de Auth usan la política de rate limit `RateLimitPolicies.Auth` (10/min por IP).
8. Un campo nuevo del usuario: propiedad en `AppUser` + DTO/validador en `AuthContracts`/`AuthValidators` + migración.
9. En producción, reemplazar `LoggingEmailSender` por un `IEmailSender` real (SMTP o proveedor) registrado en `PersistenceExtensions`.
