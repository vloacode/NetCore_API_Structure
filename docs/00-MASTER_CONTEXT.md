# {{NombreProyecto}} — Contexto maestro

<!-- IA: este es el PRIMER archivo del proyecto que lee cualquier IA. Mantenerlo corto (menos de 120 líneas),
siempre actualizado y sin detalle que ya esté en otros documentos: aquí van resúmenes y punteros.
Lo crea project-init y lo actualiza docs-sync. Mientras existan marcadores {{...}}, el proyecto no está inicializado. -->

## Perfil del proyecto
<!-- IA: tabla leída por todos los flujos para saber qué standards aplican. Valores exactos, sin texto libre. -->

| Clave | Valor | Opciones |
|---|---|---|
| KitVersion | {{2.0.0}} | versión del kit con la que se creó o actualizó |
| Mode | {{1-standard-secure}} | `1-standard-secure` · `2-standard-public` · `3-interview` |
| Security | {{enabled}} | `enabled` (Identity + JWT + permisos) · `disabled` |
| TwoFactor | {{enabled}} | `enabled` · `disabled` (solo si Security = enabled) |
| PublicRegistration | {{enabled}} | `enabled` (cualquiera se registra) · `disabled` (solo admin crea usuarios) |
| ApiKey | {{disabled}} | `enabled` · `disabled` |
| PrimaryKey | {{int}} | `int` · `Guid` (entidades de negocio) |
| SoftDelete | {{enabled}} | `enabled` · `disabled` |
| Database | {{sqlserver}} | `sqlserver` · `postgresql` |
| TargetFramework | {{net10.0}} | versión de .NET (LTS vigente por defecto; `standards/16`) |
| Email | {{log}} | `log` (desarrollo) · `smtp` · `<proveedor>` |
| Deployment | {{docker}} | `docker` · `azure-app-service` · `azure-container-apps` · `iis` |
| FrontendOrigins | {{https://localhost:5173}} | orígenes CORS separados por coma |
| Capabilities | {{ninguna}} | ids de `ai/suggestions-catalog.md` aceptados (ej. `api-versioning, hybrid-cache`) |

## Resumen
{{Qué es el sistema, para quién y qué problema resuelve, en 3 a 5 líneas.}}

## Actores
| Actor | Descripción | Rol en el sistema |
|---|---|---|
| {{Actor}} | {{Quién es}} | {{Rol de seguridad o "anónimo"}} |

## Módulos
| Módulo | Archivo | Estado | Resumen |
|---|---|---|---|
| {{Modulo}} | `docs/modules/{{modulo}}.md` | {{Planificado / En curso / Terminado}} | {{Una línea}} |

## Documentos del proyecto
| Documento | Contenido |
|---|---|
| `01-product-vision.md` | Visión, objetivos, alcance y fuera de alcance |
| `02-business-requirements.md` | Requerimientos de negocio (`BR-xxx`) |
| `03-requirements-index.md` | Trazabilidad: `BR` → `FR` → endpoint → test |
| `04-non-functional-requirements.md` | Rendimiento, disponibilidad, seguridad, retención |
| `05-domain-model.md` | Entidades, relaciones, reglas e invariantes (diagrama ER) |
| `06-business-workflows.md` | Flujos y estados de negocio |
| `07-permissions-matrix.md` | Roles × permisos (solo con seguridad) |
| `08-integrations-events-jobs.md` | Integraciones externas, eventos, notificaciones, jobs |
| `09-glossary.md` | Términos del negocio |
| `10-roadmap.md` | Fases y entregas |
| `modules/` | Un archivo por módulo (fuente de verdad de sus requerimientos funcionales) |
| `decisions/` | ADRs: decisiones y desvíos de los standards |
| `runbooks/` | Procedimientos de operación e incidentes |
| `ai/PROJECT_STATUS.md` | Qué está hecho, en curso y pendiente |
| `ai/AI_MEMORY.md` | Lecciones, trampas y preferencias acumuladas |

## Decisiones clave
<!-- IA: solo los títulos de los ADR más importantes, con su número. -->
- {{ADR-0001: Decisiones iniciales del proyecto}}

## Estado actual
{{Una o dos líneas. El detalle está en `docs/ai/PROJECT_STATUS.md`.}}
