namespace CaseManagement.Application.DTOs;

public sealed record PagedResult<T>
{
    public required IReadOnlyList<T> Items { get; init; }

    public required int Page { get; init; }

    public required int PageSize { get; init; }

    /// <summary>Rows matching the filters across all pages, not the length of <see cref="Items"/>.</summary>
    public required int TotalCount { get; init; }

    public int TotalPages => PageSize <= 0
        ? 0
        : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public bool HasPreviousPage => Page > 1;

    public bool HasNextPage => Page < TotalPages;
}
