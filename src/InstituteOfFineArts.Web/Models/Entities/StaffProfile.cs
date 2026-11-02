namespace InstituteOfFineArts.Web.Models.Entities;

/// <summary>
/// Thông tin công việc của nhân sự: ADMIN, MANAGER, STAFF (bảng staff_profile).
/// </summary>
public class StaffProfile : BaseEntity
{
    /// <summary>Khóa ngoại tới tài khoản (quan hệ 1–1).</summary>
    public long AccountId { get; set; }

    /// <summary>Mã nhân viên, duy nhất. Ví dụ: NV0001.</summary>
    public string StaffCode { get; set; } = string.Empty;

    /// <summary>Chức vụ: Giảng viên, Trưởng bộ môn…</summary>
    public string? Position { get; set; }

    /// <summary>Ngày vào làm.</summary>
    public DateOnly JoinedDate { get; set; }

    /// <summary>Các lớp phụ trách (chuỗi, ví dụ "HH01, HH02").</summary>
    public string? ClassesInCharge { get; set; }

    /// <summary>Các môn phụ trách.</summary>
    public string? SubjectsInCharge { get; set; }

    /// <summary>Trình độ.</summary>
    public string? Qualification { get; set; }

    /// <summary>Ghi chú (đề bài yêu cầu có remarks).</summary>
    public string? Notes { get; set; }

    /// <summary>Tài khoản đăng nhập.</summary>
    public Account Account { get; set; } = null!;
}
