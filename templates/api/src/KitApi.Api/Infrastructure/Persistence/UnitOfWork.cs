using Microsoft.EntityFrameworkCore;
using KitApi.Application.Abstractions.Persistence;
using KitApi.Application.Common.Results;
using KitApi.Infrastructure.Persistence.Repositories;

namespace KitApi.Infrastructure.Persistence;

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
