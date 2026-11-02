namespace InstituteOfFineArts.Web.Models.Entities;

/// <summary>
/// Customer who bought a painting at an exhibition (table customer).
/// </summary>
public class Customer : BaseEntity
{
    /// <summary>Full name.</summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>Email.</summary>
    public string? Email { get; set; }

    /// <summary>Phone number.</summary>
    public string? Phone { get; set; }

    /// <summary>Address.</summary>
    public string? Address { get; set; }

    /// <summary>Notes.</summary>
    public string? Notes { get; set; }

    /// <summary>Paintings bought by this customer.</summary>
    public ICollection<ExhibitionEntry> Purchases { get; set; } = new List<ExhibitionEntry>();
}
