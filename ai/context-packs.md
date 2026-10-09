# Context packs: qué leer para cada tarea

Objetivo: cargar **solo** lo necesario. Es clave para modelos locales con poco contexto y ahorra tokens en cualquier IA. Para construir un proyecto desde cero, seguir además las fases de `ai/implementation-phases.md` (una fase o un ítem por sesión).

**Base (siempre):** `AGENTS.md` + `docs/00-MASTER_CONTEXT.md` (perfil) + `docs/ai/PROJECT_STATUS.md` (fase actual y siguiente paso).
Lo marcado `[SEC]` se lee solo si el perfil tiene `Security = enabled`. `docs/modules/<módulo>.md` = el módulo en el que se trabaja.
El código se consulta abriendo el archivo de `templates/` que enlaza el standard, **no** leyendo todas las plantillas.

| Tarea | Flujo | Rol | Archivos a leer (además de la base) |
|---|---|---|---|
| Inicializar proyecto (fases 0–1) | `ai/workflows/project-init.md` | solution-architect + product-analyst | `ai/implementation-phases.md`, `ai/suggestions-catalog.md`, `ai/references.md`, plantillas de `docs/`; en la fase 1, `standards/01-solution-architecture.md` |
| Nuevo módulo | `ai/workflows/new-module.md` | product-analyst | `docs/modules/_TEMPLATE.md`, `docs/03-requirements-index.md`, `docs/05-domain-model.md`, `docs/07-permissions-matrix.md` [SEC] |
| Nueva entidad (fase 2) | `ai/workflows/new-entity.md` | backend-developer | `docs/modules/<módulo>.md`, `standards/07-entities.md`, `standards/06-authorization-permissions.md` [SEC] |
| Nueva funcionalidad | `ai/workflows/new-feature.md` | product-analyst → backend-developer | `docs/modules/<módulo>.md`, `docs/03-requirements-index.md`, el standard del tipo de cambio |
| Endpoint nuevo o cambio | `ai/workflows/api-endpoint.md` | backend-developer | `docs/modules/<módulo>.md`, `standards/03-application-layer.md`, `standards/04-api-conventions.md`, `standards/06-authorization-permissions.md` [SEC], `standards/15-frontend-integration.md` |
| Cambio de base de datos | `ai/workflows/database-change.md` | database-architect | `docs/05-domain-model.md`, `standards/02-persistence.md` |
| Generar tests | `ai/workflows/test-generation.md` | qa-engineer | `standards/12-testing.md`, código bajo prueba, `docs/modules/<módulo>.md` |
| Revisión de código | `ai/workflows/code-review.md` | qa-engineer | `standards/01-solution-architecture.md` (reglas), `standards/99-lessons-learned.md`, el diff |
| Revisión de seguridad (fase 4) | `ai/workflows/security-review.md` | security-reviewer | `standards/09-security-baseline.md`, `standards/05-authentication.md` [SEC], `standards/06-authorization-permissions.md` [SEC], `docs/07-permissions-matrix.md` [SEC] |
| Revisión de rendimiento (fase 4) | `ai/workflows/performance-review.md` | observability-engineer | `standards/11-performance.md`, `standards/02-persistence.md`, `docs/04-non-functional-requirements.md` |
| Análisis de logs | `ai/workflows/log-analysis.md` | observability-engineer | `standards/10-observability.md`, `docs/runbooks/` |
| Incidente en producción | `ai/workflows/incident-response.md` | observability-engineer + devops-engineer | `docs/runbooks/`, `standards/10-observability.md`, `standards/13-deployment.md` |
| Registrar decisión | `ai/workflows/record-decision.md` | solution-architect | `docs/decisions/ADR-0000-template.md`, ADRs relacionados |
| Sincronizar documentación | `ai/workflows/docs-sync.md` | technical-writer | `docs/03-requirements-index.md`, `docs/modules/`, `docs/07-permissions-matrix.md` [SEC], `docs/ai/PROJECT_STATUS.md` |
| Actualizar el kit | `ai/workflows/upgrade-kit.md` | devops-engineer | `KIT_VERSION`, `CHANGELOG.md` del kit nuevo y del proyecto |
| Cambios en autenticación [SEC] | `ai/workflows/new-feature.md` | backend-developer + security-reviewer | `standards/05-authentication.md`, `standards/06-authorization-permissions.md` |
| Integración con el frontend | `ai/workflows/api-endpoint.md` | backend-developer | `standards/15-frontend-integration.md`, `standards/04-api-conventions.md` |
| Despliegue / CI / Docker (fase 5) | — | devops-engineer | `standards/13-deployment.md`, `standards/13a-reverse-proxy-nginx.md` (si `Deployment = nginx`), `docs/04-non-functional-requirements.md` |
| Migrar a otra versión de .NET | `ai/workflows/upgrade-dotnet.md` | devops-engineer + backend-developer | `standards/16-framework-versions.md`, `ai/references.md` |
| Evento de analítica de uso | `ai/workflows/analytics-event.md` | product-analyst → backend-developer | `docs/11-product-analytics.md`, `standards/17-product-analytics.md` |
| Duda técnica (API, versión, rendimiento) | — | el rol de la tarea | `ai/references.md` (o MCP de Microsoft Learn) |

## Reglas
- Si un archivo del pack no existe todavía (por ejemplo `docs/` sin inicializar), avisa y propone `project-init`.
- No leas `standards/05` ni el módulo de Identity salvo que la tarea sea de autenticación.
- Si necesitas algo fuera del pack, léelo puntual (una sección o un archivo de `templates/`), no el kit entero.
