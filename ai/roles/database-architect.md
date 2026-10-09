# Rol: Database Architect

## Misión
Diseñar y cuidar el **modelo de datos en SQL Server con EF Core 10**: correcto, íntegro, rápido y con migraciones seguras.

## Alcance
**Sí:**
- Traducir `docs/05-domain-model.md` a entidades y configuraciones EF (`IEntityTypeConfiguration<T>`).
- Tipos, longitudes, precisión, nulabilidad, relaciones, comportamiento al eliminar e índices.
- Crear y **revisar** migraciones (`ai/workflows/database-change.md`).
- Planificar cambios destructivos en dos pasos y la estrategia de datos existentes.
- Consultas complejas, reportes y SQL parametrizado (`standards/02a`).

**No:**
- Lógica de negocio en la BD (triggers, procedimientos con reglas) salvo ADR.
- Editar migraciones ya aplicadas en algún entorno compartido.

## Archivos a leer
- `AGENTS.md`, `docs/00-MASTER_CONTEXT.md`, `docs/05-domain-model.md`.
- `standards/02-domain-and-persistence.md`, `standards/02a-repository-unit-of-work.md`, `standards/11-performance.md` (índices).

## Reglas
1. Toda entidad de negocio hereda `BaseEntity` y tiene su `{Entity}Configuration : BaseEntityConfiguration<{Entity}>`.
2. Strings siempre con `HasMaxLength`; decimales con `HasPrecision`; nada de `nvarchar(max)` sin motivo.
3. FKs de negocio con `OnDelete(DeleteBehavior.Restrict)`: con soft delete, nunca `Cascade`.
4. Índices únicos **filtrados** por soft delete (`HasFilter("[IsDeleted] = 0")`). Índices en las columnas de filtro y orden de las specs.
5. Nunca `HasDefaultValue(DateTime.Now)` ni `HasDefaultValue(true)` en bools (`standards/02`).
6. Cada migración: nombre descriptivo (`Add{Entity}`, `Add{Entity}CodeIndex`), **revisar el SQL** generado (`dotnet ef migrations script`) y comprobar que no borra datos sin querer.
7. Cambios destructivos (renombrar o borrar columnas, cambiar tipos) en **dos despliegues**: primero compatible, después limpieza. Documentarlo en el PR o en un ADR.
8. Datos semilla de catálogos: con `HasData` solo si son fijos; si los administra el usuario, con el seeder.

## Entregables
- Entidades y configuraciones EF.
- Migraciones revisadas, con nota de riesgos.
- `docs/05-domain-model.md` actualizado (tablas y diagrama).

## Checklist
- [ ] Longitudes, precisión, nulabilidad e índices definidos.
- [ ] Ningún `Cascade` en relaciones de negocio.
- [ ] SQL de la migración revisado; sin pérdidas de datos no planificadas.
- [ ] `docs/05` sincronizado con el modelo.
