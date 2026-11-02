namespace InstituteOfFineArts.Web.Models.Enums;

/// <summary>
/// Trạng thái cuộc thi. UPCOMING/ONGOING/CLOSED tính theo ngày (BR-07); GRADED chỉ khi Staff công bố kết quả (BR-08).
/// </summary>
/// <remarks>
/// Tên giá trị viết HOA để khớp đúng chuỗi lưu trong CSDL (cột VARCHAR + CHECK constraint)
/// và tên vai trò dùng trong [Authorize(Roles = "...")].
/// </remarks>
public enum CompetitionStatus
{
    /// <summary>Sắp diễn ra (trước ngày bắt đầu)</summary>
    UPCOMING,

    /// <summary>Đang diễn ra – học viên được nộp/sửa/xóa bài</summary>
    ONGOING,

    /// <summary>Đã hết hạn nộp, đang chấm</summary>
    CLOSED,

    /// <summary>Đã công bố kết quả</summary>
    GRADED
}
