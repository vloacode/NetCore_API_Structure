using System.Linq.Expressions;

namespace KitApi.Application.Common.Specifications;

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
