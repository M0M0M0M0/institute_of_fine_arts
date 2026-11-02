using Microsoft.EntityFrameworkCore;

namespace InstituteOfFineArts.Web.ViewModels.Common;

/// <summary>
/// Shared paging result for every list page.
/// Views use the _Pagination partial to draw the pager from this object.
/// </summary>
/// <typeparam name="T">Type of each row</typeparam>
public class PagedResult<T>
{
    /// <summary>Rows of the current page.</summary>
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();

    /// <summary>Current page (starting at 1).</summary>
    public int Page { get; init; }

    /// <summary>Rows per page.</summary>
    public int PageSize { get; init; }

    /// <summary>Total number of rows matching the filter.</summary>
    public int TotalCount { get; init; }

    /// <summary>Total number of pages.</summary>
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    /// <summary>Whether there is a previous page.</summary>
    public bool HasPrevious => Page > 1;

    /// <summary>Whether there is a next page.</summary>
    public bool HasNext => Page < TotalPages;

    /// <summary>
    /// Builds a paged result from an EF Core query: counts the total, then loads only the requested page.
    /// </summary>
    /// <param name="query">Filtered and ordered query</param>
    /// <param name="page">Requested page (values below 1 are treated as 1)</param>
    /// <param name="pageSize">Rows per page (default 10)</param>
    public static async Task<PagedResult<T>> CreateAsync(IQueryable<T> query, int page, int pageSize = 10)
    {
        // Normalize the input parameters
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        // Count all rows, then load the rows of the current page
        var total = await query.CountAsync();
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        return new PagedResult<T> { Items = items, Page = page, PageSize = pageSize, TotalCount = total };
    }
}
