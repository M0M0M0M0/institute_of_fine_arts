using InstituteOfFineArts.Web.Models.Enums;

namespace InstituteOfFineArts.Web.ViewModels.Staff.Evaluations;

/// <summary>
/// Evaluation page of one submission (/staff/evaluations/{submissionId}, SCR-27, task BE1-10):
/// a summary of the entry, the current evaluation (if any) and the form with what the staff member may do.
/// </summary>
public class EvaluationPageVm
{
    /// <summary>Submission id.</summary>
    public long SubmissionId { get; set; }

    /// <summary>Painting title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Student full name.</summary>
    public string StudentName { get; set; } = string.Empty;

    /// <summary>Student code.</summary>
    public string StudentCode { get; set; } = string.Empty;

    /// <summary>Competition id (for the link back to the submission list).</summary>
    public long CompetitionId { get; set; }

    /// <summary>Competition title.</summary>
    public string CompetitionTitle { get; set; } = string.Empty;

    /// <summary>Competition code.</summary>
    public string CompetitionCode { get; set; } = string.Empty;

    /// <summary>Calculated competition status (CompetitionStatusResolver).</summary>
    public CompetitionStatus CompetitionStatus { get; set; }

    /// <summary>Last day of the competition (deadline 23:59:59).</summary>
    public DateOnly EndDate { get; set; }

    /// <summary>Submission time.</summary>
    public DateTime SubmittedAt { get; set; }

    /// <summary>Submitted after 23:59:59 of the end date (BR-14) → only DISQUALIFIED is accepted.</summary>
    public bool IsLate { get; set; }

    /// <summary>Type of the accompanying text.</summary>
    public TextType TextType { get; set; }

    /// <summary>Poem / quotation / story.</summary>
    public string TextContent { get; set; } = string.Empty;

    /// <summary>Student's description of the painting.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Whether the submission already has an evaluation (BR-22: at most one).</summary>
    public bool HasEvaluation { get; set; }

    /// <summary>Full name of the staff member who evaluated / last edited (when evaluated).</summary>
    public string? EvaluatedByName { get; set; }

    /// <summary>Time of the evaluation / last edit (when evaluated).</summary>
    public DateTime? EvaluatedAt { get; set; }

    /// <summary>Whether the form can be saved now (competition CLOSED, BR-21).</summary>
    public bool CanEvaluate { get; set; }

    /// <summary>Why the form cannot be saved (null when it can).</summary>
    public string? ReadOnlyReason { get; set; }

    /// <summary>Whether the evaluation can be deleted (not GRADED, no award, not sent to an exhibition).</summary>
    public bool CanDelete { get; set; }

    /// <summary>Why an existing evaluation cannot be deleted (null when it can or when there is none).</summary>
    public string? DeleteBlockedReason { get; set; }

    /// <summary>Ratings the staff member may choose (only DISQUALIFIED for a late entry).</summary>
    public IReadOnlyList<Rating> AllowedRatings { get; set; } = [];

    /// <summary>Form values: the current evaluation, or an empty / suggested one.</summary>
    public EvaluationFormVm Form { get; set; } = new();
}
