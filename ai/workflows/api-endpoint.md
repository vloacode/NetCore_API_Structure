# Flujo: api-endpoint (endpoint nuevo o cambio de endpoint)

## Objetivo
Agregar o modificar un endpoint sobre una entidad existente respetando el contrato de la API: capas, `Result`, ProblemDetails, permisos, paginación y comodidad para el frontend.

## Cuándo usarlo
"Agregar un endpoint para activar…", "un GET que devuelva el resumen de…", "cambiar el filtro del listado", "exponer una exportación".

## Roles
**backend-developer** · **qa-engineer** (tests) · **security-reviewer** si el endpoint es público o maneja datos sensibles.

## Archivos a leer
`docs/00-MASTER_CONTEXT.md`, `docs/modules/<módulo>.md`, `standards/03-application-layer.md`, `standards/04-api-conventions.md`, `standards/08-error-codes.md`, `standards/15-frontend-integration.md` y, con seguridad, `standards/06-authorization-permissions.md`.

## Pasos
1. **Definir el contrato** y anotarlo en la tabla de endpoints del módulo:
   - Método y ruta REST: sustantivos en plural; acciones de negocio como sub-recurso (`POST /api/{entities}/{id}/activate`).
   - Request (DTO + validador), response (DTO o `PagedResult<T>`) y códigos HTTP posibles.
   - Permiso `[SEC]` (existente o nuevo) y errores (`{Entity}Errors`).
2. **Service**: método nuevo que devuelve `Result`/`Result<T>`. Lecturas con spec y proyección; escrituras con `SaveChangesAsync` o `ExecuteInTransactionAsync`.
3. **Spec** nueva si hay filtro u orden distinto (nombre de negocio, orden estable).
4. **Validador** para el request nuevo.
5. **Controller**: acción delgada que solo llama al service y usa `HandleResult` / `HandleCreated`. `[ProducesResponseType<T>]` para que OpenAPI genere los tipos exactos. `[HasPermission]` `[SEC]`.
6. **Permiso nuevo** `[SEC]`: agregarlo en `Permissions.cs` y en `docs/07`.
7. **Compilar** y probar en `/scalar`.
8. **Tests**: éxito, validación, error de negocio, 404 y permisos `[SEC]`.
9. **Documentar** (`docs-sync`).

## Reglas
- Verbos correctos: GET lee (nunca modifica), POST crea o ejecuta una acción, PUT reemplaza, DELETE elimina. Sin PATCH salvo ADR.
- Toda lista paginada (`PaginationParams`) con orden estable.
- Nunca devolver entidades EF; nunca recibir campos que el cliente no debe controlar (`CreatedBy`, `IsDeleted`, ids de dueño).
- Cambiar la forma de un endpoint existente rompe a los clientes: preguntar antes; agregar campos es seguro, quitar o renombrar no.
- Operaciones largas: 202 Accepted + estado (`standards/15`).

## Salida esperada
Endpoint funcionando, documentado en el módulo y en OpenAPI, con tests.

## Definition of Done
- [ ] Contrato documentado en el módulo.
- [ ] Controller delgado, service con `Result`, validador y permiso `[SEC]`.
- [ ] Build y tests en verde.
- [ ] `docs/03` actualizado con endpoint y test.
