using InstituteOfFineArts.Web.Models.Enums;

namespace InstituteOfFineArts.Web.ViewModels.Staff.Evaluations;

/// <summary>
/// Grading page of one entry for one judge (web /teacher/evaluations/{id} and the mobile grading screen): the entry,
/// the criteria with their weights, the judge's own grade in the form, and who in the jury has graded (never the
/// other judges' scores before the results are published).
/// </summary>
public class EvaluationPageVm
{
    /// <summary>Entry id.</summary>
    public long SubmissionId { get; set; }

    /// <summary>Entry title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Student full name.</summary>
    public string StudentName { get; set; } = string.Empty;

    /// <summary>Student code.</summary>
    public string StudentCode { get; set; } = string.Empty;

    /// <summary>Competition id.</summary>
    public long CompetitionId { get; set; }

    /// <summary>Competition title.</summary>
    public string CompetitionTitle { get; set; } = string.Empty;

    /// <summary>Competition code.</summary>
    public string CompetitionCode { get; set; } = string.Empty;

    /// <summary>Competition status today.</summary>
    public CompetitionStatus CompetitionStatus { get; set; }

    /// <summary>Last day for entries.</summary>
    public DateOnly EndDate { get; set; }

    /// <summary>When the entry was submitted.</summary>
    public DateTime SubmittedAt { get; set; }

    /// <summary>Submitted after the deadline: can only be disqualified (BR-14).</summary>
    public bool IsLate { get; set; }

    /// <summary>Kind of accompanying text.</summary>
    public TextType TextType { get; set; }

    /// <summary>Accompanying text.</summary>
    public string TextContent { get; set; } = string.Empty;

    /// <summary>Student's description.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Pictures of the entry (ids, cover first).</summary>
    public IReadOnlyList<long> ImageIds { get; set; } = [];

    /// <summary>Criteria of the competition in display order.</summary>
    public IReadOnlyList<CriterionLineVm> Criteria { get; set; } = [];

    /// <summary>The viewer is on the jury of this competition.</summary>
    public bool IsJudge { get; set; }

    /// <summary>The viewer has already graded this entry.</summary>
    public bool HasEvaluation { get; set; }

    /// <summary>When the viewer last saved their grade.</summary>
    public DateTime? EvaluatedAt { get; set; }

    /// <summary>The viewer's weighted total (2 decimals), null when none / disqualified.</summary>
    public decimal? MyTotal { get; set; }

    /// <summary>The jury and who has graded this entry (names and done / not done, no scores).</summary>
    public IReadOnlyList<JuryProgressVm> Jury { get; set; } = [];

    /// <summary>The form can be saved now.</summary>
    public bool CanEvaluate { get; set; }

    /// <summary>Why the form is read only (null when it can be saved).</summary>
    public string? ReadOnlyReason { get; set; }

    /// <summary>The viewer's grade can be deleted now.</summary>
    public bool CanDelete { get; set; }

    /// <summary>Why it cannot be deleted.</summary>
    public string? DeleteBlockedReason { get; set; }

    /// <summary>The whole jury's score sheet, once the results are published (null before).</summary>
    public ScoreSheetVm? ScoreSheet { get; set; }

    /// <summary>The viewer's grade (or an empty form).</summary>
    public EvaluationFormVm Form { get; set; } = new();

    /// <summary>Kept for the mobile contract: the ratings a late entry may get (DISQUALIFIED only) or all of them.</summary>
    public IReadOnlyList<Rating> AllowedRatings => IsLate ? [Rating.DISQUALIFIED] : Enum.GetValues<Rating>();

    /// <summary>Kept for the mobile contract: name of the viewer when they have graded.</summary>
    public string? EvaluatedByName { get; set; }
}

/// <summary>One criterion of the competition on the grading form.</summary>
/// <param name="CompetitionCriterionId">competition_criterion.id</param>
/// <param name="Name">Criterion name</param>
/// <param name="Description">What to look at</param>
/// <param name="Weight">Weight in percent</param>
public record CriterionLineVm(long CompetitionCriterionId, string Name, string? Description, decimal Weight);

/// <summary>One jury member and whether they graded the entry.</summary>
/// <param name="Name">Judge's full name</param>
/// <param name="IsChair">Chair of the jury</param>
/// <param name="Done">Has graded this entry</param>
/// <param name="IsMe">The viewer</param>
public record JuryProgressVm(string Name, bool IsChair, bool Done, bool IsMe);

/// <summary>
/// The jury's full score sheet of one entry (shown after the results are published to the student who owns it, the
/// judges of the competition and staff; staff also once the entry is fully graded): one row per criterion, one
/// column per judge, the average per criterion, the total and the rating; each judge's comments.
/// </summary>
public class ScoreSheetVm
{
    /// <summary>Criteria in display order.</summary>
    public IReadOnlyList<CriterionLineVm> Criteria { get; set; } = [];

    /// <summary>One column per judge who graded.</summary>
    public IReadOnlyList<JudgeColumnVm> Judges { get; set; } = [];

    /// <summary>Average score per criterion (competition_criterion.id → average of the judges who scored it).</summary>
    public IReadOnlyDictionary<long, decimal> CriterionAverages { get; set; } = new Dictionary<long, decimal>();

    /// <summary>Average weighted total of the jury (null when disqualified or not complete).</summary>
    public decimal? Total { get; set; }

    /// <summary>Resulting rating (null when not complete).</summary>
    public Rating? Rating { get; set; }

    /// <summary>The entry was graded before criteria existed (no score sheet, only the rating).</summary>
    public bool Legacy { get; set; }
}

/// <summary>The grade of one judge on the score sheet.</summary>
public class JudgeColumnVm
{
    /// <summary>Judge's full name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Chair of the jury.</summary>
    public bool IsChair { get; set; }

    /// <summary>The judge disqualified the entry.</summary>
    public bool Disqualified { get; set; }

    /// <summary>Weighted total (2 decimals), null when disqualified.</summary>
    public decimal? Total { get; set; }

    /// <summary>Score and comment per competition_criterion.id.</summary>
    public IReadOnlyDictionary<long, (decimal Score, string? Comment)> Scores { get; set; } = new Dictionary<long, (decimal, string?)>();

    /// <summary>Strengths.</summary>
    public string? PositivePoints { get; set; }

    /// <summary>Weaknesses (the reason when disqualified).</summary>
    public string? NegativePoints { get; set; }

    /// <summary>What to improve.</summary>
    public string? ImprovementAreas { get; set; }

    /// <summary>When the judge saved.</summary>
    public DateTime EvaluatedAt { get; set; }
}
