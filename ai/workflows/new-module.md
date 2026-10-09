# Flujo: new-module (agregar un módulo)

## Objetivo
Definir un módulo nuevo (un área funcional con una o varias entidades) **documentado y aprobado** antes de implementarlo.

## Cuándo usarlo
El usuario pide una nueva área funcional ("agregar facturación", "módulo de inventario") que no existe en `docs/modules/`.

## Roles
**product-analyst** (entrevista y documentación) · **solution-architect** (integraciones, impacto, sugerencias) · después **backend-developer** vía `new-entity`.

## Archivos a leer
`docs/00-MASTER_CONTEXT.md`, `docs/modules/_TEMPLATE.md`, `docs/02-business-requirements.md`, `docs/03-requirements-index.md`, `docs/05-domain-model.md`, `docs/07-permissions-matrix.md` `[SEC]`, `docs/09-glossary.md` y `ai/suggestions-catalog.md`.

## Pasos
1. **Entender el objetivo**: qué problema de negocio resuelve, qué actores intervienen y qué queda fuera.
2. **Entrevista corta** (product-analyst): entidades y campos, reglas, estados y flujos, quién puede hacer qué, relación con módulos existentes, integraciones y notificaciones.
3. **Sugerencias** (solution-architect): revisar disparadores del catálogo (archivos, jobs, exportación, eventos…) y proponer lo que encaje, con referencias.
4. **Documentar**:
   - `docs/modules/<modulo>.md` desde la plantilla (código `MOD`, FR con criterios de aceptación, reglas, endpoints y errores).
   - BR nuevos en `docs/02`; FR en `docs/03`; entidades y relaciones en `docs/05` (con diagrama); flujos en `docs/06`; permisos en `docs/07` `[SEC]`; términos en `docs/09`; integraciones en `docs/08`.
   - Módulo en la tabla de `docs/00-MASTER_CONTEXT.md` y fase en `docs/10-roadmap.md`.
5. **Punto de control**: mostrar el resumen del módulo y esperar la aprobación del usuario.
6. **Implementar**: `new-entity` por cada entidad, en orden de dependencia, y luego funcionalidades adicionales con `new-feature` o `api-endpoint`.
7. **Cerrar** con `docs-sync`.

## Reglas
- Un módulo con más de 5 o 6 entidades, o cuyo documento pase de unas 250 líneas, probablemente son dos módulos: proponer dividirlo.
- Reutilizar entidades existentes en vez de duplicarlas (revisar `docs/05`).
- Usar los términos del glosario.

## Salida esperada
Documento del módulo aprobado y, tras la implementación, entidades y endpoints funcionando.

## Definition of Done
- [ ] Módulo documentado y aprobado.
- [ ] Entidades implementadas con `new-entity`; build y tests en verde.
- [ ] `docs/00`, `02`, `03`, `05`, `07` `[SEC]` y `10` actualizados.
