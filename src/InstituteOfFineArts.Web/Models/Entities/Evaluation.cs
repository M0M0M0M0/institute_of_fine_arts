using InstituteOfFineArts.Web.Models.Enums;

namespace InstituteOfFineArts.Web.Models.Entities;

/// <summary>
/// Grading result of a submission (table evaluation).
/// </summary>
public class Evaluation : BaseEntity
{
    /// <summary>Foreign key to the submission (unique – at most one evaluation per submission).</summary>
    public long SubmissionId { get; set; }

    /// <summary>Id of the staff profile that graded / last edited the evaluation.</summary>
    public long EvaluatedBy { get; set; }

    /// <summary>Rating.</summary>
    public Rating Rating { get; set; }

    /// <summary>Positive points – required unless disqualified (BR-23).</summary>
    public string? PositivePoints { get; set; }

    /// <summary>Negative points – required when disqualified, used as the reason (BR-23).</summary>
    public string? NegativePoints { get; set; }

    /// <summary>Areas of improvement.</summary>
    public string? ImprovementAreas { get; set; }

    /// <summary>Grading time.</summary>
    public DateTime EvaluatedAt { get; set; }

    /// <summary>Submission.</summary>
    public Submission Submission { get; set; } = null!;

    /// <summary>Staff member who graded.</summary>
    public StaffProfile EvaluatedByStaff { get; set; } = null!;
}
