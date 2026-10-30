namespace Hmis.BuildingBlocks.Application.Pagination;

public class PaginationRequest
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = DefaultPageSize;
    public string? Search { get; set; }
    public string? SortBy { get; set; }
    public SortDirection SortDirection { get; set; } = SortDirection.Asc;

    public int Skip => (PageNumber - 1) * PageSize;
}

public enum SortDirection
{
    Asc,
    Desc
}
