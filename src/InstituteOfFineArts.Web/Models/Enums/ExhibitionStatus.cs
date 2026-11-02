namespace InstituteOfFineArts.Web.Models.Enums;

/// <summary>
/// Trạng thái triển lãm, tính tự động theo ngày (BR-42).
/// </summary>
/// <remarks>
/// Tên giá trị viết HOA để khớp đúng chuỗi lưu trong CSDL (cột VARCHAR + CHECK constraint)
/// và tên vai trò dùng trong [Authorize(Roles = "...")].
/// </remarks>
public enum ExhibitionStatus
{
    /// <summary>Sắp diễn ra</summary>
    UPCOMING,

    /// <summary>Đang diễn ra</summary>
    ONGOING,

    /// <summary>Đã kết thúc</summary>
    FINISHED
}
