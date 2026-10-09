using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using KitApi.Application.Abstractions.Persistence;
using KitApi.Application.Common.Paging;
using KitApi.Application.Common.Specifications;

namespace KitApi.Infrastructure.Persistence.Repositories;

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
