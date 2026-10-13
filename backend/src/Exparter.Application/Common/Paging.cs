namespace Exparter.Application.Common;

/// <summary>A page-based slice of a list, as described in docs/api-conventions.md.</summary>
public sealed record PagedList<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public PagedList<TOther> Map<TOther>(Func<T, TOther> map) =>
        new(Items.Select(map).ToList(), Page, PageSize, TotalCount);
}

public sealed record PageRequest(int Page, int PageSize)
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 50;

    public int Skip => (Page - 1) * PageSize;

    /// <summary>Applies the defaults for omitted values. Out-of-range values are rejected.</summary>
    public static PageRequest From(int? page, int? pageSize)
    {
        var request = new PageRequest(page ?? 1, pageSize ?? DefaultPageSize);
        if (request.Page < 1 || request.PageSize is < 1 or > MaxPageSize)
        {
            throw new FluentValidation.ValidationException(
            [
                new FluentValidation.Results.ValidationFailure(
                    request.Page < 1 ? "page" : "pageSize",
                    $"page must be at least 1 and pageSize between 1 and {MaxPageSize}."),
            ]);
        }

        return request;
    }
}
