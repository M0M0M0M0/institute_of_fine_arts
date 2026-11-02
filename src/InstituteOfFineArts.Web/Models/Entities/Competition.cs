using InstituteOfFineArts.Web.Models.Enums;

namespace InstituteOfFineArts.Web.Models.Entities;

/// <summary>
/// Cuộc thi vẽ tranh (bảng competition).
/// </summary>
public class Competition : BaseEntity
{
    /// <summary>Mã cuộc thi, duy nhất. Ví dụ: CT2026-01.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Tên cuộc thi.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Mô tả chi tiết.</summary>
    public string? Description { get; set; }

    /// <summary>Thể lệ / điều kiện.</summary>
    public string? Rules { get; set; }

    /// <summary>Ngày bắt đầu nhận bài (từ 00:00).</summary>
    public DateOnly StartDate { get; set; }

    /// <summary>Ngày kết thúc – hạn nộp là 23:59:59 ngày này (BR-06).</summary>
    public DateOnly EndDate { get; set; }

    /// <summary>Trạng thái (BR-07, BR-08).</summary>
    public CompetitionStatus Status { get; set; } = CompetitionStatus.UPCOMING;

    /// <summary>Thời điểm công bố kết quả.</summary>
    public DateTime? PublishedAt { get; set; }

    /// <summary>Id hồ sơ nhân sự đã công bố kết quả.</summary>
    public long? PublishedBy { get; set; }

    /// <summary>Đường dẫn tương đối ảnh bìa (lưu bằng File I/O).</summary>
    public string? CoverImagePath { get; set; }

    /// <summary>Nhận xét chung về cuộc thi.</summary>
    public string? ResultRemarks { get; set; }

    /// <summary>Id hồ sơ nhân sự tạo cuộc thi.</summary>
    public long CreatedBy { get; set; }

    /// <summary>Nhân sự tạo cuộc thi.</summary>
    public StaffProfile CreatedByStaff { get; set; } = null!;

    /// <summary>Nhân sự công bố kết quả.</summary>
    public StaffProfile? PublishedByStaff { get; set; }

    /// <summary>Các giải thưởng dự kiến của cuộc thi.</summary>
    public ICollection<CompetitionAward> Awards { get; set; } = new List<CompetitionAward>();

    /// <summary>Các bài dự thi.</summary>
    public ICollection<Submission> Submissions { get; set; } = new List<Submission>();
}
