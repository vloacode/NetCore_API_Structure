# Persistencia: EF Core, Repository y Unit of Work

> **Aplica a:** Todos los perfiles y ambos motores (SQL Server y PostgreSQL)  
> **Propósito:** Entidad base, IRepository/IUnitOfWork, AppDbContext, dialecto SQL, auditoría, soft delete, concurrencia, transacciones y SQL crudo.  
> **Código:** [`Infrastructure/Persistence/`](../templates/api/src/KitApi.Api/Infrastructure/Persistence/) · [`Application/Abstractions/Persistence/`](../templates/api/src/KitApi.Api/Application/Abstractions/Persistence/) · [`Domain/Common/BaseEntity.cs`](../templates/api/src/KitApi.Api/Domain/Common/BaseEntity.cs)

## Piezas y responsabilidad

| Pieza | Archivo | Qué hace |
|---|---|---|
| `BaseEntity` | `Domain/Common/BaseEntity.cs` | `Id` int, auditoría (`CreatedAt/By`, `UpdatedAt/By`), soft delete (`IsDeleted`, `DeletedAt/By`), `RowVersion` (Guid) |
| `IRepository<T>` / `Repository<T>` | `Abstractions/Persistence`, `Repositories/Repository.cs` | Lecturas con spec y proyección, `AnyAsync`, `Add`, `Remove`, `ExecuteUpdate/DeleteAsync`. **No guarda.** |
| `SpecificationEvaluator` | `Repositories/SpecificationEvaluator.cs` | Aplica filtro, includes, orden, paginación, tracking y split query de la spec |
| `IUnitOfWork` / `UnitOfWork` | `UnitOfWork.cs` | `Repository<T>()`, `SaveChangesAsync`, `ExecuteInTransactionAsync` (con la estrategia de reintentos del proveedor) |
| `AppDbContext` | `AppDbContext.cs` | DbSets, convención UTC, configuraciones del ensamblado y filtro global de soft delete |
| `AuditableEntityInterceptor` | `Interceptors/` | Llena auditoría, convierte `Remove` en soft delete y renueva `RowVersion` |
| `SqlDialect` | `SqlDialect.cs` | SQL que depende del motor (filtros de índices): `SqlDialect.NotDeleted`, `Column()`, `True/False` |
| `BaseEntityConfiguration<T>` | `Configurations/` | Clave, auditoría, `RowVersion` como token de concurrencia |

**Por qué funciona la transacción:** `IUnitOfWork` es scoped. En un request, todos los services comparten el **mismo `AppDbContext`**, y `UserManager`/`RoleManager` de Identity también. Una transacción abierta con `ExecuteInTransactionAsync` cubre todo, incluido Identity.

## Reglas
1. Toda entidad de negocio hereda `BaseEntity` y tiene su `{Entity}Configuration : BaseEntityConfiguration<{Entity}>` (en el mismo archivo `{Entity}.cs`, `standards/07`).
2. Cada entidad necesita su `DbSet` en `AppDbContext` (el nombre del DbSet es el nombre de la tabla). **Sin `ToTable`**: en PostgreSQL rompería la conversión a `snake_case`.
3. Strings con `HasMaxLength`; decimales con `HasPrecision`.
4. FKs de negocio con `OnDelete(DeleteBehavior.Restrict)`: con soft delete, nunca `Cascade`.
5. Índices únicos **filtrados** por soft delete: `.HasFilter(SqlDialect.NotDeleted)`. Condiciones propias: `$"{SqlDialect.Column(nameof(X.Flag))} = {SqlDialect.True}"`.
6. ⚠️ **Nunca `HasDefaultValue(true)` en un `bool`**: EF no envía `false` (es el default de C#) y la BD guardaría `true`. Usar el inicializador de C# (`= true`).
7. **Concurrencia con `Guid RowVersion`**, no `rowversion`: funciona igual en ambos motores (mismo enfoque que el `ConcurrencyStamp` de Identity). Toda actualización masiva con `ExecuteUpdateAsync` debe asignar `RowVersion = Guid.NewGuid()`.
8. Fechas: todas se guardan y leen como UTC (`UtcDateTimeConverter`), así el JSON sale con `Z`.
9. `ExecuteDeleteAsync` hace **borrado físico** (salta el soft delete): solo en tablas técnicas (refresh tokens vencidos, eventos de analítica).
10. Un service que necesita varios repositorios los pide al mismo `uow`; nunca inyecta `AppDbContext`.

## Transacciones
```csharp
return await uow.ExecuteInTransactionAsync(async ct =>
{
    // varios pasos con repositorios del mismo uow...
    if (algoFalla) return Result.Failure(XErrors.Algo);   // rollback
    await uow.SaveChangesAsync(ct);
    return Result.Success();                              // commit
}, ct);
// Emails, colas o HTTP: DESPUÉS, fuera de la transacción.
```

## SQL crudo y stored procedures (siempre parametrizado)
```csharp
var rows = await context.Database
    .SqlQuery<ReportRow>($"EXEC dbo.SalesReport @From = {from}, @To = {to}")      // SQL Server: procedimiento
    .SqlQuery<ReportRow>($"SELECT * FROM sales_report({from}, {to})")             // PostgreSQL: función
    .ToListAsync(ct);
```
Los valores interpolados se convierten en parámetros. **Nunca** armar SQL con `+` ni `string.Format`. Si un service lo necesita, se expone con un repositorio específico (`I{Report}Queries`) implementado en `Infrastructure/`.

## Migraciones
```bash
dotnet ef migrations add Add{Entity} -p src/{Project}.Api -o Infrastructure/Persistence/Migrations
dotnet ef migrations script --idempotent -p src/{Project}.Api   # revisar el SQL antes de aplicarlo
```
Nombre descriptivo, SQL revisado (flujo `database-change`), y cambios destructivos en dos despliegues (`ai/roles/database-architect.md`).
