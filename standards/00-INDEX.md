# Índice de standards (.NET 10 Web API)

Estos archivos son la **capa genérica** del kit: describen *cómo* se construye cualquier API con este kit.
**No se editan por proyecto.** Lo propio de cada proyecto vive en `docs/`. Si un proyecto necesita apartarse de un standard, se registra un ADR en `docs/decisions/`.

Stack fijo: **.NET 10 (LTS) · ASP.NET Core Web API con Controllers · EF Core 10 + SQL Server · FluentValidation · OpenAPI + Scalar**.
Con seguridad, además: **ASP.NET Core Identity local + JWT + refresh tokens**. Sin AutoMapper.

## Antes de leer
1. Lee el **Perfil del proyecto** en `docs/00-MASTER_CONTEXT.md` (`Security = enabled | disabled` y capacidades activas).
2. Lee solo los archivos que tu tarea necesita: `ai/context-packs.md` dice cuáles.
3. Las líneas marcadas `[SEC]` solo aplican con seguridad.

## Archivos

| Archivo | Tema | Perfil |
|---|---|---|
| `01-solution-architecture.md` | Perfiles, reglas no negociables, marcadores, paquetes, estructura, nombres | Todos |
| `01a-bootstrap.md` | DependencyInjection, Program.cs, appsettings, secretos | Todos |
| `02-domain-and-persistence.md` | BaseEntity, IRepository/IUnitOfWork, AppDbContext, configuraciones EF, interceptor de auditoría | Todos |
| `02a-repository-unit-of-work.md` | SpecificationEvaluator, Repository<T>, UnitOfWork con transacciones, SQL parametrizado | Todos |
| `03-application-layer.md` | Result/Error, paginación, PredicateBuilder, Specification<T> | Todos |
| `04-api-conventions.md` | ApiControllerBase (Result→HTTP), ValidationFilter, excepciones globales, OpenAPI | Todos |
| `05-authentication-identity.md` | Visión general de Auth: endpoints, flujo de tokens, entidades, opciones | Seguridad |
| `05a-auth-tokens-sessions.md` | TokenService, CurrentUserService, SessionManager, AccountEmails | Seguridad |
| `05b-auth-contracts.md` | DTOs, errores, interfaces y validadores de Auth | Seguridad |
| `05c-auth-service.md` | AuthService (registro, login, 2FA, refresh, reset) | Seguridad |
| `05d-account-service.md` | AccountService (perfil, contraseña, email, sesiones, 2FA) | Seguridad |
| `05e-user-role-admin.md` | UserAdminService, RoleService | Seguridad |
| `05f-auth-controllers-seed.md` | Controllers de Auth, DatabaseSeeder, contrato con el cliente | Seguridad |
| `06-authorization-permissions.md` | Roles, permisos, HasPermission, PermissionPolicyProvider | Seguridad |
| `07-business-entity-templates.md` | Molde por entidad: entidad, config, contratos, specs, validadores, service, controller | Todos |
| `07a-additional-patterns.md` | Transacciones, "solo uno activo", entidades similares, reportes, checklist por entidad | Todos |
| `08-error-codes.md` | Formato de códigos de error y catálogo | Todos |
| `09-security-baseline.md` | Baseline de seguridad (OWASP API Top 10), headers, secretos, API key | Todos |
| `10-observability.md` | Logging, OpenTelemetry, health checks, correlation id | Todos |
| `11-performance.md` | Consultas, índices, caché, compresión | Todos |
| `12-testing.md` | Estrategia de pruebas y casos mínimos | Todos |
| `13-deployment.md` | Docker, CI, migraciones, Azure/IIS, checklist de producción | Todos |
| `14-coding-standards.md` | .editorconfig, Directory.Build.props, analizadores, nullable | Todos |
| `15-frontend-integration.md` | Lo que la API configura para que el frontend la consuma cómodo | Todos |
| `99-lessons-learned.md` | Errores de implementaciones anteriores y la regla que los evita | Todos |
