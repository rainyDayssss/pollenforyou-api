namespace PollenForYouApi.DTOs;

/// <summary>
/// Standardized pagination envelope used by every collection endpoint (SRS §2.4).
/// </summary>
public record PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = [];

    public int Page { get; init; }

    public int PageSize { get; init; }

    public int TotalItems { get; init; }

    public int TotalPages { get; init; }

    public bool HasNextPage { get; init; }

    public bool HasPreviousPage { get; init; }
}
