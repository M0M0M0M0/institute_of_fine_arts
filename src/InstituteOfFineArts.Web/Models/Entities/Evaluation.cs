using InstituteOfFineArts.Web.Models.Enums;

namespace InstituteOfFineArts.Web.Models.Entities;

/// <summary>
/// Kết quả chấm một bài dự thi (bảng evaluation).
/// </summary>
public class Evaluation : BaseEntity
{
    /// <summary>Khóa ngoại tới bài dự thi (duy nhất – mỗi bài tối đa 1 đánh giá).</summary>
    public long SubmissionId { get; set; }

    /// <summary>Id hồ sơ nhân sự chấm/sửa cuối cùng.</summary>
    public long EvaluatedBy { get; set; }

    /// <summary>Xếp loại.</summary>
    public Rating Rating { get; set; }

    /// <summary>Điểm mạnh – bắt buộc khi không bị loại (BR-23).</summary>
    public string? PositivePoints { get; set; }

    /// <summary>Điểm yếu – bắt buộc khi bị loại, dùng làm lý do (BR-23).</summary>
    public string? NegativePoints { get; set; }

    /// <summary>Điểm cần cải thiện.</summary>
    public string? ImprovementAreas { get; set; }

    /// <summary>Thời điểm chấm.</summary>
    public DateTime EvaluatedAt { get; set; }

    /// <summary>Bài dự thi.</summary>
    public Submission Submission { get; set; } = null!;

    /// <summary>Nhân sự chấm bài.</summary>
    public StaffProfile EvaluatedByStaff { get; set; } = null!;
}
