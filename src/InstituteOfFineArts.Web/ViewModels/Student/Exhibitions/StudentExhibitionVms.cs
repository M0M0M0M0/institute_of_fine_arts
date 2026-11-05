using InstituteOfFineArts.Web.Models.Enums;

namespace InstituteOfFineArts.Web.ViewModels.Student.Exhibitions;

/// <summary>
/// One exhibition in the student exhibition list (F-M09-02, F-M09-03).
/// </summary>
public class StudentExhibitionListItemVm
{
    /// <summary>Exhibition id.</summary>
    public long Id { get; set; }

    /// <summary>Exhibition name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Venue.</summary>
    public string Venue { get; set; } = string.Empty;

    /// <summary>City / province.</summary>
    public string City { get; set; } = string.Empty;

    /// <summary>Start date.</summary>
    public DateOnly StartDate { get; set; }

    /// <summary>End date.</summary>
    public DateOnly EndDate { get; set; }

    /// <summary>Status calculated for today (BR-42).</summary>
    public ExhibitionStatus Status { get; set; }

    /// <summary>Number of the signed-in student's own artworks sent to this exhibition.</summary>
    public int MyEntryCount { get; set; }
}

/// <summary>
/// Exhibition details for a student (F-M09-03): information, status, own entries and whether an artwork can be proposed.
/// </summary>
public class StudentExhibitionDetailsVm
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

    /// <summary>Artworks may only be proposed to an UPCOMING exhibition (BR-42).</summary>
    public bool CanPropose => Status == ExhibitionStatus.UPCOMING;

    /// <summary>The student's own artworks sent to this exhibition (read only).</summary>
    public IReadOnlyList<StudentExhibitionEntryRowVm> MyEntries { get; set; } = Array.Empty<StudentExhibitionEntryRowVm>();
}

/// <summary>One of the student's own artworks in an exhibition.</summary>
/// <param name="EntryId">exhibition_entry.id</param>
/// <param name="ArtworkTitle">Title of the submission</param>
/// <param name="AskingPrice">Price set by the student (BR-41)</param>
/// <param name="Status">Entry status</param>
/// <param name="SoldPrice">Sale price when SOLD</param>
/// <param name="PaidToStudent">Whether the sale money was paid to the student (BR-45)</param>
public record StudentExhibitionEntryRowVm(long EntryId, string ArtworkTitle, decimal AskingPrice, EntryStatus Status,
    decimal? SoldPrice, bool PaidToStudent);
