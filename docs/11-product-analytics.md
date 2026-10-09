# Analítica de producto

<!-- IA: solo si Capabilities incluye product-analytics (standards/17). Lo llena product-analyst con el usuario.
Orden: primero las PREGUNTAS de negocio, después los KPIs que las responden y por último los EVENTOS mínimos para calcularlos.
Nunca agregar eventos "por si acaso": cada evento debe servir a un KPI. Cada evento nuevo pasa por ai/workflows/analytics-event.md. -->

## Preguntas que queremos responder
| # | Pregunta de negocio | Quién la pregunta |
|---|---|---|
| P-1 | {{¿Cuántos usuarios nuevos completan su primer {entity} en la primera semana?}} | {{Producto}} |

## KPIs
| KPI | Definición exacta | Eventos que usa | Responde a |
|---|---|---|---|
| {{Activación a 7 días}} | {{% de user.registered que tienen {entity}.created dentro de 7 días}} | `user.registered`, `{entity}.created` | P-1 |

## Catálogo de eventos
<!-- IA: la fuente de verdad del código es AnalyticsEvents; esta tabla debe coincidir con esa clase. -->
| Evento | Origen | Cuándo se dispara | Propiedades (sin datos personales) | KPI / FR |
|---|---|---|---|---|
| `page.viewed` | cliente | El frontend muestra una pantalla | `screen` (texto corto) | {{…}} |
| `search.no_results` | cliente | Una búsqueda no devuelve resultados | `term_length` (número) | {{…}} |
| {{`{entity}.created`}} | servidor | {{Después de guardar un {entity} con éxito}} | {{`plan` (texto)}} | {{KPI / FR-MOD-001}} |

Reglas: nombre `area.accion` en minúsculas y pasado, máximo 25 propiedades, valores simples y cortos, **nunca** email, nombre, teléfono, dirección, IP ni tokens.

## Privacidad y retención
| Tema | Decisión |
|---|---|
| Identidad | Seudónimo HMAC (`AnonymousId`); nunca el id real |
| Retención | {{90}} días (`Analytics:RetentionDays`) |
| Consentimiento | {{No requerido / El frontend no envía eventos sin consentimiento / ADR-000X}} |
| Eventos del frontend sin login | {{Sí, con X-Anonymous-Id / No}} |
| Quién puede consultar los datos | {{Rol o persona}}, con usuario de BD de solo lectura |

## Cómo consultar
Consultas de ejemplo (eventos por día, embudos, propiedades JSON) en `standards/17-product-analytics.md`.
