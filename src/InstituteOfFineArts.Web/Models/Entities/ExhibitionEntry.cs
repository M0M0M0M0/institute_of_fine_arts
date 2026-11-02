using InstituteOfFineArts.Web.Models.Enums;

namespace InstituteOfFineArts.Web.Models.Entities;

/// <summary>
/// Tranh gửi triển lãm và thông tin bán tranh (bảng exhibition_entry).
/// </summary>
/// <remarks>Trạng thái thanh toán (chưa bán / chưa trả / đã trả) được suy ra từ Status và PaidToStudent, không lưu.</remarks>
public class ExhibitionEntry : BaseEntity
{
    /// <summary>Khóa ngoại tới triển lãm.</summary>
    public long ExhibitionId { get; set; }

    /// <summary>Khóa ngoại tới bài dự thi (chỉ bài BEST/BETTER của cuộc thi đã công bố – BR-40).</summary>
    public long SubmissionId { get; set; }

    /// <summary>Giá bán do học viên quyết định (BR-41).</summary>
    public decimal AskingPrice { get; set; }

    /// <summary>Trạng thái.</summary>
    public EntryStatus Status { get; set; } = EntryStatus.PENDING;

    /// <summary>Lý do từ chối – bắt buộc khi REJECTED.</summary>
    public string? RejectReason { get; set; }

    /// <summary>Giá bán thực tế.</summary>
    public decimal? SoldPrice { get; set; }

    /// <summary>Ngày bán.</summary>
    public DateOnly? SoldDate { get; set; }

    /// <summary>Khóa ngoại tới khách mua.</summary>
    public long? CustomerId { get; set; }

    /// <summary>Đã trả tiền bán tranh cho học viên chưa (BR-45).</summary>
    public bool PaidToStudent { get; set; }

    /// <summary>Số tiền đã trả cho học viên.</summary>
    public decimal? PaidAmount { get; set; }

    /// <summary>Ngày trả tiền.</summary>
    public DateOnly? PaidDate { get; set; }

    /// <summary>Id tài khoản tạo entry (học viên đề xuất hoặc staff tạo trực tiếp).</summary>
    public long ProposedBy { get; set; }

    /// <summary>Id hồ sơ nhân sự đã duyệt / từ chối / ghi nhận bán.</summary>
    public long? ReviewedBy { get; set; }

    /// <summary>Ghi chú.</summary>
    public string? Notes { get; set; }

    /// <summary>Triển lãm.</summary>
    public Exhibition Exhibition { get; set; } = null!;

    /// <summary>Bài dự thi.</summary>
    public Submission Submission { get; set; } = null!;

    /// <summary>Khách mua.</summary>
    public Customer? Customer { get; set; }

    /// <summary>Tài khoản tạo entry.</summary>
    public Account ProposedByAccount { get; set; } = null!;

    /// <summary>Nhân sự duyệt.</summary>
    public StaffProfile? ReviewedByStaff { get; set; }
}
