using System.ComponentModel.DataAnnotations;
using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.ViewModels.Staff.Submissions;

namespace InstituteOfFineArts.Web.ViewModels.Reports;

/// <summary>
/// Filters of RPT-01 "Submissions of one competition in a date range by rating" (F-M10-01, the example of the
/// assignment): competition (required), submitted date range (both days included), ratings and class.
/// </summary>
public class Rpt01FilterVm
{
    /// <summary>Value of the "Not graded" choice in the rating list (entries without an evaluation).</summary>
    public const string NotGraded = "NOT_GRADED";

    /// <summary>Competition of the report (required).</summary>
    [Required(ErrorMessage = "Please choose a competition.")]
    [Display(Name = "Competition")]
    public long? CompetitionId { get; set; }

    /// <summary>Submitted on or after this day (from 00:00).</summary>
    [DataType(DataType.Date)]
    [Display(Name = "Submitted from")]
    public DateOnly? From { get; set; }

    /// <summary>Submitted on or before this day (until 23:59:59, TC-M10-02).</summary>
    [DataType(DataType.Date)]
    [Display(Name = "Submitted to")]
    public DateOnly? To { get; set; }

    /// <summary>Wanted ratings: Rating names and/or <see cref="NotGraded"/>; empty = every entry.</summary>
    [Display(Name = "Ratings")]
    public List<string> Ratings { get; set; } = new();

    /// <summary>Only students of this class (null = every class).</summary>
    [StringLength(50)]
    [Display(Name = "Class")]
    public string? ClassName { get; set; }
}

/// <summary>One row of RPT-01 (columns of the BA doc §7).</summary>
public class Rpt01RowVm
{
    /// <summary>Row number (STT).</summary>
    public int No { get; set; }

    /// <summary>Submission id (link to the details page).</summary>
    public long SubmissionId { get; set; }

    /// <summary>Student code.</summary>
    public string StudentCode { get; set; } = string.Empty;

    /// <summary>Full name of the student.</summary>
    public string StudentName { get; set; } = string.Empty;

    /// <summary>Class of the student.</summary>
    public string? ClassName { get; set; }

    /// <summary>Title of the painting.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Submission time.</summary>
    public DateTime SubmittedAt { get; set; }

    /// <summary>Time of the last change of the entry (null = never changed).</summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>Rating, or null when not graded yet.</summary>
    public Rating? Rating { get; set; }

    /// <summary>Full name of the staff member who evaluated the entry.</summary>
    public string? EvaluatorName { get; set; }

    /// <summary>Time of the evaluation.</summary>
    public DateTime? EvaluatedAt { get; set; }

    /// <summary>Name of the award issued to the entry, or null when none.</summary>
    public string? AwardName { get; set; }
}

/// <summary>Result of RPT-01: the rows (at most the row cap) and the totals of the whole result.</summary>
public class Rpt01ResultVm
{
    /// <summary>Code of the competition.</summary>
    public string CompetitionCode { get; set; } = string.Empty;

    /// <summary>Title of the competition.</summary>
    public string CompetitionTitle { get; set; } = string.Empty;

    /// <summary>Rows in submission order.</summary>
    public IReadOnlyList<Rpt01RowVm> Rows { get; set; } = Array.Empty<Rpt01RowVm>();

    /// <summary>Total number of matching entries (may exceed the rows shown when truncated).</summary>
    public int TotalCount { get; set; }

    /// <summary>True when more rows matched than the report shows.</summary>
    public bool IsTruncated => TotalCount > Rows.Count;

    /// <summary>Number of matching entries per rating (every rating listed, 0 when none).</summary>
    public IReadOnlyDictionary<Rating, int> CountByRating { get; set; } = new Dictionary<Rating, int>();

    /// <summary>Number of matching entries without an evaluation.</summary>
    public int NotGradedCount { get; set; }
}

/// <summary>Page model of /reports/rpt-01: filters, dropdown options and the result (null before the first run).</summary>
public class Rpt01PageVm
{
    /// <summary>Filters as entered.</summary>
    public Rpt01FilterVm Filter { get; set; } = new();

    /// <summary>Competitions for the dropdown.</summary>
    public IReadOnlyList<CompetitionOptionVm> Competitions { get; set; } = Array.Empty<CompetitionOptionVm>();

    /// <summary>Distinct class names for the dropdown.</summary>
    public IReadOnlyList<string> Classes { get; set; } = Array.Empty<string>();

    /// <summary>Report result; null when the report has not been run or the filters are invalid.</summary>
    public Rpt01ResultVm? Result { get; set; }
}
