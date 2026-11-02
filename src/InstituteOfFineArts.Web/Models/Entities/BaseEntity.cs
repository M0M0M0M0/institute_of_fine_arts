namespace InstituteOfFineArts.Web.Models.Entities;

/// <summary>
/// Lớp cha của mọi entity: khóa chính và 2 cột audit.
/// CreatedAt/UpdatedAt được gán tự động trong AppDbContext.SaveChangesAsync.
/// </summary>
public abstract class BaseEntity
{
    /// <summary>Khóa chính (BIGINT IDENTITY).</summary>
    public long Id { get; set; }

    /// <summary>Thời điểm tạo bản ghi (giờ Việt Nam).</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Thời điểm sửa gần nhất; null nếu chưa sửa lần nào.</summary>
    public DateTime? UpdatedAt { get; set; }
}
