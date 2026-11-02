using Microsoft.EntityFrameworkCore;

namespace InstituteOfFineArts.Web.ViewModels.Common;

/// <summary>
/// Kết quả phân trang dùng chung cho mọi trang danh sách.
/// View dùng partial _Pagination để vẽ thanh chuyển trang từ đối tượng này.
/// </summary>
/// <typeparam name="T">Kiểu của từng dòng dữ liệu</typeparam>
public class PagedResult<T>
{
    /// <summary>Các dòng của trang hiện tại.</summary>
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();

    /// <summary>Trang hiện tại (bắt đầu từ 1).</summary>
    public int Page { get; init; }

    /// <summary>Số dòng mỗi trang.</summary>
    public int PageSize { get; init; }

    /// <summary>Tổng số dòng thỏa điều kiện lọc.</summary>
    public int TotalCount { get; init; }

    /// <summary>Tổng số trang.</summary>
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    /// <summary>Có trang trước không.</summary>
    public bool HasPrevious => Page > 1;

    /// <summary>Có trang sau không.</summary>
    public bool HasNext => Page < TotalPages;

    /// <summary>
    /// Tạo kết quả phân trang từ một truy vấn EF Core: đếm tổng rồi lấy đúng trang cần xem.
    /// </summary>
    /// <param name="query">Truy vấn đã lọc và sắp xếp</param>
    /// <param name="page">Trang cần lấy (nhỏ hơn 1 sẽ coi là 1)</param>
    /// <param name="pageSize">Số dòng mỗi trang (mặc định 10)</param>
    public static async Task<PagedResult<T>> CreateAsync(IQueryable<T> query, int page, int pageSize = 10)
    {
        // Chuẩn hóa tham số đầu vào
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        // Đếm tổng số dòng, sau đó lấy dữ liệu của trang hiện tại
        var total = await query.CountAsync();
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        return new PagedResult<T> { Items = items, Page = page, PageSize = pageSize, TotalCount = total };
    }
}
