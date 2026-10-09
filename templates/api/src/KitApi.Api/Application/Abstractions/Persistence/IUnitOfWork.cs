using KitApi.Application.Common.Results;

namespace KitApi.Application.Abstractions.Persistence;

/// <summary>
/// Unit of Work: un DbContext compartido por request (Scoped).
/// Todos los repositorios obtenidos aquí comparten contexto y transacción.
/// </summary>
public interface IUnitOfWork
{
    IRepository<T> Repository<T>() where T : class;

    Task<int> SaveChangesAsync(CancellationToken ct = default);

    /// <summary>
    /// Ejecuta varias operaciones en UNA transacción de BD.
    /// Hace SaveChanges + Commit solo si el Result es exitoso; si falla o lanza excepción, Rollback.
    /// La operación puede re-ejecutarse si la estrategia de reintentos lo requiere: no tener efectos externos (emails) dentro.
    /// </summary>
    Task<Result<T>> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<Result<T>>> operation, CancellationToken ct = default);

    Task<Result> ExecuteInTransactionAsync(Func<CancellationToken, Task<Result>> operation, CancellationToken ct = default);
}
