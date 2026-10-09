# Flujo: performance-review (revisión de rendimiento)

## Objetivo
Encontrar y corregir cuellos de botella **con mediciones**, contra los objetivos de `docs/04-non-functional-requirements.md`.

## Cuándo usarlo
- Un endpoint está lento o hay alertas de latencia.
- Antes de producción, en endpoints de mucho tráfico o con mucho volumen de datos.
- El usuario pide "optimizar".

## Roles
**observability-engineer** (diagnóstico) · **database-architect** (índices y consultas) · **backend-developer** (cambios de código).

## Archivos a leer
`docs/04-non-functional-requirements.md`, `standards/11-performance.md`, `standards/02a-repository-unit-of-work.md` y el código de los endpoints involucrados.

## Pasos
1. **Definir el escenario**: endpoint, parámetros, volumen de datos y objetivo (p95).
2. **Medir la línea base**: tiempos del endpoint (trazas de OpenTelemetry, logs) y SQL generado (logs de EF `Microsoft.EntityFrameworkCore.Database.Command` en desarrollo, o Query Store).
3. **Revisar contra el checklist** de `standards/11`:
   - Proyección vs entidades completas; tracking innecesario.
   - N+1 (consultas dentro de bucles); `Include` de varias colecciones sin `AsSplitQuery`.
   - Listas sin paginar; filtros u orden sin índice; funciones sobre columnas en el `WHERE`.
   - Llamadas a terceros sin caché ni timeout; código síncrono bloqueante.
4. **Proponer cambios** ordenados por impacto/esfuerzo: índice, proyección, spec, caché (HybridCache), `ExecuteUpdate`, etc.
5. **Aplicar** (con aprobación del usuario si hay cambios de esquema → `database-change`).
6. **Medir de nuevo** con el mismo escenario y comparar.
7. **Documentar** el resultado y, si hubo una lección, en `docs/ai/AI_MEMORY.md`.

## Formato del reporte
| Hallazgo | Evidencia | Cambio | Antes | Después |
|---|---|---|---|---|
| N+1 en GetPaged | 21 consultas por petición (log de EF) | Proyección con join | 850 ms p95 | 120 ms p95 |

## Reglas
- Sin medición no hay optimización: no cambiar código "por si acaso".
- No sacrificar la correctitud ni las reglas del kit (por ejemplo, no saltarse capas por velocidad).
- Caché solo donde los datos lo permiten (ver "qué no cachear" en `standards/11`).

## Definition of Done
- [ ] Línea base y resultado medidos con el mismo escenario.
- [ ] Objetivo de `docs/04` cumplido o brecha explicada.
- [ ] Tests en verde tras los cambios.
