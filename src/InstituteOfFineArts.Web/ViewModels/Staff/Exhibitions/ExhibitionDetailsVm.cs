using InstituteOfFineArts.Web.Models.Enums;

namespace InstituteOfFineArts.Web.ViewModels.Staff.Exhibitions;

/// <summary>
/// Exhibition details page for Staff and Manager (F-M09-01): information, status and entry counts by status.
/// </summary>
public class ExhibitionDetailsVm
{
    /// <summary>Exhibition id.</summary>
    public long Id { get; set; }

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

    /// <summary>Status calculated for today (BR-42).</summary>
    public ExhibitionStatus Status { get; set; }

    /// <summary>Name of the staff member / manager who created it.</summary>
    public string CreatedByName { get; set; } = string.Empty;

    /// <summary>When it was created.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Number of entries for every entry status (statuses without entries have 0).</summary>
    public IReadOnlyList<EntryCountVm> EntryCounts { get; set; } = Array.Empty<EntryCountVm>();

    /// <summary>Total number of entries (all statuses).</summary>
    public int TotalEntries => EntryCounts.Sum(c => c.Count);

    /// <summary>Sum of the sale prices of the SOLD entries (USD).</summary>
    public decimal SoldTotal { get; set; }

    /// <summary>Only an exhibition without entries can be deleted (BR-42, US-12 AC2).</summary>
    public bool CanDelete => TotalEntries == 0;
}

/// <summary>Number of entries with one status.</summary>
/// <param name="Status">Entry status</param>
/// <param name="Count">Number of entries</param>
public record EntryCountVm(EntryStatus Status, int Count);
