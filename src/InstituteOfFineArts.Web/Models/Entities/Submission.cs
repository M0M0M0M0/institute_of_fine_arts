using InstituteOfFineArts.Web.Models.Enums;

namespace InstituteOfFineArts.Web.Models.Entities;

/// <summary>
/// Bài dự thi của học viên: ảnh JPEG + phần chữ đi kèm (bảng submission).
/// </summary>
/// <remarks>Không có cột status: trạng thái hiển thị (đã nộp, chờ chấm, đã chấm, đạt giải, bị loại) được suy ra từ cuộc thi, đánh giá và giải thưởng.</remarks>
public class Submission : BaseEntity
{
    /// <summary>Khóa ngoại tới cuộc thi.</summary>
    public long CompetitionId { get; set; }

    /// <summary>Khóa ngoại tới hồ sơ học viên.</summary>
    public long StudentId { get; set; }

    /// <summary>Tên bức tranh (≤ 150 ký tự).</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Đường dẫn tương đối của file JPEG trong thư mục uploads.</summary>
    public string ImagePath { get; set; } = string.Empty;

    /// <summary>Đường dẫn tương đối của ảnh thu nhỏ.</summary>
    public string? ThumbnailPath { get; set; }

    /// <summary>Tên file gốc – chỉ để hiển thị/tải xuống, không dùng làm đường dẫn.</summary>
    public string OriginalFileName { get; set; } = string.Empty;

    /// <summary>Dung lượng file (byte).</summary>
    public long FileSize { get; set; }

    /// <summary>Loại phần chữ đi kèm.</summary>
    public TextType TextType { get; set; }

    /// <summary>Nội dung thơ / trích dẫn / câu chuyện (≤ 4000 ký tự).</summary>
    public string TextContent { get; set; } = string.Empty;

    /// <summary>Mô tả: vì sao sáng tác, vì sao xứng đáng nhất… (≤ 2000 ký tự).</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Thời điểm nộp bài.</summary>
    public DateTime SubmittedAt { get; set; }

    /// <summary>Cuộc thi.</summary>
    public Competition Competition { get; set; } = null!;

    /// <summary>Học viên.</summary>
    public StudentProfile Student { get; set; } = null!;

    /// <summary>Kết quả chấm (tối đa 1 – BR-22).</summary>
    public Evaluation? Evaluation { get; set; }

    /// <summary>Giải đã trao cho bài này (tối đa 1 – BR-31).</summary>
    public AwardIssued? AwardIssued { get; set; }

    /// <summary>Các lần gửi triển lãm.</summary>
    public ICollection<ExhibitionEntry> ExhibitionEntries { get; set; } = new List<ExhibitionEntry>();
}
