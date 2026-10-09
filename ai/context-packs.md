# Context packs: qué leer para cada tarea

Objetivo: cargar **solo** lo necesario. Esto es clave para modelos locales con poco contexto, y ahorra tokens en cualquier IA.

**Base (siempre):** `AGENTS.md` + `docs/00-MASTER_CONTEXT.md` (perfil del proyecto).
Los archivos marcados `[SEC]` se leen solo si el perfil tiene `Security = enabled`.
`docs/modules/<módulo>.md` = el módulo en el que se está trabajando.

| Tarea | Flujo | Rol | Archivos a leer (además de la base) |
|---|---|---|---|
| Inicializar proyecto | `ai/workflows/project-init.md` | solution-architect + product-analyst | `ai/suggestions-catalog.md`, `ai/references.md`, `standards/16-framework-versions.md`, `standards/00-INDEX.md`, `standards/01-solution-architecture.md`, `standards/01a-bootstrap.md`, plantillas de `docs/` |
| Nuevo módulo | `ai/workflows/new-module.md` | product-analyst | `docs/modules/_TEMPLATE.md`, `docs/03-requirements-index.md`, `docs/05-domain-model.md`, `docs/07-permissions-matrix.md` [SEC] |
| Nueva entidad | `ai/workflows/new-entity.md` | backend-developer | `docs/modules/<módulo>.md`, `standards/07-business-entity-templates.md`, `standards/07a-additional-patterns.md`, `standards/06-authorization-permissions.md` [SEC] |
| Nueva funcionalidad | `ai/workflows/new-feature.md` | product-analyst → backend-developer | `docs/modules/<módulo>.md`, `docs/03-requirements-index.md`, standards según el tipo de cambio |
| Endpoint nuevo o cambio | `ai/workflows/api-endpoint.md` | backend-developer | `docs/modules/<módulo>.md`, `standards/03-application-layer.md`, `standards/04-api-conventions.md`, `standards/06-authorization-permissions.md` [SEC], `standards/15-frontend-integration.md` |
| Cambio de base de datos | `ai/workflows/database-change.md` | database-architect | `docs/05-domain-model.md`, `standards/02-domain-and-persistence.md`, `standards/02a-repository-unit-of-work.md` |
| Generar tests | `ai/workflows/test-generation.md` | qa-engineer | `standards/12-testing.md`, código bajo prueba, `docs/modules/<módulo>.md` |
| Revisión de código | `ai/workflows/code-review.md` | qa-engineer | `standards/01-solution-architecture.md`, `standards/99-lessons-learned.md`, el diff |
| Revisión de seguridad | `ai/workflows/security-review.md` | security-reviewer | `standards/09-security-baseline.md`, `standards/05-authentication-identity.md` [SEC], `standards/06-authorization-permissions.md` [SEC], `docs/07-permissions-matrix.md` [SEC] |
| Revisión de rendimiento | `ai/workflows/performance-review.md` | observability-engineer | `standards/11-performance.md`, `standards/02a-repository-unit-of-work.md`, `docs/04-non-functional-requirements.md` |
| Análisis de logs | `ai/workflows/log-analysis.md` | observability-engineer | `standards/10-observability.md`, `docs/runbooks/` |
| Incidente en producción | `ai/workflows/incident-response.md` | observability-engineer + devops-engineer | `docs/runbooks/`, `standards/10-observability.md`, `standards/13-deployment.md` |
| Registrar decisión | `ai/workflows/record-decision.md` | solution-architect | `docs/decisions/ADR-0000-template.md`, ADRs relacionados |
| Sincronizar documentación | `ai/workflows/docs-sync.md` | technical-writer | `docs/03-requirements-index.md`, `docs/modules/`, `docs/07-permissions-matrix.md` [SEC], `docs/ai/PROJECT_STATUS.md` |
| Actualizar el kit | `ai/workflows/upgrade-kit.md` | devops-engineer | `KIT_VERSION`, `CHANGELOG.md` del kit nuevo y del proyecto |
| Cambios en autenticación [SEC] | `ai/workflows/new-feature.md` | backend-developer + security-reviewer | `standards/05-authentication-identity.md` y el `05a`–`05f` del tema, `standards/06-authorization-permissions.md` |
| Integración con el frontend | `ai/workflows/api-endpoint.md` | backend-developer | `standards/15-frontend-integration.md`, `standards/04-api-conventions.md` |
| Despliegue / CI / Docker | — | devops-engineer | `standards/13-deployment.md`, `docs/04-non-functional-requirements.md` |
| Migrar a otra versión de .NET | `ai/workflows/upgrade-dotnet.md` | devops-engineer + backend-developer | `standards/16-framework-versions.md`, `ai/references.md` |
| Duda técnica (API, versión, rendimiento) | — | el rol de la tarea | `ai/references.md` (o MCP de Microsoft Learn) |

## Reglas
- Si un archivo del pack no existe todavía (por ejemplo `docs/` sin inicializar), avisa y propone `project-init`.
- No leas los `05a`–`05f` completos salvo que la tarea sea de autenticación.
- Si necesitas algo fuera del pack, léelo puntual (una sección), no el archivo entero.
