namespace InstituteOfFineArts.Web.Models.Entities;

/// <summary>
/// Giải đã trao cho một bài dự thi (bảng award_issued).
/// </summary>
public class AwardIssued : BaseEntity
{
    /// <summary>Khóa ngoại tới giải của cuộc thi.</summary>
    public long CompetitionAwardId { get; set; }

    /// <summary>Khóa ngoại tới bài đạt giải (duy nhất ⇒ tối đa 1 giải / học viên / cuộc thi).</summary>
    public long SubmissionId { get; set; }

    /// <summary>Id hồ sơ nhân sự trao giải.</summary>
    public long IssuedBy { get; set; }

    /// <summary>Ngày trao giải.</summary>
    public DateOnly IssuedDate { get; set; }

    /// <summary>Nhận xét khi trao giải / nhận xét về cuộc thi.</summary>
    public string? Remarks { get; set; }

    /// <summary>Giải thưởng.</summary>
    public CompetitionAward CompetitionAward { get; set; } = null!;

    /// <summary>Bài đạt giải.</summary>
    public Submission Submission { get; set; } = null!;

    /// <summary>Nhân sự trao giải.</summary>
    public StaffProfile IssuedByStaff { get; set; } = null!;
}
