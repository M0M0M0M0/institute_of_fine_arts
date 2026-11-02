namespace InstituteOfFineArts.Web.Models.Enums;

/// <summary>
/// Vai trò của tài khoản (mỗi tài khoản đúng 1 vai trò – BR-03).
/// </summary>
/// <remarks>
/// Tên giá trị viết HOA để khớp đúng chuỗi lưu trong CSDL (cột VARCHAR + CHECK constraint)
/// và tên vai trò dùng trong [Authorize(Roles = "...")].
/// </remarks>
public enum Role
{
    /// <summary>Quản trị viên: quản lý tài khoản nhân viên, học viên</summary>
    ADMIN,

    /// <summary>Quản lý: xem toàn bộ dữ liệu, quản lý triển lãm</summary>
    MANAGER,

    /// <summary>Giáo viên/nhân viên: cuộc thi, chấm bài, trao giải, triển lãm</summary>
    STAFF,

    /// <summary>Học viên: nộp bài, xem kết quả</summary>
    STUDENT
}
