using System.ComponentModel.DataAnnotations;
using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.Services.Implementations;

namespace InstituteOfFineArts.Web.ViewModels.Staff.ExhibitionEntries;

/// <summary>A painting staff may send to the exhibition (option of the painting dropdown, BR-40, BR-43).</summary>
/// <param name="Id">Submission id</param>
/// <param name="Title">Painting title</param>
/// <param name="StudentName">Full name of the student</param>
/// <param name="StudentCode">Student code</param>
/// <param name="CompetitionTitle">Competition title</param>
/// <param name="Rating">Rating (BEST / BETTER)</param>
public record EntryCandidateOptionVm(long Id, string Title, string StudentName, string StudentCode, string CompetitionTitle, Rating Rating);

/// <summary>A customer in the buyer dropdown of the sale form.</summary>
/// <param name="Id">Customer id</param>
/// <param name="FullName">Full name</param>
/// <param name="Phone">Phone number</param>
/// <param name="Email">Email</param>
public record EntryCustomerOptionVm(long Id, string FullName, string? Phone, string? Email);

/// <summary>
/// Staff direct create /staff/exhibition-entries/new?exhibitionId= (F-M09-05, UC-07 3b): a BEST/BETTER painting is
/// sent with the price the student quoted plus a note about where that price comes from (BR-41); the entry is
/// ACCEPTED immediately. The rules are checked here for the form and again in ExhibitionEntryService.
/// </summary>
public class EntryCreateFormVm
{
    /// <summary>Message when the note about the price source is missing (BR-41).</summary>
    public const string NotesRequiredMessage = "Please note where the student's asking price comes from (e.g. email of the student on 12 Nov)";

    /// <summary>Target exhibition (from the query string; must be UPCOMING, BR-42).</summary>
    public long ExhibitionId { get; set; }

    /// <summary>Exhibition name (display only).</summary>
    public string? ExhibitionName { get; set; }

    /// <summary>First day of the exhibition (display only).</summary>
    public DateOnly StartDate { get; set; }

    /// <summary>Last day of the exhibition (display only).</summary>
    public DateOnly EndDate { get; set; }

    /// <summary>Paintings that may be sent (display only, loaded from the database).</summary>
    public List<EntryCandidateOptionVm> Candidates { get; set; } = new();

    /// <summary>Chosen painting.</summary>
    [Required(ErrorMessage = "Please choose a painting")]
    [Display(Name = "Painting")]
    public long? SubmissionId { get; set; }

    /// <summary>Price the student quoted, in USD (BR-41).</summary>
    [Required(ErrorMessage = ExhibitionEntryRules.PriceRequiredMessage)]
    [Range(0.01d, 9999999999999999d, ErrorMessage = ExhibitionEntryRules.PriceMessage)]
    [Display(Name = "Student's asking price (USD)")]
    public decimal? AskingPrice { get; set; }

    /// <summary>Where the price comes from (BR-41), stored in Notes.</summary>
    [Required(ErrorMessage = NotesRequiredMessage)]
    [StringLength(500, ErrorMessage = "The note must be at most 500 characters")]
    [Display(Name = "Source of the price")]
    public string? Notes { get; set; }
}

/// <summary>Reject a pending proposal (F-M09-05, UC-07 3a): the reason is required and shown to the student.</summary>
public class RejectFormVm
{
    /// <summary>Message when the reason is missing.</summary>
    public const string ReasonRequiredMessage = "Please enter the reason for rejecting";

    /// <summary>Reason for rejecting (stored in RejectReason).</summary>
    [Required(ErrorMessage = ReasonRequiredMessage)]
    [StringLength(500, ErrorMessage = "The reason must be at most 500 characters")]
    [Display(Name = "Reason for rejecting")]
    public string? Reason { get; set; }
}

/// <summary>
/// Record the sale of an accepted painting (F-M09-06, BR-44, US-13 AC3, TC-M09-05/06): sold price &gt; 0
/// (default = asking price), sale date within the exhibition dates and not in the future, customer required.
/// </summary>
public class SoldFormVm
{
    /// <summary>Message when the sold price is missing (TC-M09-06).</summary>
    public const string PriceRequiredMessage = "Please enter the sold price";

    /// <summary>Message when the sold price is 0 or negative (BR-44).</summary>
    public const string PriceMessage = "Sold price must be greater than 0 (USD)";

    /// <summary>Message when the sale date is missing.</summary>
    public const string DateRequiredMessage = "Please enter the sale date";

    /// <summary>Message when no customer is chosen (US-13 AC3, TC-M09-06).</summary>
    public const string CustomerRequiredMessage = "Please choose the customer who bought the painting";

    /// <summary>Price the customer paid.</summary>
    [Required(ErrorMessage = PriceRequiredMessage)]
    [Range(0.01d, 9999999999999999d, ErrorMessage = PriceMessage)]
    [Display(Name = "Sold price (USD)")]
    public decimal? SoldPrice { get; set; }

    /// <summary>Sale date.</summary>
    [Required(ErrorMessage = DateRequiredMessage)]
    [DataType(DataType.Date)]
    [Display(Name = "Sale date")]
    public DateOnly? SoldDate { get; set; }

    /// <summary>Buyer (customer table).</summary>
    [Required(ErrorMessage = CustomerRequiredMessage)]
    [Display(Name = "Customer")]
    public long? CustomerId { get; set; }
}

/// <summary>
/// Record the payment of the sale money to the student (F-M09-08, BR-45, US-13 AC4, TC-M09-07/08): only for a SOLD
/// painting; the amount is the full sale price; payment date on or after the sale date and not in the future.
/// </summary>
public class PaidFormVm
{
    /// <summary>Message when the amount is missing.</summary>
    public const string AmountRequiredMessage = "Please enter the amount paid to the student";

    /// <summary>Message when the amount is 0 or negative.</summary>
    public const string AmountMessage = "The amount paid must be greater than 0 (USD)";

    /// <summary>Message when the payment date is missing.</summary>
    public const string DateRequiredMessage = "Please enter the payment date";

    /// <summary>Amount paid to the student (default = sold price, BR-45).</summary>
    [Required(ErrorMessage = AmountRequiredMessage)]
    [Range(0.01d, 9999999999999999d, ErrorMessage = AmountMessage)]
    [Display(Name = "Amount paid (USD)")]
    public decimal? PaidAmount { get; set; }

    /// <summary>Payment date.</summary>
    [Required(ErrorMessage = DateRequiredMessage)]
    [DataType(DataType.Date)]
    [Display(Name = "Payment date")]
    public DateOnly? PaidDate { get; set; }
}
