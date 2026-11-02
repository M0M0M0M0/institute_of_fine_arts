using InstituteOfFineArts.Web.Models.Enums;

namespace InstituteOfFineArts.Web.Models.Entities;

/// <summary>
/// Domestic art exhibition (table exhibition).
/// </summary>
public class Exhibition : BaseEntity
{
    /// <summary>Exhibition name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Description.</summary>
    public string? Description { get; set; }

    /// <summary>Venue.</summary>
    public string Venue { get; set; } = string.Empty;

    /// <summary>City / province.</summary>
    public string City { get; set; } = string.Empty;

    /// <summary>Organizer.</summary>
    public string? Organizer { get; set; }

    /// <summary>Start date.</summary>
    public DateOnly StartDate { get; set; }

    /// <summary>End date.</summary>
    public DateOnly EndDate { get; set; }

    /// <summary>Status (derived from the dates – BR-42).</summary>
    public ExhibitionStatus Status { get; set; } = ExhibitionStatus.UPCOMING;

    /// <summary>Id of the staff profile that created the exhibition (staff or manager).</summary>
    public long CreatedBy { get; set; }

    /// <summary>Staff member who created the exhibition.</summary>
    public StaffProfile CreatedByStaff { get; set; } = null!;

    /// <summary>Paintings sent to the exhibition.</summary>
    public ICollection<ExhibitionEntry> Entries { get; set; } = new List<ExhibitionEntry>();
}
