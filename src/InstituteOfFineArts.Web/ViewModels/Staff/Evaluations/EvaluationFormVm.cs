namespace InstituteOfFineArts.Web.ViewModels.Staff.Evaluations;

/// <summary>
/// Grading form of one judge for one entry: a score 0–100 and an optional comment per criterion of the competition,
/// and the general comments. Instead of scores the judge may disqualify the entry with a reason (written in
/// <see cref="NegativePoints"/>). Every rule is checked again by EvaluationService (web and mobile app alike).
/// </summary>
public class EvaluationFormVm
{
    /// <summary>Longest general comment.</summary>
    public const int MaxCommentLength = 2000;

    /// <summary>Longest comment on one criterion.</summary>
    public const int MaxCriterionCommentLength = 1000;

    /// <summary>A score is missing.</summary>
    public const string ScoreRequiredMessage = "Please give a score from 0 to 100 for every criterion";

    /// <summary>A score is out of range or has more than 2 decimals.</summary>
    public const string ScoreRangeMessage = "A score must be between 0 and 100, with at most 2 decimals";

    /// <summary>Disqualified without a reason.</summary>
    public const string DisqualifiedReasonMessage = "Please enter the reason for disqualification in Negative points";

    /// <summary>Graded without positive points.</summary>
    public const string PositivePointsMessage = "Please enter the positive points of the entry";

    /// <summary>True = the judge disqualifies the entry (no scores, reason required).</summary>
    public bool Disqualified { get; set; }

    /// <summary>One line per criterion of the competition.</summary>
    public List<CriterionScoreVm> Scores { get; set; } = [];

    /// <summary>General comment: strengths (required unless disqualified).</summary>
    public string? PositivePoints { get; set; }

    /// <summary>General comment: weaknesses (the reason when disqualified).</summary>
    public string? NegativePoints { get; set; }

    /// <summary>General comment: what to improve.</summary>
    public string? ImprovementAreas { get; set; }

    /// <summary>
    /// Version of this judge's grade when the form was opened (empty = none yet); a save is refused when it changed
    /// meanwhile (BA-07). Null = not checked.
    /// </summary>
    public string? Version { get; set; }
}

/// <summary>Score and comment of one criterion on the grading form.</summary>
public class CriterionScoreVm
{
    /// <summary>competition_criterion.id.</summary>
    public long CompetitionCriterionId { get; set; }

    /// <summary>Score 0–100 (null = not given yet).</summary>
    public decimal? Score { get; set; }

    /// <summary>Optional comment on this criterion.</summary>
    public string? Comment { get; set; }
}
