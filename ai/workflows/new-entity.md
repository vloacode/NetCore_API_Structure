# Flujo: new-entity (agregar una entidad de negocio)

## Objetivo
Crear una entidad de negocio completa (entidad + configuración EF, contratos, validadores, service + specs, controller, permisos, migración y tests) con `dotnet new kit-entity`, adaptada al dominio y **sin desviarse** del molde.

## Cuándo usarlo
- Fase 2 del plan (`ai/implementation-phases.md`): una vez por entidad, en orden de dependencia (padres primero).
- El usuario pide agregar una entidad o tabla nueva a un módulo.

## Roles
**backend-developer** (implementa) · **database-architect** (revisa configuración y migración) · **qa-engineer** (tests).

## Entradas
- Nombre de la entidad (singular, PascalCase, en inglés), su plural y su módulo.
- Definición en `docs/05-domain-model.md` y `docs/modules/<módulo>.md`. **Si no está documentada, primero documentarla** con el usuario (product-analyst).

## Archivos a leer (solo estos)
`docs/00-MASTER_CONTEXT.md` (perfil), la entidad en `docs/05-domain-model.md`, `docs/modules/<módulo>.md` y `standards/07-entities.md`. Con seguridad, además `standards/06-authorization-permissions.md`.

## Pasos
1. **Confirmar la definición**: propiedades (tipo, longitud, obligatoria, única), relación con un padre, reglas e invariantes, filtros del listado, endpoints que pide el módulo y permisos. Si falta algo, preguntar.
2. **Generar** desde la raíz de la solución, con las opciones del perfil:
   ```bash
   dotnet new kit-entity -n {Entity} [--plural {Entities}] [--parent {Parent}] --app {Project} --security <true|false> --database <sqlserver|postgresql> [--apikey true]
   ```
3. **Registrar**: copiar las líneas del comentario REGISTRO de `{Entity}.cs` (`DbSet` + `using` en `AppDbContext`; con seguridad, la clase en `Permissions.cs` y, si corresponde, `DefaultUserPermissions`). Borrar el comentario.
4. **Adaptar al dominio** en los 4 archivos y en el test: reemplazar `Name`/`Code` por las propiedades reales (entidad, configuración, DTO, requests, mapeo, validadores, búsqueda de la spec) y quitar los endpoints que el módulo no pide.
5. **Reglas de negocio** en el service, como validaciones que devuelven `{Entity}Errors`. Patrones de `standards/07` ("Patrones adicionales") si el dominio los requiere.
6. **Formatear y compilar**: `dotnet format` y `dotnet build`. Corregir hasta que compile sin warnings nuevos.
7. **Migración**: `dotnet ef migrations add Add{Entity} -p src/{Project}.Api -o Infrastructure/Persistence/Migrations` y revisar el SQL (database-architect).
8. **Tests**: completar `{Entities}ApiTests.cs` con un caso por criterio de aceptación y por error nuevo (`test-generation`). `dotnet test` en verde.
9. **Analítica** (solo con `product-analytics`): si algún KPI de `docs/11` depende de esta entidad, agregar el evento con `analytics-event`.
10. **Documentación** (`docs-sync`): endpoints y errores en el módulo, FR con endpoint y test en `docs/03`, permisos en `docs/07` (con seguridad) y `PROJECT_STATUS` (ítem ✅ en el plan de fases).

## Reglas
- Generar siempre con `kit-entity`; no escribir los archivos a mano ni con otro estilo.
- No agregar endpoints que no pidan los requerimientos (si la entidad no se elimina, quitar DELETE).
- Si una regla requiere algo que la plantilla no cubre, aplicar un patrón de `standards/07` o proponer un ADR.
- Una entidad por sesión cuando el contexto es limitado.

## Salida esperada
Entidad funcional con sus endpoints, compilando, con migración, tests y documentación. Resumen con archivos creados, endpoints y permisos.

## Definition of Done
- [ ] Generada, registrada y sin comentario REGISTRO.
- [ ] Sin `Name`/`Code` ni campos que el dominio no tenga.
- [ ] `dotnet build -c Release` y `dotnet test` en verde.
- [ ] Migración revisada.
- [ ] Docs sincronizados (módulo, `03`, `05`, `07` con seguridad, `PROJECT_STATUS`).
