namespace InstituteOfFineArts.Web.Models.Entities;

/// <summary>
/// Giải thưởng dự kiến của một cuộc thi (bảng competition_award).
/// </summary>
public class CompetitionAward : BaseEntity
{
    /// <summary>Khóa ngoại tới cuộc thi.</summary>
    public long CompetitionId { get; set; }

    /// <summary>Tên giải: Giải Nhất, Nhì, Khuyến khích…</summary>
    public string AwardName { get; set; } = string.Empty;

    /// <summary>Thứ hạng, 1 = cao nhất (lưu ở cột rank_no để tránh trùng hàm RANK của SQL).</summary>
    public int Rank { get; set; }

    /// <summary>Giá trị giải thưởng (VNĐ) – yêu cầu tài chính.</summary>
    public decimal PrizeValue { get; set; }

    /// <summary>Mô tả phần thưởng: hiện vật, giấy khen…</summary>
    public string? PrizeDescription { get; set; }

    /// <summary>Số suất của giải này.</summary>
    public int Quantity { get; set; } = 1;

    /// <summary>Cuộc thi.</summary>
    public Competition Competition { get; set; } = null!;

    /// <summary>Các lần trao giải này.</summary>
    public ICollection<AwardIssued> IssuedAwards { get; set; } = new List<AwardIssued>();
}
