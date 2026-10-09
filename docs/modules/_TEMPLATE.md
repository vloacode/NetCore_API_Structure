# Módulo: {{NombreModulo}}

<!-- IA: copiar como docs/modules/<modulo-en-minusculas>.md con el flujo new-module.
Es la FUENTE DE VERDAD de los requerimientos funcionales del módulo. Código (MOD) de 3-4 letras para los ids FR-MOD-xxx.
Lo mantienen new-module, new-entity, api-endpoint y docs-sync. Menos de 250 líneas; si crece, dividir el módulo. -->

| Campo | Valor |
|---|---|
| Código | `{{MOD}}` |
| Estado | {{Planificado / En curso / Terminado}} |
| Requerimientos de negocio | {{BR-001, BR-004}} |
| Dueño de negocio | {{Persona o área}} |

## Propósito y alcance
{{Qué resuelve este módulo, en 2 o 3 líneas.}}

**Incluye:** {{...}}
**No incluye:** {{...}}

## Requerimientos funcionales
| Id | Como… | Quiero… | Para… | Prioridad | Estado |
|---|---|---|---|---|---|
| FR-{{MOD}}-001 | {{rol}} | {{acción}} | {{beneficio}} | {{Must}} | {{Pendiente}} |

### FR-{{MOD}}-001 — {{Título}}
**Criterios de aceptación:**
- Dado {{contexto}}, cuando {{acción}}, entonces {{resultado}}.
- Dado {{contexto}}, cuando {{acción inválida}}, entonces {{error `{{Entity}}.Motivo`}}.

## Entidades
<!-- IA: el detalle de propiedades está en 05-domain-model.md; aquí solo cuáles pertenecen al módulo. -->
| Entidad | Rol en el módulo |
|---|---|
| {{Entity}} | {{Principal / catálogo / detalle}} |

## Reglas de negocio
| Id | Regla | Dónde se valida |
|---|---|---|
| RN-{{MOD}}-01 | {{El código es único entre registros activos}} | {{Service (BD) / Validator (forma) / índice único}} |

## Endpoints
| Método | Ruta | Descripción | Permiso `[SEC]` | Request | Response | FR |
|---|---|---|---|---|---|---|
| GET | `/api/{{entities}}` | Listado paginado con filtros | `{{entities}}.read` | `{{Entity}}Filter` (query) | `PagedResult<{{Entity}}Dto>` | FR-{{MOD}}-002 |
| GET | `/api/{{entities}}/{id}` | Detalle | `{{entities}}.read` | — | `{{Entity}}Dto` | FR-{{MOD}}-002 |
| POST | `/api/{{entities}}` | Crear | `{{entities}}.write` | `Create{{Entity}}Request` | 201 `{{Entity}}Dto` | FR-{{MOD}}-001 |
| PUT | `/api/{{entities}}/{id}` | Actualizar | `{{entities}}.write` | `Update{{Entity}}Request` | `{{Entity}}Dto` | FR-{{MOD}}-003 |
| DELETE | `/api/{{entities}}/{id}` | Eliminar (soft delete) | `{{entities}}.delete` | — | 204 | FR-{{MOD}}-004 |

## Filtros y orden del listado
| Parámetro | Tipo | Efecto |
|---|---|---|
| `search` | string | {{Busca en nombre y código}} |
| `sortBy` | `name` · `createdAt` | Orden (desempate por Id) |

## Errores
| Código | HTTP | Cuándo ocurre |
|---|---|---|
| `{{Entity}}.NotFound` | 404 | El id no existe o está eliminado |
| `{{Entity}}.CodeAlreadyExists` | 409 | Ya existe otro registro activo con ese código |

## Eventos, notificaciones y jobs
{{"Ninguno" o lista con referencia a 08-integrations-events-jobs.md.}}

## Pruebas
| Escenario | Test | Estado |
|---|---|---|
| {{Crear duplicado devuelve 409}} | `{{Entities}}ApiTests.Create_WithDuplicateCode_Returns409WithCode` | {{Pendiente}} |

## Notas y decisiones
- {{Decisiones propias del módulo; si contradicen un standard, enlazar el ADR.}}
