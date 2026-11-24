namespace InstituteOfFineArts.Web.Models.Entities;

/// <summary>
/// The grade one judge gave one entry (table judge_evaluation): a score 0–100 and a comment per criterion of the
/// competition, the weighted total, and the judge's general comments. A judge may instead disqualify the entry with
/// a reason. Judges grade independently and never see each other's grades before the results are published.
/// When every judge of the jury has graded the entry, its result (table evaluation) is calculated from these rows.
/// </summary>
public class JudgeEvaluation : BaseEntity
{
    /// <summary>Entry.</summary>
    public long SubmissionId { get; set; }

    /// <summary>staff_profile.id of the judge.</summary>
    public long JudgeId { get; set; }

    /// <summary>True = the judge disqualified the entry (reason in NegativePoints, no scores).</summary>
    public bool Disqualified { get; set; }

    /// <summary>Σ score × weight / 100, rounded to 2 decimals for display; null when disqualified.</summary>
    public decimal? TotalScore { get; set; }

    /// <summary>General comment: strengths.</summary>
    public string? PositivePoints { get; set; }

    /// <summary>General comment: weaknesses (the reason when disqualified).</summary>
    public string? NegativePoints { get; set; }

    /// <summary>General comment: what to improve.</summary>
    public string? ImprovementAreas { get; set; }

    /// <summary>When the judge last saved.</summary>
    public DateTime EvaluatedAt { get; set; }

    /// <summary>Entry.</summary>
    public Submission Submission { get; set; } = null!;

    /// <summary>The judge.</summary>
    public StaffProfile Judge { get; set; } = null!;

    /// <summary>Score per criterion.</summary>
    public ICollection<JudgeScore> Scores { get; set; } = new List<JudgeScore>();
}
