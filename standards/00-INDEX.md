# Índice de standards (.NET Web API)

Los standards son las **reglas** del kit: qué se hace, por qué y qué evitar. El **código** vive en las plantillas `dotnet new`, que son la fuente de verdad y se verifican compilando:
- [`templates/api/`](../templates/api/) → `dotnet new kitapi` (la solución completa según el perfil).
- [`templates/entity/`](../templates/entity/) → `dotnet new kit-entity` (una entidad de negocio: 4 archivos + test).

**No se editan por proyecto.** Lo propio de cada proyecto vive en `docs/`. Si un proyecto necesita apartarse de un standard, se registra un ADR en `docs/decisions/`.

Stack fijo: **.NET LTS vigente (hoy 10) · ASP.NET Core con Controllers · EF Core (Repository + Unit of Work) + SQL Server o PostgreSQL · FluentValidation · OpenAPI + Scalar · OpenTelemetry**. Con seguridad, además: **Identity local + JWT + refresh tokens**. Sin MediatR ni AutoMapper.

## Antes de leer
1. Lee el **Perfil del proyecto** en `docs/00-MASTER_CONTEXT.md` y la fase actual en `docs/ai/PROJECT_STATUS.md`.
2. Lee **solo** los archivos que tu fase o tarea necesita (`ai/implementation-phases.md`, `ai/context-packs.md`).
3. Para ver el código de algo, abre el archivo enlazado en el standard; no lo copies a mano: genera con las plantillas.

## Archivos

| Archivo | Tema | Aplica |
|---|---|---|
| `01-solution-architecture.md` | Perfil → opciones de plantilla, crear el proyecto, reglas no negociables, estructura, Program.cs, nombres | Todos |
| `02-persistence.md` | BaseEntity, Repository/Unit of Work, AppDbContext, auditoría, soft delete, concurrencia, transacciones, SQL crudo, migraciones | Todos |
| `03-application-layer.md` | Result/Error, paginación, PredicateBuilder, especificaciones | Todos |
| `04-api-conventions.md` | Controllers, Result→HTTP, validación, excepciones, OpenAPI, JSON | Todos |
| `05-authentication.md` | Identity local + JWT: endpoints, tokens, piezas, reglas | Con seguridad |
| `06-authorization-permissions.md` | Roles, permisos, `HasPermission` | Con seguridad |
| `07-entities.md` | `kit-entity`: qué genera, cómo adaptarlo, patrones adicionales, DoD por entidad | Todos |
| `08-error-codes.md` | Formato ProblemDetails y catálogo de códigos | Todos |
| `09-security-baseline.md` | OWASP API Top 10, headers, secretos, API key | Todos |
| `10-observability.md` | Logging, OpenTelemetry, health checks, trace id | Todos |
| `11-performance.md` | Consultas, índices, caché, compresión | Todos |
| `12-testing.md` | Estrategia, fixture con Testcontainers, casos mínimos | Todos |
| `13-deployment.md` | Docker, CI, migraciones, Azure/IIS, checklist de producción | Todos |
| `13a-reverse-proxy-nginx.md` | Nginx delante de la API: TLS, límites, Forwarded Headers | Solo `Deployment = nginx` |
| `14-coding-standards.md` | Estilo C#, analizadores, formato, Git | Todos |
| `15-frontend-integration.md` | Lo que la API configura para que el frontend la consuma cómodo | Todos |
| `16-framework-versions.md` | Versiones de .NET, piezas dependientes, migración | Todos |
| `17-product-analytics.md` | Analítica de uso: tracker, despacho, privacidad, endpoint, KPIs | Solo `product-analytics` |
| `99-lessons-learned.md` | Errores de implementaciones anteriores y la regla que los evita | Todos |
