namespace Hmis.BuildingBlocks.Application.Pagination;

public sealed class PagedResponse<T>
{
    public IReadOnlyList<T> Items { get; init; } = [];
    public int PageNumber { get; init; }
    public int PageSize { get; init; }
    public long TotalCount { get; init; }
    public int TotalPages { get; init; }

    public static PagedResponse<T> Create(IReadOnlyList<T> items, long totalCount, PaginationRequest request) => new()
    {
        Items = items,
        PageNumber = request.PageNumber,
        PageSize = request.PageSize,
        TotalCount = totalCount,
        TotalPages = (int)Math.Ceiling(totalCount / (double)request.PageSize)
    };

    public PagedResponse<TOut> Map<TOut>(Func<T, TOut> selector) => new()
    {
        Items = Items.Select(selector).ToList(),
        PageNumber = PageNumber,
        PageSize = PageSize,
        TotalCount = TotalCount,
        TotalPages = TotalPages
    };
}
