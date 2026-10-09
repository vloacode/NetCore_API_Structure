# Flujo: analytics-event (agregar un evento de analítica de uso)

## Objetivo
Medir algo nuevo del uso del producto **con un propósito claro**: el evento responde a un KPI, no tiene datos personales y queda documentado, implementado y probado.

## Cuándo usarlo
- El usuario quiere saber "cuántos…", "qué porcentaje…" o "en qué paso abandonan…".
- Al terminar `new-entity` o `new-feature`, si `product-analytics` está activa y la funcionalidad es relevante para algún KPI.

## Requisitos
`Capabilities` incluye `product-analytics` (perfil en `docs/00-MASTER_CONTEXT.md`). Si no está activa, proponer activarla (`ai/suggestions-catalog.md`) y registrar la decisión en un ADR.

## Roles
**product-analyst** (pregunta, KPI y definición del evento) · **backend-developer** (implementación) · **qa-engineer** (test) · **security-reviewer** si hay dudas sobre si una propiedad es un dato personal.

## Archivos a leer
`docs/11-product-analytics.md`, `standards/17-product-analytics.md`, `standards/17a-analytics-ingestion-queries.md`, `standards/17b-analytics-dispatch.md` (si se agrega un destino) y el service donde ocurre el hecho.

## Pasos
1. **Para qué**: identificar la pregunta de negocio y el KPI que el evento alimenta. Sin KPI, no hay evento.
2. **Definir el evento**:
   - Nombre `area.accion` en pasado (`invoice.paid`).
   - Origen: servidor (hecho confirmado) o cliente (interacción de interfaz).
   - Cuándo se dispara y propiedades mínimas.
   - Revisar que **ninguna** propiedad identifique a una persona.
3. **Documentar** en `docs/11-product-analytics.md`: catálogo de eventos y el KPI que lo usa.
4. **Constante** en `AnalyticsEvents`. Si lo envía el frontend, agregarlo también a `ClientAllowed`.
5. **Implementar**:
   - **Servidor**: `analytics.Track(AnalyticsEvents.X, props)` en el service, **después** del guardado exitoso (`standards/17`, "Uso en un service"). Inyectar `IAnalyticsTracker` en el constructor.
   - **Cliente**: informar al equipo de frontend el nombre y las propiedades, y el contrato de `POST /api/analytics/events` (`standards/17a`).
6. **Test** de integración: el evento se guarda con sus propiedades y sin datos personales (patrón `WaitForEventAsync` de `standards/17a`).
7. **Consulta**: dejar en `docs/11` (o en el KPI) la consulta SQL que calcula el KPI con este evento.
8. `docs-sync` y `PROJECT_STATUS`.

## Reglas
- Un evento por hecho de negocio; no mandar el mismo hecho desde el cliente y el servidor.
- Nunca llamar a `Track` dentro de `ExecuteInTransactionAsync` ni antes de guardar: se mediría algo que quizá no ocurrió.
- Cambiar el significado de un evento existente = evento nuevo (los datos históricos no se reinterpretan).

## Definition of Done
- [ ] Evento con KPI asociado, documentado en `docs/11`.
- [ ] Constante (y `ClientAllowed` si aplica), `Track` después del guardado.
- [ ] Test en verde; sin datos personales.
- [ ] Consulta del KPI escrita.
