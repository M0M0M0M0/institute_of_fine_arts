using InstituteOfFineArts.Web.Models.Enums;

namespace InstituteOfFineArts.Web.Models.Entities;

/// <summary>
/// Triển lãm tranh trong nước (bảng exhibition).
/// </summary>
public class Exhibition : BaseEntity
{
    /// <summary>Tên triển lãm.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Mô tả.</summary>
    public string? Description { get; set; }

    /// <summary>Địa điểm.</summary>
    public string Venue { get; set; } = string.Empty;

    /// <summary>Tỉnh / thành phố.</summary>
    public string City { get; set; } = string.Empty;

    /// <summary>Đơn vị tổ chức.</summary>
    public string? Organizer { get; set; }

    /// <summary>Ngày bắt đầu.</summary>
    public DateOnly StartDate { get; set; }

    /// <summary>Ngày kết thúc.</summary>
    public DateOnly EndDate { get; set; }

    /// <summary>Trạng thái (tính theo ngày – BR-42).</summary>
    public ExhibitionStatus Status { get; set; } = ExhibitionStatus.UPCOMING;

    /// <summary>Id hồ sơ nhân sự tạo triển lãm (Staff hoặc Manager).</summary>
    public long CreatedBy { get; set; }

    /// <summary>Nhân sự tạo triển lãm.</summary>
    public StaffProfile CreatedByStaff { get; set; } = null!;

    /// <summary>Các tranh gửi tới triển lãm.</summary>
    public ICollection<ExhibitionEntry> Entries { get; set; } = new List<ExhibitionEntry>();
}
