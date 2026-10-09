# Entidades de negocio

> **Aplica a:** Todos los perfiles  
> **Propósito:** Cómo se crea cada entidad (`dotnet new kit-entity`), qué contiene cada archivo, cómo adaptarla y patrones adicionales.  
> **Código:** [`templates/entity/`](../templates/entity/) → `Features/{Entities}/` (4 archivos) + `tests/.../{Entities}ApiTests.cs`

> **IMPORTANTE para la IA:** crear **solo** las entidades acordadas con el usuario y documentadas en `docs/05-domain-model.md` y `docs/modules/`. Nunca entidades de ejemplo.

## Crear la entidad
Desde la raíz de la solución, con las mismas opciones del perfil del proyecto:
```bash
dotnet new kit-entity -n Customer --app {Project} --security true --database postgresql
dotnet new kit-entity -n Address --plural Addresses --parent Customer --app {Project} --security true --database postgresql
```

| Opción | Uso |
|---|---|
| `-n` | Entidad en singular, PascalCase, en inglés |
| `--app` | Nombre del proyecto (`{Project}`) |
| `--plural` | Si el plural no es `nombre + s` (`Address` → `Addresses`) |
| `--parent` / `--parent-plural` | Relación N:1 con una entidad ya creada (agrega FK, validación de existencia, filtro y `{Parent}Name` en el DTO) |
| `--security`, `--database`, `--apikey` | Igual que el proyecto (`docs/00-MASTER_CONTEXT.md`) |

## Qué genera

| Archivo | Contiene |
|---|---|
| `{Entity}.cs` | Entidad (`: BaseEntity`) + `{Entity}Configuration` (EF) + comentario **REGISTRO** |
| `{Entity}Contracts.cs` | `{Entity}Dto`, `Create/Update{Entity}Request`, `{Entity}Filter`, `{Entity}Errors`, `{Entity}Mappings` (proyección, `ToEntity`, `ApplyTo`) y validadores |
| `{Entity}Service.cs` | `I{Entity}Service` + `{Entity}Service` (CRUD con `Result`) + `{Entity}ByIdSpec` y `{Entities}ByFilterSpec` |
| `{Entities}Controller.cs` | GET paginado, GET por id, POST, PUT, DELETE; con seguridad, `[HasPermission]` en cada acción |
| `tests/.../{Entities}ApiTests.cs` | Duplicado → 409, inexistente → 404 y, con seguridad, sin token → 401 |

## Pasos después de generar
1. **Registro**: copiar las líneas del comentario REGISTRO de `{Entity}.cs`: el `DbSet` (y su `using`) en `AppDbContext` y, con seguridad, la clase de permisos en `Permissions.cs`. Borrar el comentario. El service **no** se registra: lo hace `AddApplication()`.
2. **Adaptar al dominio**: reemplazar `Name`/`Code` por las propiedades reales en los 4 archivos (entidad, configuración, DTO, requests, mapeo, validadores, búsqueda de la spec) y en el test. Si no hay código único, quitar `Code`, su índice y `CodeAlreadyExists`.
3. **Quitar lo que el módulo no pide** (por ejemplo, DELETE si la entidad no se elimina).
4. **Reglas de negocio** en el service como validaciones que devuelven `{Entity}Errors` y, si hace falta, los patrones de abajo.
5. `dotnet format` → `dotnet build` → `dotnet ef migrations add Add{Entity}` (revisar el SQL) → `dotnet test`.

## Reglas del molde
- El **validador** revisa forma (vacíos, longitudes, rangos). El **service** revisa lo que necesita BD (duplicados, existencia de FKs, estados).
- La búsqueda de texto usa `Contains` en SQL Server (collation sin mayúsculas) e `ILike` en PostgreSQL (`pg_trgm` en tablas grandes).
- Update con concurrencia optimista: el cliente devuelve `RowVersion` tal como lo leyó; si no coincide → 409 `{Entity}.Concurrency`.
- El service devuelve el DTO recién leído (`GetByIdAsync`) después de crear o actualizar, para que el cliente reciba `RowVersion` y datos calculados.
- Sin seguridad: decidir con el usuario si las escrituras existen, son públicas o van con API key (`--apikey`, `standards/09`).

## Patrones adicionales (solo si el dominio los requiere)

**A. Varios pasos en una transacción**
```csharp
public Task<Result<InvoiceDto>> CreateWithLinesAsync(CreateInvoiceRequest request, CancellationToken ct)
    => uow.ExecuteInTransactionAsync<InvoiceDto>(async token =>
    {
        var invoice = request.ToEntity();
        _repo.Add(invoice);
        await uow.SaveChangesAsync(token);       // obtener el Id dentro de la transacción
        // ... hijos con invoice.Id, otras tablas ...
        return new InvoiceDto(/* ... */);        // commit al salir si el Result es exitoso
    }, ct);
// Emails o HTTP: DESPUÉS, fuera de la transacción, y solo si el Result fue exitoso.
```

**B. "Solo uno activo"** (un único registro con `IsPrimary = true`, global o por padre):
- En una transacción: `ExecuteUpdateAsync` que apaga los demás (**excluyendo la propia fila**) asignando `UpdatedAt`, `UpdatedBy` y `RowVersion = Guid.NewGuid()` (no pasa por el interceptor), y luego se activa la entidad trackeada.
- Garantía en BD: índice único filtrado `HasIndex(e => new { e.CustomerId, e.IsPrimary }).IsUnique().HasFilter($"{SqlDialect.Column(nameof(Address.IsPrimary))} = {SqlDialect.True} AND {SqlDialect.NotDeleted}")`.

**C. Entidades casi idénticas** (mismas columnas y reglas): no duplicar services ni hacer `switch` sobre strings. Preferir una tabla con discriminador (TPH) y un `enum`; si no, una clase base y un service genérico; o handlers por tipo resueltos por DI.

**D. Reportes / stored procedures**: DTO plano + `SqlQuery<T>` parametrizado dentro de `I{Report}Queries` en `Infrastructure/` (`standards/02`, SQL crudo).

**E. Listas para combos**: spec con `OrderBy` y sin `ApplyPaging`, más una proyección mínima `(Id, Name)`.

## Definition of Done por entidad
- [ ] Generada con `kit-entity`, registrada (DbSet y permisos) y sin comentario REGISTRO.
- [ ] Sin `Name`/`Code` ni campos que el dominio no tenga; sin endpoints que el módulo no pida.
- [ ] Migración `Add{Entity}` con SQL revisado.
- [ ] `dotnet build -c Release` y `dotnet test` en verde, con tests de los criterios de aceptación.
- [ ] Docs: módulo, `docs/03`, `docs/05`, `docs/07` (con seguridad) y `PROJECT_STATUS`.
