# Repositorio, especificaciones y Unit of Work

> **Aplica a:** Todos los perfiles  
> **Propósito:** Implementación de SpecificationEvaluator, Repository<T>, UnitOfWork (transacciones) y SQL crudo parametrizado.  
> Índice general: `standards/00-INDEX.md`

### Evaluador de especificaciones — `Infrastructure/Persistence/Repositories/SpecificationEvaluator.cs`
```csharp
using Microsoft.EntityFrameworkCore;
using {Project}.Application.Common.Specifications;

namespace {Project}.Infrastructure.Persistence.Repositories;

/// <summary>Traduce una ISpecification a IQueryable en el orden correcto: Where → Include → OrderBy → Skip → Take.</summary>
internal static class SpecificationEvaluator
{
    public static IQueryable<T> GetQuery<T>(IQueryable<T> source, ISpecification<T> spec, bool applyPaging = true) where T : class
    {
        var query = source;

        if (spec.IgnoreQueryFilters) query = query.IgnoreQueryFilters();
        if (spec.AsNoTracking) query = query.AsNoTracking();
        if (spec.Criteria is not null) query = query.Where(spec.Criteria);

        query = spec.Includes.Aggregate(query, (current, include) => current.Include(include));
        query = spec.IncludeStrings.Aggregate(query, (current, include) => current.Include(include));

        if (spec.AsSplitQuery) query = query.AsSplitQuery();

        if (spec.OrderExpressions.Count > 0)
        {
            var (firstKey, firstDesc) = spec.OrderExpressions[0];
            var ordered = firstDesc ? query.OrderByDescending(firstKey) : query.OrderBy(firstKey);

            foreach (var (key, desc) in spec.OrderExpressions.Skip(1))
                ordered = desc ? ordered.ThenByDescending(key) : ordered.ThenBy(key);

            query = ordered;
        }

        if (applyPaging && (spec.Skip.HasValue || spec.Take.HasValue))
        {
            if (spec.OrderExpressions.Count == 0)
                throw new InvalidOperationException($"La especificación {spec.GetType().Name} pagina sin OrderBy; el orden no sería determinista.");

            if (spec.Skip.HasValue) query = query.Skip(spec.Skip.Value);
            if (spec.Take.HasValue) query = query.Take(spec.Take.Value);
        }

        return query;
    }
}
```

### Repositorio genérico — `Infrastructure/Persistence/Repositories/Repository.cs`
```csharp
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using {Project}.Application.Abstractions.Persistence;
using {Project}.Application.Common.Paging;
using {Project}.Application.Common.Specifications;

namespace {Project}.Infrastructure.Persistence.Repositories;

public class Repository<T>(AppDbContext context) : IRepository<T> where T : class
{
    protected DbSet<T> Set { get; } = context.Set<T>();

    public ValueTask<T?> GetByIdAsync(object id, CancellationToken ct = default)
        => Set.FindAsync([id], ct);

    public Task<T?> FirstOrDefaultAsync(ISpecification<T> spec, CancellationToken ct = default)
        => Apply(spec).FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<T>> ListAsync(ISpecification<T> spec, CancellationToken ct = default)
        => await Apply(spec).ToListAsync(ct);

    public Task<TResult?> FirstOrDefaultAsync<TResult>(ISpecification<T> spec, Expression<Func<T, TResult>> selector, CancellationToken ct = default)
        => Apply(spec).Select(selector).FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<TResult>> ListAsync<TResult>(ISpecification<T> spec, Expression<Func<T, TResult>> selector, CancellationToken ct = default)
        => await Apply(spec).Select(selector).ToListAsync(ct);

    public async Task<PagedResult<TResult>> PagedListAsync<TResult>(ISpecification<T> spec, Expression<Func<T, TResult>> selector,
        PaginationParams paging, CancellationToken ct = default)
    {
        var total = await Apply(spec, applyPaging: false).CountAsync(ct);
        var items = await Apply(spec).Select(selector).ToListAsync(ct);
        return new PagedResult<TResult>(items, paging.PageNumber, paging.PageSize, total);
    }

    public Task<int> CountAsync(ISpecification<T>? spec = null, CancellationToken ct = default)
        => spec is null ? Set.CountAsync(ct) : Apply(spec, applyPaging: false).CountAsync(ct);

    public Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
        => Set.AnyAsync(predicate, ct);

    public void Add(T entity) => Set.Add(entity);
    public void AddRange(IEnumerable<T> entities) => Set.AddRange(entities);
    public void Update(T entity) => Set.Update(entity);   // solo para entidades desconectadas
    public void Remove(T entity) => Set.Remove(entity);
    public void RemoveRange(IEnumerable<T> entities) => Set.RemoveRange(entities);

    public Task<int> ExecuteUpdateAsync(Expression<Func<T, bool>> predicate, Action<UpdateSettersBuilder<T>> setters, CancellationToken ct = default)
        => Set.Where(predicate).ExecuteUpdateAsync(setters, ct);

    public Task<int> ExecuteDeleteAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
        => Set.Where(predicate).ExecuteDeleteAsync(ct);

    private IQueryable<T> Apply(ISpecification<T> spec, bool applyPaging = true)
        => SpecificationEvaluator.GetQuery(Set.AsQueryable(), spec, applyPaging);
}
```

> `ExecuteDeleteAsync` hace un **borrado físico** que salta el soft delete. Usarlo solo en tablas técnicas (por ejemplo, limpieza de `RefreshTokens` expirados).

### Unit of Work — `Infrastructure/Persistence/UnitOfWork.cs`
```csharp
using Microsoft.EntityFrameworkCore;
using {Project}.Application.Abstractions.Persistence;
using {Project}.Application.Common.Results;
using {Project}.Infrastructure.Persistence.Repositories;

namespace {Project}.Infrastructure.Persistence;

/// <summary>
/// Scoped. NO implementa IDisposable: el DbContext pertenece al contenedor DI.
/// </summary>
public sealed class UnitOfWork(AppDbContext context) : IUnitOfWork
{
    private readonly Dictionary<Type, object> _repositories = [];

    public IRepository<T> Repository<T>() where T : class
    {
        if (!_repositories.TryGetValue(typeof(T), out var repository))
        {
            repository = new Repository<T>(context);
            _repositories[typeof(T)] = repository;
        }
        return (IRepository<T>)repository;
    }

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => context.SaveChangesAsync(ct);

    public async Task<Result<T>> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<Result<T>>> operation, CancellationToken ct = default)
    {
        // La estrategia de ejecución (EnableRetryOnFailure) exige envolver transacciones explícitas.
        var strategy = context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            // Si ya hay una transacción abierta (llamada anidada), reutilizarla.
            if (context.Database.CurrentTransaction is not null)
                return await operation(ct);

            await using var transaction = await context.Database.BeginTransactionAsync(ct);

            var result = await operation(ct);
            if (result.IsFailure)
            {
                await transaction.RollbackAsync(ct);
                context.ChangeTracker.Clear();   // descartar cambios pendientes del intento fallido
                return result;
            }

            await context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return result;
            // Si hay excepción, el using hace Dispose sin Commit = Rollback.
        });
    }

    public async Task<Result> ExecuteInTransactionAsync(Func<CancellationToken, Task<Result>> operation, CancellationToken ct = default)
    {
        var result = await ExecuteInTransactionAsync<bool>(async token =>
        {
            var inner = await operation(token);
            return inner.IsSuccess ? Result.Success(true) : Result.Failure<bool>(inner.Error);
        }, ct);

        return result.IsSuccess ? Result.Success() : Result.Failure(result.Error);
    }
}
```

**Por qué funciona:** `IUnitOfWork` es *Scoped*. En un request, todos los services reciben la misma instancia y, por tanto, el **mismo `AppDbContext`**. `UserManager`/`RoleManager` de Identity también usan ese contexto, así que una transacción abierta con `ExecuteInTransactionAsync` cubre también las operaciones de Identity.

### Stored procedures y SQL crudo (siempre parametrizado)
```csharp
// Resultado sin entidad (EF Core 8+): tipo DTO plano.
var rows = await context.Database
    .SqlQuery<{Report}Row>($"EXEC dbo.{StoredProcedure} @From = {from}, @To = {to}")       // [MSSQL] procedimiento
    .SqlQuery<{Report}Row>($"SELECT * FROM {report_function}({from}, {to})")              // [PGSQL] función
    .ToListAsync(ct);
```
Los valores interpolados se convierten en parámetros SQL. **Nunca** armar el SQL con `+` ni con `string.Format`. Si un service necesita esto, se expone mediante un método en un repositorio específico (`I{Report}Queries`) implementado en Infrastructure.
