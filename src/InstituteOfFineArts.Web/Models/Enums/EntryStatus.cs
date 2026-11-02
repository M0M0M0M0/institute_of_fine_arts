namespace InstituteOfFineArts.Web.Models.Enums;

/// <summary>
/// Trạng thái tranh gửi triển lãm.
/// </summary>
/// <remarks>
/// Tên giá trị viết HOA để khớp đúng chuỗi lưu trong CSDL (cột VARCHAR + CHECK constraint)
/// và tên vai trò dùng trong [Authorize(Roles = "...")].
/// </remarks>
public enum EntryStatus
{
    /// <summary>Học viên đề xuất, chờ giáo viên duyệt</summary>
    PENDING,

    /// <summary>Đã được nhận vào triển lãm</summary>
    ACCEPTED,

    /// <summary>Bị từ chối (có lý do)</summary>
    REJECTED,

    /// <summary>Đã bán</summary>
    SOLD,

    /// <summary>Triển lãm kết thúc nhưng không bán được</summary>
    UNSOLD
}
