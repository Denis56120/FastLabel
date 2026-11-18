namespace FastLabel.Backend.Helpers;

public static class QueryableHelpers
{
    public static async Task<PagedResultDto<TResult>> ToPagedResultAsync<TEntity, TResult>(
        this IQueryable<TEntity> query,
        int page,
        int pageSize,
        string? sortBy,
        bool? sortAscending,
        Func<TEntity, TResult> selector,
        Func<IEnumerable<TResult>, string?, bool, IEnumerable<TResult>>? customNaturalSort = null)
    {
        var rawEntities = await query.ToListAsync();
        var totalCount = rawEntities.Count;

        var mappedItems = rawEntities.Select(selector).ToList();

        bool ascending = sortAscending ?? true;
        
        if (customNaturalSort != null)
        {
            mappedItems = customNaturalSort(mappedItems, sortBy, ascending).ToList();
        }

        var items = mappedItems
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new PagedResultDto<TResult>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }
}