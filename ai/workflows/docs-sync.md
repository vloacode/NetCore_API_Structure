# Flujo: docs-sync (sincronizar documentación con el código)

## Objetivo
Que `docs/` refleje **exactamente** el estado real del código después de un cambio, para que la próxima tarea parta de información correcta.

## Cuándo usarlo
- Al final de cualquier tarea con código (forma parte de la Definition of Done).
- Cuando se sospecha que los docs están desactualizados ("¿qué endpoints tenemos?").

## Roles
**technical-writer**.

## Archivos a leer
`docs/00-MASTER_CONTEXT.md`, `docs/03-requirements-index.md`, `docs/modules/<módulos afectados>.md`, `docs/05-domain-model.md`, `docs/07-permissions-matrix.md` `[SEC]`, `docs/ai/PROJECT_STATUS.md` y el diff o la lista de cambios.

## Pasos
1. **Inventario real** desde el código (no desde la memoria):
   - Endpoints: atributos `[Http*]` de los controllers.
   - Permisos `[SEC]`: `Permissions.cs` y `[HasPermission]` usados.
   - Entidades y relaciones: `Features/{Entities}/{Entity}.cs` (entidad y configuración EF).
   - Errores: clases `{Entity}Errors`.
   - Tests: nombres en `tests/`.
2. **Módulo**: tabla de endpoints, errores, pruebas y estado de cada FR.
3. **`docs/03`**: endpoint, permiso, test y estado de cada FR; recalcular la cobertura y los FR sin cubrir.
4. **`docs/05`**: entidades, propiedades relevantes, relaciones y diagrama.
5. **`docs/07`** `[SEC]`: permisos nuevos y su asignación por rol (coherente con `DefaultUserPermissions` y el seed).
6. **`docs/06` y `08`**: si cambiaron estados, integraciones, eventos o jobs.
7. **`docs/ai/PROJECT_STATUS.md`**: hecho, en curso, siguiente, salud técnica.
8. **`docs/00-MASTER_CONTEXT.md`**: estado de módulos y estado actual (manteniéndolo corto).
9. **Inconsistencias de negocio** (el código hace algo distinto de lo documentado): no elegir por cuenta propia; preguntar al usuario cuál es el correcto.

## Reglas
- No copiar código ni esquemas completos a los docs; enlazar o resumir.
- No borrar los comentarios `<!-- IA: ... -->` de las plantillas.
- Respetar los límites de tamaño (ver `ai/roles/technical-writer.md`).

## Definition of Done
- [ ] Endpoints, permisos, entidades, errores y tests documentados coinciden con el código.
- [ ] `docs/03` y `PROJECT_STATUS` al día.
- [ ] Inconsistencias de negocio resueltas con el usuario o listadas como preguntas abiertas.
