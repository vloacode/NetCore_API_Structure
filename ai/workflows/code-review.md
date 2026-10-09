# Flujo: code-review (revisión de código)

## Objetivo
Revisar un cambio (diff, PR o carpeta) y entregar **hallazgos accionables** sobre corrección, cumplimiento de los standards y Definition of Done. La revisión es de **solo lectura**.

## Cuándo usarlo
Antes de un merge, al terminar una tarea grande o cuando el usuario pide "revisa esto".

## Roles
**qa-engineer** (revisión general). Si el cambio toca autenticación, permisos o datos sensibles, también **security-reviewer** (`security-review`).

## Archivos a leer
`standards/01-solution-architecture.md`, `standards/99-lessons-learned.md`, `docs/modules/<módulo>.md`, el diff (`git diff main...HEAD` o el que indique el usuario) y los standards específicos que toque el cambio.

## Pasos
1. **Entender la intención**: qué FR o tarea implementa. Si no está claro, preguntar.
2. **Correctitud**: ¿hace lo que piden los criterios de aceptación? Casos borde, nulls, concurrencia, transacciones y efectos externos dentro de transacciones.
3. **Reglas del kit** (las 16 de `standards/01`), con especial atención a:
   - Capas respetadas; controller delgado.
   - `Result` + `{Entity}Errors`; sin excepciones para flujo de negocio.
   - DTOs en la API; proyecciones en las lecturas; spec con orden estable al paginar.
   - `CancellationToken` y `TimeProvider`; nada de `DateTime.Now`.
   - Permisos en cada acción `[SEC]`; SQL parametrizado.
4. **Lecciones aprendidas**: comprobar que no se repite ninguno de los errores de `standards/99`.
5. **Tests**: ¿cubren los criterios y los errores nuevos? ¿Pasan?
6. **Documentación**: ¿se actualizaron el módulo, `docs/03`, `docs/07` `[SEC]` y `PROJECT_STATUS`?
7. **Verificar** con `dotnet build` y `dotnet test` (solo lectura del resultado).
8. **Entregar el reporte.**

## Formato del reporte
Ordenado por severidad:
- **Bloqueante**: bug, pérdida de datos, falla de seguridad o regla del kit rota.
- **Importante**: falta de tests, rendimiento, mantenibilidad seria.
- **Menor**: estilo o claridad.

Cada hallazgo: `archivo:línea`, el problema, por qué importa (escenario concreto) y la corrección sugerida. Al final, un veredicto: **Aprobar** / **Aprobar con cambios menores** / **Requiere cambios**.

## Reglas
- No modificar archivos.
- No reportar preferencias personales como problemas. Si algo cumple los standards, está bien.
- Un hallazgo sin escenario concreto no es un hallazgo.

## Definition of Done
- [ ] Todos los archivos del cambio revisados.
- [ ] Build y tests verificados.
- [ ] Reporte entregado con veredicto.
