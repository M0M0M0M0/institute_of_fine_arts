namespace InstituteOfFineArts.Web.Models.Enums;

/// <summary>
/// Xếp loại bài dự thi do giáo viên chấm.
/// </summary>
/// <remarks>
/// Tên giá trị viết HOA để khớp đúng chuỗi lưu trong CSDL (cột VARCHAR + CHECK constraint)
/// và tên vai trò dùng trong [Authorize(Roles = "...")].
/// </remarks>
public enum Rating
{
    /// <summary>Xuất sắc nhất</summary>
    BEST,

    /// <summary>Rất tốt</summary>
    BETTER,

    /// <summary>Tốt</summary>
    GOOD,

    /// <summary>Trung bình</summary>
    MODERATE,

    /// <summary>Bình thường</summary>
    NORMAL,

    /// <summary>Bị loại (vi phạm thể lệ hoặc nộp sau hạn)</summary>
    DISQUALIFIED
}
