# Patrones adicionales y checklist por entidad

> **Aplica a:** Todos los perfiles  
> **Propósito:** Transacciones de varios pasos, 'solo uno activo', entidades casi idénticas, reportes, registro y checklist.  
> Índice general: `standards/00-INDEX.md`

### Patrones adicionales (aplicar solo si el dominio los requiere)

**A. Operación de varios pasos en una transacción**
```csharp
public Task<Result<{Entity}Dto>> CreateWithDetailsAsync(Create{Entity}Request request, CancellationToken ct)
    => uow.ExecuteInTransactionAsync<{Entity}Dto>(async token =>
    {
        if (await _repo.AnyAsync(e => e.Code == request.Code, token))
            return {Entity}Errors.CodeAlreadyExists(request.Code);

        var entity = request.ToEntity();
        _repo.Add(entity);
        await uow.SaveChangesAsync(token);       // obtener Id dentro de la transacción

        // ... agregar hijos con entity.Id, afectar otras tablas, etc.

        return new {Entity}Dto(/* ... */);       // SaveChanges final + Commit los hace ExecuteInTransactionAsync
    }, ct);
// Emails o llamadas HTTP: DESPUÉS, fuera de la transacción, y solo si el Result fue exitoso.
```

**B. "Solo uno activo" (un único registro con `{Flag} = true`)**
```csharp
public Task<Result> Set{Flag}Async(int id, CancellationToken ct)
    => uow.ExecuteInTransactionAsync(async token =>
    {
        var entity = await _repo.GetByIdAsync(id, token);
        if (entity is null) return {Entity}Errors.NotFound(id);

        // Excluir la propia fila: si ExecuteUpdate la tocara, cambiaría su RowVersion
        // y el SaveChanges posterior fallaría por concurrencia.
        var now = clock.GetUtcNow().UtcDateTime;   // inyectar TimeProvider clock e ICurrentUserService currentUser
        var userId = currentUser.UserId;
        await _repo.ExecuteUpdateAsync(e => e.{Flag} && e.Id != id, s => s
            .SetProperty(e => e.{Flag}, false)
            .SetProperty(e => e.UpdatedAt, now)          // ExecuteUpdate no pasa por el interceptor
            .SetProperty(e => e.UpdatedBy, userId), token);

        entity.{Flag} = true;
        return Result.Success();
    }, ct);
```
Con garantía en BD: `builder.HasIndex(e => e.{Flag}).IsUnique().HasFilter("[{Flag}] = 1 AND [IsDeleted] = 0");`. Si el "uno activo" es por padre, el índice es `HasIndex(e => new { e.{Parent}Id, e.{Flag} })` con el mismo filtro.

**C. Varias entidades casi idénticas** (mismas columnas y mismas reglas): no duplicar services ni usar `switch` sobre strings.
- **Opción 1 (preferida):** una sola tabla con discriminador (TPH) y un `enum` de tipo.
- **Opción 2:** clase base común `{Base}Entity` y un service genérico `CrudService<TEntity>` con la lógica compartida; cada tipo solo añade lo propio.
- **Opción 3:** handlers registrados por tipo (`IDictionary<{Tipo}Enum, I{Tipo}Handler>`) resueltos por DI.

**D. Reportes / stored procedures**: DTO plano + `SqlQuery<T>` parametrizado (`standards/02a-repository-unit-of-work.md`, sección SQL crudo) dentro de una clase `I{Report}Queries` en Infrastructure. Nunca concatenar SQL.

**E. Listado sin paginar** (combos o dropdowns): spec con `OrderBy` y sin `ApplyPaging`, más una proyección mínima `(Id, Name)`.

### Registrar la entidad
1. `Permissions.{Entities}` (Read/Write/Delete) en `Permissions.cs`. Si corresponde, añadir `{Entities}.Read` a `DefaultUserPermissions`.
2. `services.AddScoped<I{Entity}Service, {Entity}Service>();` en `AddApplication()`.
3. Los validadores se registran solos (escaneo de assembly).
4. Migración: `dotnet ef migrations add Add{Entity} -o Infrastructure/Persistence/Migrations`.

## Receta: agregar una entidad de negocio

Checklist que la IA sigue por **cada** entidad acordada con el usuario:

1. [ ] `Domain/Entities/{Entity}.cs`, heredando `BaseEntity` (`07` §Entidad).
2. [ ] `{Entity}Configuration` con longitudes, precisión, índices filtrados y FKs `Restrict` (`07` §Configuración EF).
3. [ ] `DbSet<{Entity}>` en `AppDbContext`.
4. [ ] `Features/{Entities}/{Entity}Contracts.cs`: DTO, Requests, Filter, `{Entity}Errors`, `{Entity}Mappings` (`07` §Contratos).
5. [ ] `{Entity}Specifications.cs`: por Id y por filtro con orden y desempate (`07` §Especificaciones).
6. [ ] `{Entity}Validators.cs` (`07` §Validadores).
7. [ ] `I{Entity}Service` / `{Entity}Service` (`07` §Service), más los patrones de este archivo si aplican.
8. [ ] `{Entities}Controller` (`07` §Controller); con seguridad, `[HasPermission]` en cada acción.
9. [ ] `Permissions.{Entities}` y, si corresponde, `DefaultUserPermissions` (solo perfil con seguridad).
10. [ ] Registro del service en `AddApplication()`.
11. [ ] Migración `Add{Entity}` y revisión del SQL generado.
12. [ ] Tests del service: crear, duplicado, no encontrado, concurrencia y borrado lógico.
