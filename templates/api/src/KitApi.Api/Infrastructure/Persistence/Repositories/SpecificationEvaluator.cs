using Microsoft.EntityFrameworkCore;
using KitApi.Application.Common.Specifications;

namespace KitApi.Infrastructure.Persistence.Repositories;

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
