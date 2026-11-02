namespace InstituteOfFineArts.Web.Models.Enums;

/// <summary>
/// Loại phần chữ đi kèm bức tranh (BR-19).
/// </summary>
/// <remarks>
/// Tên giá trị viết HOA để khớp đúng chuỗi lưu trong CSDL (cột VARCHAR + CHECK constraint)
/// và tên vai trò dùng trong [Authorize(Roles = "...")].
/// </remarks>
public enum TextType
{
    /// <summary>Thơ</summary>
    POEM,

    /// <summary>Trích dẫn</summary>
    QUOTATION,

    /// <summary>Câu chuyện</summary>
    STORY,

    /// <summary>Khác</summary>
    OTHER
}
