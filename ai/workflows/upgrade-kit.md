# Flujo: upgrade-kit (actualizar el kit en un proyecto existente)

## Objetivo
Traer a un proyecto existente la versión nueva del kit (`standards/`, `ai/`, adaptadores) **sin pisar lo propio del proyecto** (`docs/`, `src/`) y aplicando al código los cambios que los standards nuevos requieran.

## Cuándo usarlo
`KIT_VERSION` del proyecto es menor que la del repositorio del kit (`NetCore_API_Structure`).

## Roles
**devops-engineer** (actualización de archivos) · **solution-architect** (impacto) · **backend-developer** (cambios de código si los hay).

## Archivos a leer
`KIT_VERSION` y `CHANGELOG.md` del proyecto y del kit nuevo; `docs/decisions/` (desvíos aceptados que deben respetarse).

## Qué se actualiza y qué no

| Ruta | Acción |
|---|---|
| `standards/`, `ai/`, `AGENTS.md`, `CLAUDE.md`, `.claude/agents/`, `.claude/skills/`, `.cursor/rules/`, `.github/copilot-instructions.md` | **Reemplazar** con la versión del kit |
| `docs/` (proyecto) | **No tocar**. Solo agregar plantillas nuevas que no existan (`_TEMPLATE`, nuevos documentos numerados vacíos) |
| `src/`, `tests/` | **No reemplazar**. Aplicar cambios puntuales según el CHANGELOG |
| `.claude/settings.json` | **Fusionar**: conservar los permisos propios del proyecto |
| `KIT_VERSION`, `CHANGELOG.md` | Actualizar a la versión del kit |

## Pasos
1. **Comparar versiones** y leer el `CHANGELOG.md` del kit entre ambas. Clasificar:
   - **PATCH/MINOR**: textos, standards o flujos nuevos compatibles.
   - **MAJOR**: cambios incompatibles que requieren tocar código.
2. **Revisar ADRs** del proyecto: si un desvío aceptado choca con el kit nuevo, el ADR manda; anotarlo.
3. **Crear una rama** `chore/upgrade-kit-<version>`.
4. **Copiar** los archivos de la tabla ("Reemplazar") desde el kit.
5. **Plan de cambios de código**: por cada entrada del CHANGELOG que afecte código (por ejemplo, un middleware nuevo o un cambio en `ApiControllerBase`), listar los archivos del proyecto a modificar. **Mostrar el plan al usuario y esperar su aprobación.**
6. **Aplicar** los cambios aprobados, compilando tras cada uno.
7. **Verificar**: `dotnet build`, `dotnet test` y que la API arranque.
8. **Actualizar** `KIT_VERSION`, `KitVersion` en el Perfil (`docs/00-MASTER_CONTEXT.md`) y `PROJECT_STATUS`.
9. **Commit** `chore: actualizar kit a <version>` con el resumen de cambios.

## Reglas
- Nunca sobrescribir `docs/` ni código del proyecto sin aprobación.
- Si el salto es de varias versiones MAJOR, hacerlo de una versión en una versión.

## Definition of Done
- [ ] Archivos del kit actualizados; `docs/` intacto.
- [ ] Cambios de código aprobados y aplicados; build y tests en verde.
- [ ] `KIT_VERSION` y perfil actualizados.
