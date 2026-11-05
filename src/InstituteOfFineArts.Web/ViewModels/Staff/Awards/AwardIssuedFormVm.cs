using System.ComponentModel.DataAnnotations;
using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.ViewModels.Staff.Submissions;

namespace InstituteOfFineArts.Web.ViewModels.Staff.Awards;

/// <summary>
/// Issue / edit form of an award given to a submission (F-M08-01/02, task BE1-11): competition (CLOSED or GRADED),
/// one of its awards with slots left, an eligible entry (BR-30, BR-31), issued date and remarks.
/// The rules are checked here for the form and again in AwardIssuedService.
/// </summary>
public class AwardIssuedFormVm
{
    /// <summary>Longest remarks accepted (the column is nvarchar(max); 1000 keeps the lists readable).</summary>
    public const int MaxRemarksLength = 1000;

    /// <summary>Message when no competition is chosen.</summary>
    public const string CompetitionRequiredMessage = "Please choose a competition";

    /// <summary>Message when no award is chosen.</summary>
    public const string AwardRequiredMessage = "Please choose an award";

    /// <summary>Message when no entry is chosen.</summary>
    public const string SubmissionRequiredMessage = "Please choose the winning entry";

    /// <summary>Message when the issued date is empty.</summary>
    public const string IssuedDateRequiredMessage = "Please enter the issued date";

    /// <summary>Message when the remarks are too long.</summary>
    public const string RemarksTooLongMessage = "Remarks must be at most 1000 characters";

    /// <summary>award_issued.id (0 when issuing a new award).</summary>
    public long Id { get; set; }

    /// <summary>Competition (fixed when editing: taken from the database, never from the form).</summary>
    [Required(ErrorMessage = CompetitionRequiredMessage)]
    [Display(Name = "Competition")]
    public long? CompetitionId { get; set; }

    /// <summary>Defined award of the competition (CompetitionAward) with a slot left.</summary>
    [Required(ErrorMessage = AwardRequiredMessage)]
    [Display(Name = "Award")]
    public long? CompetitionAwardId { get; set; }

    /// <summary>Winning entry: evaluated BEST in this competition and without an award yet (BR-30, BR-31).</summary>
    [Required(ErrorMessage = SubmissionRequiredMessage)]
    [Display(Name = "Winning entry")]
    public long? SubmissionId { get; set; }

    /// <summary>Date the award is issued (default today).</summary>
    [Required(ErrorMessage = IssuedDateRequiredMessage)]
    [DataType(DataType.Date)]
    [Display(Name = "Issued date")]
    public DateOnly? IssuedDate { get; set; }

    /// <summary>Remarks about the award / the competition (A-16).</summary>
    [StringLength(MaxRemarksLength, ErrorMessage = RemarksTooLongMessage)]
    [Display(Name = "Remarks")]
    public string? Remarks { get; set; }

    /// <summary>Competition title (display only).</summary>
    public string? CompetitionTitle { get; set; }

    /// <summary>Competition code (display only).</summary>
    public string? CompetitionCode { get; set; }

    /// <summary>Competition status calculated for today (display only).</summary>
    public CompetitionStatus? CompetitionStatus { get; set; }

    /// <summary>Competitions that accept awards: CLOSED or GRADED (BR-32). Used when issuing a new award.</summary>
    public IReadOnlyList<CompetitionOptionVm> Competitions { get; set; } = [];

    /// <summary>Awards of the chosen competition that still have a slot (UC-06 2a), plus the current one when editing.</summary>
    public IReadOnlyList<AwardOptionVm> Awards { get; set; } = [];

    /// <summary>Eligible entries of the chosen competition (UC-06 2b), plus the current one when editing.</summary>
    public IReadOnlyList<EligibleSubmissionVm> Submissions { get; set; } = [];
}

/// <summary>
/// Reason form of the revoke action (BR-33): the reason is required and is written to the log.
/// </summary>
public class RevokeAwardVm
{
    /// <summary>Longest reason accepted.</summary>
    public const int MaxReasonLength = 500;

    /// <summary>Message when the reason is empty (BR-33).</summary>
    public const string ReasonRequiredMessage = "Please enter the reason for revoking this award.";

    /// <summary>Message when the reason is too long.</summary>
    public const string ReasonTooLongMessage = "The reason must be at most 500 characters.";

    /// <summary>Why the award is revoked.</summary>
    [Required(ErrorMessage = ReasonRequiredMessage)]
    [StringLength(MaxReasonLength, ErrorMessage = ReasonTooLongMessage)]
    [Display(Name = "Reason")]
    public string? Reason { get; set; }
}

/// <summary>
/// One award option of the issue form.
/// </summary>
/// <param name="Id">competition_award.id</param>
/// <param name="Name">Award name</param>
/// <param name="Rank">1 = highest</param>
/// <param name="PrizeValue">Prize in USD</param>
/// <param name="Quantity">How many can be given</param>
/// <param name="IssuedCount">How many have been given (without the award being edited)</param>
public record AwardOptionVm(long Id, string Name, int Rank, decimal PrizeValue, int Quantity, int IssuedCount)
{
    /// <summary>Slots still free (BR-31).</summary>
    public int SlotsLeft => Math.Max(0, Quantity - IssuedCount);
}

/// <summary>
/// One entry that may receive an award (evaluated BEST, no award yet).
/// </summary>
/// <param name="Id">Submission id</param>
/// <param name="Title">Painting title</param>
/// <param name="StudentName">Full name of the author</param>
/// <param name="StudentCode">Student code of the author</param>
public record EligibleSubmissionVm(long Id, string Title, string StudentName, string StudentCode);
