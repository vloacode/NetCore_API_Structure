# Flujo: record-decision (registrar una decisión / ADR)

## Objetivo
Dejar por escrito **qué se decidió, por qué y qué alternativas se descartaron**, para que ninguna IA o persona futura deshaga la decisión sin saberlo.

## Cuándo usarlo
- Algo se aparta de un standard del kit.
- Se elige entre alternativas técnicas importantes (proveedor, librería, patrón).
- Se acepta, pospone o descarta una sugerencia de `ai/suggestions-catalog.md`.
- `project-init` (ADR-0001 con las decisiones iniciales).

## Roles
**solution-architect**. Las decisiones las toma el usuario.

## Archivos a leer
`docs/decisions/ADR-0000-template.md` y los ADRs existentes relacionados (para no contradecirlos sin reemplazarlos).

## Pasos
1. **Número**: el siguiente correlativo de 4 dígitos en `docs/decisions/`.
2. **Contexto**: hechos y restricciones que obligan a decidir, sin proponer todavía la solución.
3. **Opciones**: al menos dos, con ventajas y desventajas reales. Incluir "seguir el standard del kit" cuando aplique.
4. **Recomendación** de la IA, con el porqué; si hay acceso web, con referencias actuales.
5. **Decisión del usuario**: preguntarle explícitamente; registrar la que elija.
6. **Consecuencias**: positivas, costos, y qué archivos o standards se ven afectados.
7. **Guardar** como `docs/decisions/ADR-NNNN-titulo-en-kebab-case.md` con estado `Aceptada` (o `Propuesta` si queda pendiente).
8. Si reemplaza un ADR anterior: marcar el viejo como `Reemplazada por ADR-NNNN`.
9. Si es clave, agregarla a "Decisiones clave" de `docs/00-MASTER_CONTEXT.md`.

## Reglas
- Un ADR por decisión. Los ADR aceptados no se editan: se reemplazan con uno nuevo.
- Breve: una página como máximo.
- Si la decisión implica mejorar el kit para todos los proyectos, anotarlo en el ADR como "Propuesta para el kit".

## Definition of Done
- [ ] ADR creado con contexto, opciones, decisión y consecuencias.
- [ ] Decisión confirmada por el usuario.
- [ ] ADRs reemplazados marcados y `00-MASTER_CONTEXT` actualizado si aplica.
