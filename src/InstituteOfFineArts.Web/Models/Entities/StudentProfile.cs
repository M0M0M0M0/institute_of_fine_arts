using InstituteOfFineArts.Web.Models.Enums;

namespace InstituteOfFineArts.Web.Models.Entities;

/// <summary>
/// Thông tin nhập học của học viên (bảng student_profile).
/// </summary>
public class StudentProfile : BaseEntity
{
    /// <summary>Khóa ngoại tới tài khoản (quan hệ 1–1).</summary>
    public long AccountId { get; set; }

    /// <summary>Mã học viên, duy nhất. Ví dụ: HV0001.</summary>
    public string StudentCode { get; set; } = string.Empty;

    /// <summary>Ngày nhập học.</summary>
    public DateOnly EnrollmentDate { get; set; }

    /// <summary>Khóa học: Hội họa, Hoạt hình…</summary>
    public string CourseName { get; set; } = string.Empty;

    /// <summary>Lớp.</summary>
    public string? ClassName { get; set; }

    /// <summary>Tình trạng học tập.</summary>
    public EnrollmentStatus EnrollmentStatus { get; set; } = EnrollmentStatus.STUDYING;

    /// <summary>Ghi chú.</summary>
    public string? Notes { get; set; }

    /// <summary>Tài khoản đăng nhập.</summary>
    public Account Account { get; set; } = null!;

    /// <summary>Các bài dự thi của học viên.</summary>
    public ICollection<Submission> Submissions { get; set; } = new List<Submission>();
}
