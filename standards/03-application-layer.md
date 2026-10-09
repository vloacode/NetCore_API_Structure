# Capa Application: Result, paginación y especificaciones

> **Aplica a:** Todos los perfiles  
> **Propósito:** Result/Error, PaginationParams/PagedResult, PredicateBuilder y Specification<T>.  
> Índice general: `standards/00-INDEX.md`

## Application: Common

### `Result` y `Error` — `Application/Common/Results/Result.cs`
```csharp
namespace {Project}.Application.Common.Results;

public enum ErrorType
{
    Failure = 0,
    Validation = 1,
    NotFound = 2,
    Conflict = 3,
    Unauthorized = 4,
    Forbidden = 5
}

public sealed record Error(
    string Code,
    string Message,
    ErrorType Type = ErrorType.Failure,
    IReadOnlyDictionary<string, string[]>? Details = null)
{
    public static readonly Error None = new(string.Empty, string.Empty);

    public static Error Failure(string code, string message) => new(code, message, ErrorType.Failure);
    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);
    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);
    public static Error Unauthorized(string code, string message) => new(code, message, ErrorType.Unauthorized);
    public static Error Forbidden(string code, string message) => new(code, message, ErrorType.Forbidden);

    public static Error Validation(string code, string message, IReadOnlyDictionary<string, string[]>? details = null)
        => new(code, message, ErrorType.Validation, details);

    /// <summary>Convierte los errores de ASP.NET Identity en un Error de validación.</summary>
    public static Error FromIdentity(IEnumerable<Microsoft.AspNetCore.Identity.IdentityError> errors)
    {
        var details = errors
            .GroupBy(e => e.Code)
            .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray());
        return Validation("Identity.Validation", "Uno o más errores de validación.", details);
    }
}

public class Result
{
    protected Result(bool isSuccess, Error error)
    {
        if (isSuccess && error != Error.None) throw new InvalidOperationException("Un resultado exitoso no puede tener error.");
        if (!isSuccess && error == Error.None) throw new InvalidOperationException("Un resultado fallido debe tener error.");
        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public Error Error { get; }

    public static Result Success() => new(true, Error.None);
    public static Result Failure(Error error) => new(false, error);
    public static Result<T> Success<T>(T value) => new(value, true, Error.None);
    public static Result<T> Failure<T>(Error error) => new(default, false, error);

    public static implicit operator Result(Error error) => Failure(error);
}

public class Result<T> : Result
{
    private readonly T? _value;

    protected internal Result(T? value, bool isSuccess, Error error) : base(isSuccess, error) => _value = value;

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("No se puede leer Value de un resultado fallido.");

    public static implicit operator Result<T>(T value) => Success(value);
    public static implicit operator Result<T>(Error error) => Failure<T>(error);
}
```
Gracias a las conversiones implícitas, en un service se escribe `return dto;` o `return {Entity}Errors.NotFound(id);`.

### Paginación — `Application/Common/Paging/PagedResult.cs`
```csharp
namespace {Project}.Application.Common.Paging;

public class PaginationParams
{
    public const int MaxPageSize = 100;
    private int _pageNumber = 1;
    private int _pageSize = 20;

    public int PageNumber
    {
        get => _pageNumber;
        set => _pageNumber = value < 1 ? 1 : value;
    }

    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value < 1 ? 1 : Math.Min(value, MaxPageSize);
    }

    public int Skip => (PageNumber - 1) * PageSize;
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int PageNumber, int PageSize, int TotalCount)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPrevious => PageNumber > 1;
    public bool HasNext => PageNumber < TotalPages;
}
```

### `PredicateBuilder` — `Application/Common/Specifications/PredicateBuilder.cs`
```csharp
using System.Linq.Expressions;

namespace {Project}.Application.Common.Specifications;

/// <summary>Compone expresiones (And/Or/Not) para filtros dinámicos traducibles a SQL.</summary>
public static class PredicateBuilder
{
    public static Expression<Func<T, bool>> True<T>() => _ => true;
    public static Expression<Func<T, bool>> False<T>() => _ => false;

    public static Expression<Func<T, bool>> And<T>(this Expression<Func<T, bool>> first, Expression<Func<T, bool>> second)
        => first.Compose(second, Expression.AndAlso);

    public static Expression<Func<T, bool>> Or<T>(this Expression<Func<T, bool>> first, Expression<Func<T, bool>> second)
        => first.Compose(second, Expression.OrElse);

    public static Expression<Func<T, bool>> Not<T>(this Expression<Func<T, bool>> expression)
        => Expression.Lambda<Func<T, bool>>(Expression.Not(expression.Body), expression.Parameters);

    /// <summary>Agrega la condición solo si <paramref name="condition"/> es true.</summary>
    public static Expression<Func<T, bool>> AndIf<T>(this Expression<Func<T, bool>> first, bool condition, Expression<Func<T, bool>> second)
        => condition ? first.And(second) : first;

    private static Expression<TDelegate> Compose<TDelegate>(this Expression<TDelegate> first, Expression<TDelegate> second,
        Func<Expression, Expression, Expression> merge) where TDelegate : Delegate
    {
        var map = first.Parameters
            .Select((f, i) => new { f, s = second.Parameters[i] })
            .ToDictionary(p => p.s, p => p.f);

        var secondBody = new ParameterRebinder(map).Visit(second.Body);
        return Expression.Lambda<TDelegate>(merge(first.Body, secondBody), first.Parameters);
    }

    private sealed class ParameterRebinder(Dictionary<ParameterExpression, ParameterExpression> map) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node)
            => base.VisitParameter(map.TryGetValue(node, out var replacement) ? replacement : node);
    }
}
```

### Specification — `Application/Common/Specifications/Specification.cs`
```csharp
using System.Linq.Expressions;
using {Project}.Application.Common.Paging;

namespace {Project}.Application.Common.Specifications;

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
/// Crear una clase por consulta con nombre de negocio (ej. {Entities}ByFilterSpec).
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

    /// <summary>Para ThenInclude anidados: "{Parent}.OtraNavegacion".</summary>
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
```
