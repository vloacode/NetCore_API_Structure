# Flujo: new-entity (agregar una entidad de negocio)

## Objetivo
Crear una entidad de negocio completa (entidad, configuración EF, contratos, specs, validadores, service, controller, permisos, migración y tests) aplicando las plantillas del kit **sin desviarse**.

## Cuándo usarlo
- Desde `project-init` (paso 7), una vez por entidad.
- El usuario pide agregar una entidad o tabla nueva a un módulo.

## Roles
**backend-developer** (implementa) · **database-architect** (revisa configuración y migración) · **qa-engineer** (tests).

## Entradas
- Nombre de la entidad (singular, PascalCase) y su módulo.
- Definición en `docs/05-domain-model.md` y en `docs/modules/<módulo>.md`. **Si no está documentada, primero documentarla** con el usuario (product-analyst) y después seguir.

## Archivos a leer
`docs/00-MASTER_CONTEXT.md` (perfil), `docs/05-domain-model.md` (la entidad), `docs/modules/<módulo>.md`, `standards/07-business-entity-templates.md`, `standards/07a-additional-patterns.md` y, con seguridad, `standards/06-authorization-permissions.md`.

## Pasos
1. **Confirmar la definición**: propiedades (tipo, longitud, obligatoria, única), relaciones, reglas e invariantes, filtros del listado y permisos. Si falta algo, preguntar.
2. **Definir los reemplazos** y escribirlos antes de empezar:
   `{Entity}`, `{Entities}`, `{entity}`, `{entities}`, `{Parent}`/`{parent}` (si tiene FK) y qué propiedades reales reemplazan a `Name` y `Code`.
3. **Entidad** `Domain/Entities/{Entity}.cs` (`standards/07`, sección Entidad).
4. **Configuración EF** `Infrastructure/Persistence/Configurations/{Entity}Configuration.cs` y `DbSet` en `AppDbContext`.
5. **Contratos** `Application/Features/{Entities}/{Entity}Contracts.cs`: DTO, Create/Update requests, Filter, `{Entity}Errors` y `{Entity}Mappings`.
6. **Especificaciones** `{Entity}Specifications.cs`: por Id y por filtro, con orden estable y desempate por Id.
7. **Validadores** `{Entity}Validators.cs`.
8. **Service** `{Entity}Service.cs` + interfaz. Agregar las reglas de negocio del módulo como validaciones que devuelven `{Entity}Errors`. Aplicar los patrones de `standards/07a` si el dominio los requiere ("solo uno activo", transacción de varios pasos).
9. **Controller** `Api/Controllers/{Entities}Controller.cs`. Con seguridad, `[HasPermission]` en cada acción; sin seguridad, la variante pública (y API key en escrituras si el perfil lo indica).
10. **Registro**: service en `AddApplication()`. Con seguridad, `Permissions.{Entities}` en `Permissions.cs` y, si corresponde, en `DefaultUserPermissions`.
11. **Formatear y compilar**: `dotnet format` y después `dotnet build`. Corregir hasta que compile sin warnings nuevos.
12. **Buscar marcadores olvidados** en los archivos nuevos: no debe quedar `{Entity}`, `{Parent}`, `{entities}` ni campos de ejemplo.
13. **Migración**: `dotnet ef migrations add Add{Entity} ...` y revisar el SQL generado (database-architect).
14. **Tests**: casos mínimos por entidad de `standards/12` (flujo `test-generation`).
15. **Documentación** (flujo `docs-sync`): endpoints y errores en el módulo, FR con endpoint y test en `docs/03`, permisos en `docs/07` `[SEC]`, `PROJECT_STATUS`.

## Reglas
- Copiar las plantillas y adaptar; no reescribir desde cero con otro estilo.
- No agregar endpoints que no pidan los requerimientos (por ejemplo, si la entidad no se elimina, no crear DELETE).
- Si una regla de negocio requiere algo que la plantilla no cubre, aplicar un patrón de `07a` o proponer un ADR.

## Salida esperada
Entidad funcional con sus endpoints, compilando, con migración, tests y documentación actualizada. Resumen con archivos creados, endpoints y permisos.

## Definition of Done
- [ ] Los 15 pasos completos.
- [ ] `dotnet build` y `dotnet test` en verde.
- [ ] Sin marcadores sin reemplazar.
- [ ] Docs sincronizados (módulo, `03`, `05`, `07` `[SEC]`, `PROJECT_STATUS`).
