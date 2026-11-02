namespace InstituteOfFineArts.Web.Models.Enums;

/// <summary>
/// Giới tính (lưu 1 ký tự trong CSDL).
/// </summary>
/// <remarks>
/// Tên giá trị viết HOA để khớp đúng chuỗi lưu trong CSDL (cột VARCHAR + CHECK constraint)
/// và tên vai trò dùng trong [Authorize(Roles = "...")].
/// </remarks>
public enum Gender
{
    /// <summary>Nam</summary>
    M,

    /// <summary>Nữ</summary>
    F,

    /// <summary>Khác</summary>
    O
}
