using System.Linq.Expressions;

namespace SchoolManagementSystem.Application.Repository;

/// <summary>Direction-aware OrderBy / ThenBy for list endpoints that take <c>sort</c> and <c>dir</c>.</summary>
public static class QueryOrdering
{
    /// <summary>True only for "desc" (any case); anything else is ascending.</summary>
    public static bool IsDescending(string? dir) => string.Equals(dir?.Trim(), "desc", StringComparison.OrdinalIgnoreCase);

    public static IOrderedQueryable<T> OrderByDir<T, TKey>(this IQueryable<T> query, Expression<Func<T, TKey>> key, bool descending) =>
        descending ? query.OrderByDescending(key) : query.OrderBy(key);

    public static IOrderedQueryable<T> ThenByDir<T, TKey>(this IOrderedQueryable<T> query, Expression<Func<T, TKey>> key, bool descending) =>
        descending ? query.ThenByDescending(key) : query.ThenBy(key);
}
