namespace InstituteOfFineArts.Web.Models.Enums;

/// <summary>
/// Tình trạng học tập của học viên.
/// </summary>
/// <remarks>
/// Tên giá trị viết HOA để khớp đúng chuỗi lưu trong CSDL (cột VARCHAR + CHECK constraint)
/// và tên vai trò dùng trong [Authorize(Roles = "...")].
/// </remarks>
public enum EnrollmentStatus
{
    /// <summary>Đang học</summary>
    STUDYING,

    /// <summary>Đã tốt nghiệp</summary>
    GRADUATED,

    /// <summary>Bảo lưu / tạm dừng</summary>
    SUSPENDED,

    /// <summary>Đã nghỉ học</summary>
    DROPPED
}
