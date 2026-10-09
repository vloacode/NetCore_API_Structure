using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Query;
using KitApi.Application.Common.Paging;
using KitApi.Application.Common.Specifications;

namespace KitApi.Application.Abstractions.Persistence;

/// <summary>
/// Repositorio genérico. NUNCA llama SaveChanges: eso es responsabilidad de IUnitOfWork.
/// Las lecturas por especificación son AsNoTracking salvo que la spec pida tracking.
/// </summary>
public interface IRepository<T> where T : class
{
    // ---- Lectura de entidades ----
    /// <summary>Busca por PK con tracking (para luego modificar/eliminar).</summary>
    ValueTask<T?> GetByIdAsync(object id, CancellationToken ct = default);
    Task<T?> FirstOrDefaultAsync(ISpecification<T> spec, CancellationToken ct = default);
    Task<IReadOnlyList<T>> ListAsync(ISpecification<T> spec, CancellationToken ct = default);

    // ---- Lectura con proyección (preferida para devolver DTOs) ----
    Task<TResult?> FirstOrDefaultAsync<TResult>(ISpecification<T> spec, Expression<Func<T, TResult>> selector, CancellationToken ct = default);
    Task<IReadOnlyList<TResult>> ListAsync<TResult>(ISpecification<T> spec, Expression<Func<T, TResult>> selector, CancellationToken ct = default);

    /// <summary>Cuenta con el filtro de la spec (ignora paginación) y trae la página proyectada.</summary>
    Task<PagedResult<TResult>> PagedListAsync<TResult>(ISpecification<T> spec, Expression<Func<T, TResult>> selector,
        PaginationParams paging, CancellationToken ct = default);

    // ---- Agregados ----
    Task<int> CountAsync(ISpecification<T>? spec = null, CancellationToken ct = default);
    Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);

    // ---- Escritura (se persiste con IUnitOfWork.SaveChangesAsync) ----
    void Add(T entity);
    void AddRange(IEnumerable<T> entities);
    void Update(T entity);
    void Remove(T entity);
    void RemoveRange(IEnumerable<T> entities);

    // ---- Operaciones masivas (se ejecutan YA en BD; no pasan por el interceptor de auditoría) ----
    Task<int> ExecuteUpdateAsync(Expression<Func<T, bool>> predicate, Action<UpdateSettersBuilder<T>> setters, CancellationToken ct = default);
    Task<int> ExecuteDeleteAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);
}
