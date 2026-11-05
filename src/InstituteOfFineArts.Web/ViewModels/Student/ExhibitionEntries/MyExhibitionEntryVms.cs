using System.ComponentModel.DataAnnotations;
using InstituteOfFineArts.Web.Models.Enums;

namespace InstituteOfFineArts.Web.ViewModels.Student.ExhibitionEntries;

/// <summary>
/// Filters of "My exhibition entries" /student/exhibition-entries (F-M09-10, SCR-49): status, exhibition, page.
/// </summary>
public class MyEntrySearchVm
{
    /// <summary>Only entries with this status.</summary>
    [Display(Name = "Status")]
    public EntryStatus? Status { get; set; }

    /// <summary>Only entries of this exhibition.</summary>
    [Display(Name = "Exhibition")]
    public long? ExhibitionId { get; set; }

    /// <summary>Page number (1-based).</summary>
    public int Page { get; set; } = 1;
}

/// <summary>An exhibition the student has sent paintings to (options of the exhibition filter).</summary>
/// <param name="Id">Exhibition id</param>
/// <param name="Name">Exhibition name</param>
public record MyExhibitionOptionVm(long Id, string Name);

/// <summary>
/// One row of "My exhibition entries" (US-13 AC5): painting, exhibition, asking price, status, sold?, sold price,
/// paid? + paid date.
/// </summary>
public class MyEntryListItemVm
{
    /// <summary>Entry id.</summary>
    public long Id { get; set; }

    /// <summary>Submission id (thumbnail /files/submissions/{id}/thumb).</summary>
    public long SubmissionId { get; set; }

    /// <summary>Painting title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>True when a thumbnail exists.</summary>
    public bool HasThumbnail { get; set; }

    /// <summary>Exhibition id.</summary>
    public long ExhibitionId { get; set; }

    /// <summary>Exhibition name.</summary>
    public string ExhibitionName { get; set; } = string.Empty;

    /// <summary>First day of the exhibition.</summary>
    public DateOnly StartDate { get; set; }

    /// <summary>Last day of the exhibition.</summary>
    public DateOnly EndDate { get; set; }

    /// <summary>Asking price set by the student (BR-41).</summary>
    public decimal AskingPrice { get; set; }

    /// <summary>Entry status.</summary>
    public EntryStatus Status { get; set; }

    /// <summary>Price the customer paid, only when SOLD.</summary>
    public decimal? SoldPrice { get; set; }

    /// <summary>Whether the sale money has been paid to the student (BR-45).</summary>
    public bool PaidToStudent { get; set; }

    /// <summary>Payment date, only when paid.</summary>
    public DateOnly? PaidDate { get; set; }

    /// <summary>Price and withdraw can still be changed by the student (PENDING, BR-46).</summary>
    public bool CanChange => Status == EntryStatus.PENDING;
}

/// <summary>
/// Details of an own exhibition entry (F-M09-10): painting, exhibition, asking price, status, reject reason,
/// sale (price, date) and payment to the student. The customer is not shown to the student (personal data).
/// </summary>
public class MyEntryDetailsVm
{
    /// <summary>Entry id.</summary>
    public long Id { get; set; }

    /// <summary>Submission id.</summary>
    public long SubmissionId { get; set; }

    /// <summary>Painting title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Competition of the painting.</summary>
    public string CompetitionTitle { get; set; } = string.Empty;

    /// <summary>Rating of the painting (BEST / BETTER).</summary>
    public Rating? Rating { get; set; }

    /// <summary>Exhibition id.</summary>
    public long ExhibitionId { get; set; }

    /// <summary>Exhibition name.</summary>
    public string ExhibitionName { get; set; } = string.Empty;

    /// <summary>Venue of the exhibition.</summary>
    public string Venue { get; set; } = string.Empty;

    /// <summary>City of the exhibition.</summary>
    public string City { get; set; } = string.Empty;

    /// <summary>First day of the exhibition.</summary>
    public DateOnly StartDate { get; set; }

    /// <summary>Last day of the exhibition.</summary>
    public DateOnly EndDate { get; set; }

    /// <summary>Exhibition status calculated for today.</summary>
    public ExhibitionStatus ExhibitionStatus { get; set; }

    /// <summary>Asking price set by the student.</summary>
    public decimal AskingPrice { get; set; }

    /// <summary>Entry status.</summary>
    public EntryStatus Status { get; set; }

    /// <summary>Reason given by staff when REJECTED.</summary>
    public string? RejectReason { get; set; }

    /// <summary>Price the customer paid.</summary>
    public decimal? SoldPrice { get; set; }

    /// <summary>Sale date.</summary>
    public DateOnly? SoldDate { get; set; }

    /// <summary>Whether the sale money has been paid to the student.</summary>
    public bool PaidToStudent { get; set; }

    /// <summary>Amount paid to the student.</summary>
    public decimal? PaidAmount { get; set; }

    /// <summary>Payment date.</summary>
    public DateOnly? PaidDate { get; set; }

    /// <summary>When the entry was created.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>True when staff created the entry directly with the price the student quoted (BR-41).</summary>
    public bool CreatedByStaff { get; set; }

    /// <summary>Price and withdraw can still be changed by the student (PENDING, BR-46).</summary>
    public bool CanChange => Status == EntryStatus.PENDING;
}

/// <summary>A painting the student may propose (option of the painting dropdown, BR-40, BR-43).</summary>
/// <param name="Id">Submission id</param>
/// <param name="Title">Painting title</param>
/// <param name="CompetitionTitle">Competition title</param>
/// <param name="Rating">Rating (BEST / BETTER)</param>
public record MyCandidateOptionVm(long Id, string Title, string CompetitionTitle, Rating Rating);

/// <summary>
/// Proposal form /student/exhibition-entries/new?exhibitionId= (F-M09-04, US-13 AC1): painting + asking price.
/// The rules are checked here for the form and again in StudentExhibitionEntryService.
/// </summary>
public class ProposeEntryFormVm
{
    /// <summary>Target exhibition (from the query string; must be UPCOMING, BR-42).</summary>
    public long ExhibitionId { get; set; }

    /// <summary>Exhibition name (display only).</summary>
    public string? ExhibitionName { get; set; }

    /// <summary>City (display only).</summary>
    public string? City { get; set; }

    /// <summary>First day of the exhibition (display only).</summary>
    public DateOnly StartDate { get; set; }

    /// <summary>Last day of the exhibition (display only).</summary>
    public DateOnly EndDate { get; set; }

    /// <summary>Paintings that may be proposed (display only, loaded from the database).</summary>
    public List<MyCandidateOptionVm> Candidates { get; set; } = new();

    /// <summary>Chosen painting.</summary>
    [Required(ErrorMessage = "Please choose a painting")]
    [Display(Name = "Painting")]
    public long? SubmissionId { get; set; }

    /// <summary>Asking price in USD (BR-41).</summary>
    [Required(ErrorMessage = Services.Implementations.ExhibitionEntryRules.PriceRequiredMessage)]
    [Range(0.01d, 9999999999999999d, ErrorMessage = Services.Implementations.ExhibitionEntryRules.PriceMessage)]
    [Display(Name = "Asking price (USD)")]
    public decimal? AskingPrice { get; set; }
}

/// <summary>Change the asking price while the entry is PENDING (BR-46).</summary>
public class EditPriceFormVm
{
    /// <summary>Entry id (from the route).</summary>
    public long Id { get; set; }

    /// <summary>Painting title (display only).</summary>
    public string? Title { get; set; }

    /// <summary>Exhibition name (display only).</summary>
    public string? ExhibitionName { get; set; }

    /// <summary>New asking price in USD (BR-41).</summary>
    [Required(ErrorMessage = Services.Implementations.ExhibitionEntryRules.PriceRequiredMessage)]
    [Range(0.01d, 9999999999999999d, ErrorMessage = Services.Implementations.ExhibitionEntryRules.PriceMessage)]
    [Display(Name = "Asking price (USD)")]
    public decimal? AskingPrice { get; set; }
}
