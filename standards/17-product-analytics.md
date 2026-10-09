# Analítica de producto (uso)

> **Aplica a:** Todos los perfiles, solo con la capacidad `product-analytics` (`--analytics true`).  
> **Propósito:** Registrar eventos de uso del producto ("¿qué hacen los usuarios?") en la BD del proyecto, con privacidad desde el diseño.  
> **Código:** [`Infrastructure/Analytics/`](../templates/api/src/KitApi.Api/Infrastructure/Analytics/) · [`Application/Abstractions/Analytics/`](../templates/api/src/KitApi.Api/Application/Abstractions/Analytics/) · [`AnalyticsController`](../templates/api/src/KitApi.Api/Api/Controllers/AnalyticsController.cs) · [`ProductAnalyticsTests`](../templates/api/tests/KitApi.IntegrationTests/ProductAnalyticsTests.cs)

## Qué es y qué no es
| Es | No es |
|---|---|
| Hechos de negocio confirmados por la API: `user.registered`, `invoice.paid`, `search.no_results` | Telemetría técnica (latencia, errores): eso es `standards/10` |
| Datos propios, exactos (no los bloquea un ad-blocker), consultables con SQL | Analítica web de páginas, sesiones o campañas: eso es GA4 u otra herramienta en el **frontend** |
| Seudónimos: nunca el id real, email ni nombre | Reportes de BI ni tableros listos |

## Cómo funciona
```
Service (después de SaveChanges OK) ─► IAnalyticsTracker.Track() ─► cola en memoria (AnalyticsBuffer, Channel acotado)
Frontend ─► POST /api/analytics/events ─────┘                          │
                                            AnalyticsDispatcher (BackgroundService, lotes)
                                                         └─► cada IAnalyticsSink: BD (por defecto) · log (Analytics:LogEvents) · otro (ADR)
AnalyticsRetentionJob (diario) ─► borra lo más viejo que Analytics:RetentionDays
```
- `Track` **no bloquea y nunca falla la petición**: si la analítica está apagada o la cola llena, el evento se descarta.
- Si un destino falla, se registra un warning y se descarta el lote: la analítica no tumba la API. Un evento que no puede perderse (por ejemplo, para facturar) no es analítica: usar outbox.

## Reglas
1. Nombres `area.accion` en minúsculas y en pasado: `user.registered`, `invoice.paid`. Siempre como constante en `AnalyticsEvents`, nunca un string suelto.
2. `Track` **solo después de un guardado exitoso**, nunca dentro de `ExecuteInTransactionAsync`.
3. Propiedades: pocas (máximo 25), simples (texto corto, número, booleano, fecha) y **sin datos personales**. `AnalyticsPropertyGuard` descarta claves sensibles (`email`, `password`, `token`, `phone`, `name`, `address`, `ip`…) y textos largos.
4. Identidad: `AnonymousId = HMAC-SHA256(Analytics:HashKey, id)`, del usuario autenticado o del `X-Anonymous-Id` que envía el frontend. La llave va en user-secrets o Key Vault.
5. Cada evento se documenta en `docs/11-product-analytics.md` con el KPI que alimenta (flujo `ai/workflows/analytics-event.md`).
6. Retención limitada (`Analytics:RetentionDays`, 90 por defecto). Interruptor global `Analytics:Enabled`.

## Uso en un service
```csharp
// Constructor: (IUnitOfWork uow, IAnalyticsTracker analytics)
_repo.Add(invoice);
await uow.SaveChangesAsync(ct);
analytics.Track(AnalyticsEvents.InvoicePaid, new Dictionary<string, object?> { ["plan"] = invoice.Plan });
```

## Eventos del frontend: `POST /api/analytics/events`
- Cuerpo `{ "events": [ { "name": "search.no_results", "properties": { "term_length": 7 } } ] }`, máximo 50 por envío.
- Solo nombres de la lista blanca `AnalyticsEvents.ClientAllowed`. La privacidad se valida aquí (400) y otra vez en el tracker.
- Header `X-Anonymous-Id`: GUID aleatorio que el frontend genera una vez y guarda. Cuenta usuarios únicos sin identificarlos.
- Respuestas: **202** aceptado, **400** evento no permitido o con datos personales, **429** demasiados envíos (política `analytics`, 60/min por IP).
- Si el usuario rechazó la analítica (consentimiento), el frontend no envía eventos ni el header.

## Tabla `AnalyticsEvents`
`Id` (Guid v7), `Name`, `OccurredAt` (UTC), `AnonymousId`, `Source` (`server`/`client`), `Properties` (JSON: `jsonb` en PostgreSQL, `nvarchar(max)` con `ISJSON` en SQL Server), `TraceId`. Solo inserciones: no hereda `BaseEntity`. Índices en `(Name, OccurredAt)` y `OccurredAt`.

## Consultas de KPIs (ejemplos)
```sql
-- Eventos y usuarios por día (PostgreSQL; en SQL Server: CAST(OccurredAt AS date), DATEADD(day, -28, SYSUTCDATETIME()))
SELECT occurred_at::date AS day, name, COUNT(*) AS events, COUNT(DISTINCT anonymous_id) AS users
FROM analytics_events
WHERE occurred_at >= now() - interval '28 days'
GROUP BY occurred_at::date, name
ORDER BY day, name;

-- Embudo de dos pasos: cuántos de los que hicieron A hicieron después B
WITH a AS (SELECT anonymous_id, MIN(occurred_at) AS at FROM analytics_events WHERE name = 'user.registered' GROUP BY anonymous_id),
     b AS (SELECT anonymous_id, MIN(occurred_at) AS at FROM analytics_events WHERE name = 'invoice.paid' GROUP BY anonymous_id)
SELECT COUNT(a.anonymous_id) AS step_a, COUNT(b.anonymous_id) AS step_b
FROM a LEFT JOIN b ON b.anonymous_id = a.anonymous_id AND b.at >= a.at;
```
Leer una propiedad: SQL Server `JSON_VALUE(Properties, '$.plan')`; PostgreSQL `properties->>'plan'`. Estas consultas se corren con un usuario **de solo lectura** (herramienta SQL, Metabase, Grafana…). No se crean endpoints de reportes: eso sería BI.

## Tests
El despacho es asíncrono: los tests esperan con sondeo (hasta ~5 s) a que el evento aparezca en la BD (`WaitForEventAsync`). `ApiFactory` configura `Analytics:HashKey`.

## Otro destino (GA4, PostHog…)
El kit no trae código de servicios externos: se agrega una clase `IAnalyticsSink`, se registra en `AddProductAnalytics` y se documenta con un ADR. Para **GA4 (Measurement Protocol)**: `measurement_id` y `api_secret` (en secretos), el `client_id` de GA4 que envía el frontend (cookie `_ga`), nombres `snake_case` de máximo 40 caracteres, máximo 25 parámetros, y un `HttpClient` tipado con `AddStandardResilienceHandler` (`standards/11`). Referencia: https://developers.google.com/analytics/devguides/collection/protocol/ga4
