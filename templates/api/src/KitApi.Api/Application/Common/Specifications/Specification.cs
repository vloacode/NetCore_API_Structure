using System.Linq.Expressions;
using KitApi.Application.Common.Paging;

namespace KitApi.Application.Common.Specifications;

public interface ISpecification<T>
{
    Expression<Func<T, bool>>? Criteria { get; }
    IReadOnlyList<Expression<Func<T, object>>> Includes { get; }
    IReadOnlyList<string> IncludeStrings { get; }
    IReadOnlyList<(Expression<Func<T, object>> KeySelector, bool Descending)> OrderExpressions { get; }
    int? Skip { get; }
    int? Take { get; }
    bool AsNoTracking { get; }
    bool AsSplitQuery { get; }
    bool IgnoreQueryFilters { get; }
}

/// <summary>
/// Encapsula una consulta reutilizable: filtro + includes + orden + paginación.
/// Crear una clase por consulta con nombre de negocio (ej. InvoicesByFilterSpec).
/// </summary>
public abstract class Specification<T> : ISpecification<T>
{
    private readonly List<Expression<Func<T, object>>> _includes = [];
    private readonly List<string> _includeStrings = [];
    private readonly List<(Expression<Func<T, object>>, bool)> _orderExpressions = [];

    protected Specification() { }
    protected Specification(Expression<Func<T, bool>> criteria) => Criteria = criteria;

    public Expression<Func<T, bool>>? Criteria { get; private set; }
    public IReadOnlyList<Expression<Func<T, object>>> Includes => _includes;
    public IReadOnlyList<string> IncludeStrings => _includeStrings;
    public IReadOnlyList<(Expression<Func<T, object>> KeySelector, bool Descending)> OrderExpressions => _orderExpressions;
    public int? Skip { get; private set; }
    public int? Take { get; private set; }
    public bool AsNoTracking { get; private set; } = true;
    public bool AsSplitQuery { get; private set; }
    public bool IgnoreQueryFilters { get; private set; }

    protected void Where(Expression<Func<T, bool>> criteria)
        => Criteria = Criteria is null ? criteria : Criteria.And(criteria);

    protected void Include(Expression<Func<T, object>> include) => _includes.Add(include);

    /// <summary>Para ThenInclude anidados: "Customer.Address".</summary>
    protected void Include(string navigationPath) => _includeStrings.Add(navigationPath);

    protected void OrderBy(Expression<Func<T, object>> key) => _orderExpressions.Add((key, false));
    protected void OrderByDescending(Expression<Func<T, object>> key) => _orderExpressions.Add((key, true));

    protected void ApplyPaging(PaginationParams paging)
    {
        Skip = paging.Skip;
        Take = paging.PageSize;
    }

    protected void EnableTracking() => AsNoTracking = false;
    protected void SplitQuery() => AsSplitQuery = true;   // usar con 2+ Include de colecciones
    protected void IncludeSoftDeleted() => IgnoreQueryFilters = true;
}
