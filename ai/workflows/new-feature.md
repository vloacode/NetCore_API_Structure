# Flujo: new-feature (funcionalidad nueva o cambio)

## Objetivo
Implementar una funcionalidad o un cambio que **no es simplemente una entidad nueva**: una regla de negocio, una transición de estado, un proceso de varios pasos, una integración, un job, un cambio en autenticación, etc. Es el flujo por defecto cuando ningún otro encaja.

## Cuándo usarlo
"Agregar la opción de anular facturas", "enviar un email cuando…", "importar desde Excel", "permitir login con…", "cambiar cómo se calcula…".

## Roles
**product-analyst** (requerimiento) → **solution-architect** (diseño si cruza módulos o agrega dependencias) → **backend-developer** (código) → **qa-engineer** (tests) → **security-reviewer** (si toca auth, permisos o datos sensibles).

## Archivos a leer
`docs/00-MASTER_CONTEXT.md`, `docs/modules/<módulo>.md`, `docs/03-requirements-index.md` y los standards según el tipo de cambio (`ai/context-packs.md`).

## Pasos
1. **Clasificar el cambio**: ¿es una entidad nueva? → `new-entity`. ¿Solo un endpoint? → `api-endpoint`. ¿Solo esquema? → `database-change`. Si es más amplio, seguir aquí.
2. **Requerimiento**: escribir o actualizar el FR en el módulo, con criterios de aceptación ("Dado / cuando / entonces") y los errores esperados. Confirmar con el usuario si hay ambigüedad.
3. **Diseño breve** (5 a 15 líneas): qué archivos cambian, qué endpoints, qué datos, qué transacción, qué efectos externos (emails, eventos, jobs). Si agrega dependencias nuevas o se aparta de un standard → ADR con `record-decision`.
4. **Sugerencias**: si el cambio activa un disparador de `ai/suggestions-catalog.md` (idempotencia, outbox, caché…), proponerlo.
5. **Implementar** siguiendo los standards. Escrituras de varios pasos con `ExecuteInTransactionAsync`; efectos externos después del commit.
6. **Compilar** tras cada archivo relevante.
7. **Tests** para cada criterio de aceptación y cada error nuevo (`test-generation`).
8. **Revisión de seguridad** si toca autenticación, permisos, datos personales o endpoints públicos (`security-review`).
9. **Analítica** (solo con `product-analytics`): si la funcionalidad responde a un KPI, agregar su evento con `analytics-event`.
10. **Documentar** con `docs-sync` (módulo, `docs/03`, `docs/06` si cambian estados, `docs/08` si hay integraciones).

## Reglas
- No mezclar cambios no pedidos ("ya que estaba, refactoricé…"). Si se detecta una mejora, proponerla aparte.
- Cambios incompatibles en un endpoint existente: preguntar si hay consumidores; considerar versionado.

## Salida esperada
Funcionalidad implementada con tests, documentación actualizada y, si aplica, ADR.

## Definition of Done
- [ ] FR con criterios de aceptación cubiertos por tests.
- [ ] `dotnet build` y `dotnet test` en verde.
- [ ] Revisión de seguridad hecha si aplicaba.
- [ ] Docs sincronizados y `PROJECT_STATUS` actualizado.
