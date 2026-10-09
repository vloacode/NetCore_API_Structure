# Flujo: database-change (cambio de esquema)

## Objetivo
Cambiar el modelo de datos (columnas, tipos, índices, relaciones) con una **migración segura y revisada**, sin perder datos y sin romper despliegues.

## Cuándo usarlo
Agregar, renombrar o eliminar propiedades; cambiar tipos o longitudes; nuevos índices; cambiar relaciones; datos semilla.

## Roles
**database-architect** (diseño y revisión) · **backend-developer** (ajusta DTOs, specs y services) · **devops-engineer** (si requiere despliegue en dos pasos).

## Archivos a leer
`docs/05-domain-model.md`, `standards/02-domain-and-persistence.md`, `standards/02a-repository-unit-of-work.md`, `standards/11-performance.md` (índices) y `standards/13-deployment.md` (migraciones en producción).

## Pasos
1. **Clasificar el cambio**:
   - **Aditivo** (columna nullable o con default, tabla o índice nuevo): un solo despliegue.
   - **Destructivo** (eliminar o renombrar columna o tabla, reducir longitud, cambiar tipo, hacer obligatoria una columna con datos): **dos despliegues**.
2. **Destructivo, plan en dos pasos:**
   - Paso A: agregar lo nuevo, migrar datos (SQL en la migración), el código escribe en lo nuevo y lee lo nuevo. Lo viejo sigue existiendo.
   - Paso B (siguiente despliegue): eliminar lo viejo.
   - Documentar el plan en el PR o en un ADR.
3. **Modificar** la entidad y su `{Entity}Configuration` (longitudes, precisión, índices filtrados, `Restrict`).
4. **Ajustar** DTOs, mappings, specs, validadores y services afectados.
5. **Crear la migración** con nombre descriptivo:
   `dotnet ef migrations add <Nombre> -p src/{Project}.Api -o Infrastructure/Persistence/Migrations`
6. **Revisar el SQL**: `dotnet ef migrations script <Anterior> <Nueva> -p src/{Project}.Api`. Buscar `DROP`, `ALTER COLUMN` que reduzca tamaño y operaciones sobre tablas grandes que bloqueen.
7. **Datos existentes**: si se crea un índice único o una columna obligatoria, verificar o limpiar los datos primero (SQL en `migrationBuilder.Sql(...)`).
8. **Aplicar en local** (`dotnet ef database update`) y correr los tests.
9. **Documentar**: `docs/05` (tablas y diagrama) y nota de despliegue si es en dos pasos.

## Reglas
- **Nunca editar una migración ya aplicada** en un entorno compartido: crear otra.
- Nunca borrar la carpeta de migraciones ni regenerar `InitialCreate` en un proyecto con datos.
- Una migración por cambio lógico; nombres en inglés y en PascalCase (`AddInvoiceDueDate`).
- Tablas grandes: avisar del impacto (bloqueos, tiempo) y sugerir una ventana de mantenimiento.

## Salida esperada
Migración creada y revisada, código ajustado, tests en verde y plan de despliegue si aplica.

## Definition of Done
- [ ] SQL revisado sin pérdidas de datos no planificadas.
- [ ] Build y tests en verde con la migración aplicada en local.
- [ ] `docs/05` sincronizado.
- [ ] Plan de dos pasos documentado si fue destructivo.
