# Lecciones aprendidas

> **Aplica a:** Todos los perfiles  
> **Propósito:** Errores de una implementación anterior y la regla que los evita. Explica el porqué de las reglas.  
> Índice general: `standards/00-INDEX.md`

## Anexo: errores que esta arquitectura evita

Lecciones de una implementación anterior del patrón. Explican el **porqué** de las reglas:

| Problema visto | Regla que lo evita |
|---|---|
| Una propiedad `success` hacía `iserror = true ? …` (asignaba en vez de comparar): leerla corrompía la respuesta | `Result` inmutable, sin setters, con `IsSuccess` / `IsFailure` |
| Métodos `Delete` que llamaban `Remove` pero nunca `SaveChanges` | Plantilla de service con `SaveChangesAsync` explícito y tests de borrado |
| `GetByIdAsync` hacía `Entry(null)` y explotaba antes de validar si existía | `FindAsync` devuelve `null` y el service responde `NotFound` |
| Paginación con `Take` antes de `Skip` y `OrderBy` después de paginar | `SpecificationEvaluator` con orden fijo y excepción si se pagina sin orden |
| Parámetros `(take, skip)` en la interfaz y `(skip, take)` en la implementación | Paginación encapsulada en `PaginationParams` |
| SQL de stored procedure armado concatenando strings | Solo `SqlQuery` / `FromSqlInterpolated` parametrizados |
| `TransactionScope` con doble `Dispose`, sin rollback en excepciones y `DbContext` dispuesto por el UoW | `ExecuteInTransactionAsync` con `IDbContextTransaction` y estrategia de reintentos; el UoW no dispone el contexto |
| Guardados fuera de la transacción por empezarla tarde | La transacción envuelve toda la operación en un lambda |
| Auditoría (`CreatedBy`, `UpdatedBy`…) repetida en cada método, a veces con errores | `AuditableEntityInterceptor` |
| `HasDefaultValue(DateTime.Now)`: fecha congelada al generar la migración | `HasDefaultValueSql("SYSUTCDATETIME()")` y `TimeProvider` |
| Métodos `GetAll` que devolvían `null` | Todo devuelve `Result<T>` |
| Páginas o vistas que enlazaban el objeto de respuesta completo (overposting) | Requests dedicados (`Create{Entity}Request` / `Update{Entity}Request`) |
| Un `switch` por strings con 8 tablas casi idénticas y código copiado | `standards/07a-additional-patterns.md` patrón C (TPH, service genérico o handlers) |
| Consultas N+1 de nombres de usuario | Proyecciones con joins en una sola consulta |
| Includes por string sin chequeo de compilación | Includes tipados en `Specification<T>` |
