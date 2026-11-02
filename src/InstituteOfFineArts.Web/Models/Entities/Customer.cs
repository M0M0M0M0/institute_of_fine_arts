namespace InstituteOfFineArts.Web.Models.Entities;

/// <summary>
/// Khách mua tranh tại triển lãm (bảng customer).
/// </summary>
public class Customer : BaseEntity
{
    /// <summary>Họ và tên.</summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>Email.</summary>
    public string? Email { get; set; }

    /// <summary>Số điện thoại.</summary>
    public string? Phone { get; set; }

    /// <summary>Địa chỉ.</summary>
    public string? Address { get; set; }

    /// <summary>Ghi chú.</summary>
    public string? Notes { get; set; }

    /// <summary>Các tranh khách đã mua.</summary>
    public ICollection<ExhibitionEntry> Purchases { get; set; } = new List<ExhibitionEntry>();
}
