using System.ComponentModel.DataAnnotations;
using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.ViewModels.Staff.Submissions;

namespace InstituteOfFineArts.Web.ViewModels.Reports;

/// <summary>
/// Filters of RPT-02 "Participation &amp; ratings per competition" (F-M10-02): competition period, status, and the
/// competition whose non-submitters are listed (BR-12).
/// </summary>
public class Rpt02FilterVm
{
    /// <summary>Only competitions still running on or after this day (end date ≥ From).</summary>
    [DataType(DataType.Date)]
    [Display(Name = "Period from")]
    public DateOnly? From { get; set; }

    /// <summary>Only competitions started on or before this day (start date ≤ To).</summary>
    [DataType(DataType.Date)]
    [Display(Name = "Period to")]
    public DateOnly? To { get; set; }

    /// <summary>Only competitions with this actual status (null = all).</summary>
    [Display(Name = "Status")]
    public CompetitionStatus? Status { get; set; }

    /// <summary>Competition whose active students without an entry are listed (null = no list).</summary>
    [Display(Name = "Students who have not submitted to")]
    public long? NotSubmittedCompetitionId { get; set; }
}

/// <summary>One competition row of RPT-02.</summary>
public class Rpt02RowVm
{
    /// <summary>Competition id.</summary>
    public long CompetitionId { get; set; }

    /// <summary>Competition code.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Competition title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>First day.</summary>
    public DateOnly StartDate { get; set; }

    /// <summary>Last day.</summary>
    public DateOnly EndDate { get; set; }

    /// <summary>Actual status today (CompetitionStatusResolver).</summary>
    public CompetitionStatus Status { get; set; }

    /// <summary>Number of active students (accounts not locked) today.</summary>
    public int ActiveStudents { get; set; }

    /// <summary>Number of entries (any student).</summary>
    public int Submissions { get; set; }

    /// <summary>Number of distinct active students with an entry.</summary>
    public int Participants { get; set; }

    /// <summary>Participants / active students in percent (0 when there is no active student).</summary>
    public decimal ParticipationPercent { get; set; }

    /// <summary>Number of entries per rating (every rating listed).</summary>
    public IReadOnlyDictionary<Rating, int> CountByRating { get; set; } = new Dictionary<Rating, int>();

    /// <summary>Number of entries without an evaluation.</summary>
    public int NotGraded { get; set; }

    /// <summary>Number of active students without an entry (size of the BR-12 list).</summary>
    public int NotSubmitted => ActiveStudents - Participants;
}

/// <summary>One active student who has not submitted to the chosen competition (BR-12).</summary>
public class Rpt02StudentRowVm
{
    /// <summary>Row number.</summary>
    public int No { get; set; }

    /// <summary>student_profile.id.</summary>
    public long StudentId { get; set; }

    /// <summary>Student code.</summary>
    public string StudentCode { get; set; } = string.Empty;

    /// <summary>Full name.</summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>Class.</summary>
    public string? ClassName { get; set; }

    /// <summary>Course.</summary>
    public string CourseName { get; set; } = string.Empty;

    /// <summary>Email (to remind the student).</summary>
    public string Email { get; set; } = string.Empty;
}

/// <summary>Active students without an entry in one competition (BR-12 detail of RPT-02).</summary>
public class Rpt02NotSubmittedVm
{
    /// <summary>Competition id.</summary>
    public long CompetitionId { get; set; }

    /// <summary>Competition code.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Competition title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Students ordered by student code.</summary>
    public IReadOnlyList<Rpt02StudentRowVm> Students { get; set; } = Array.Empty<Rpt02StudentRowVm>();
}

/// <summary>Result of RPT-02: one row per competition plus the totals row.</summary>
public class Rpt02ResultVm
{
    /// <summary>Number of active students today (same in every row).</summary>
    public int ActiveStudents { get; set; }

    /// <summary>Competition rows, newest start first.</summary>
    public IReadOnlyList<Rpt02RowVm> Rows { get; set; } = Array.Empty<Rpt02RowVm>();

    /// <summary>Number of matching competitions (may exceed the rows shown when truncated).</summary>
    public int TotalCount { get; set; }

    /// <summary>True when more competitions matched than the report shows.</summary>
    public bool IsTruncated => TotalCount > Rows.Count;

    /// <summary>Totals row: entries of every matching competition.</summary>
    public int TotalSubmissions { get; set; }

    /// <summary>Totals row: participants summed over the competitions.</summary>
    public int TotalParticipants { get; set; }

    /// <summary>Totals row: average participation = total participants / (active students × competitions).</summary>
    public decimal TotalParticipationPercent { get; set; }

    /// <summary>Totals row: entries per rating.</summary>
    public IReadOnlyDictionary<Rating, int> TotalByRating { get; set; } = new Dictionary<Rating, int>();

    /// <summary>Totals row: entries without an evaluation.</summary>
    public int TotalNotGraded { get; set; }

    /// <summary>BR-12 list of the chosen competition, or null when none was chosen.</summary>
    public Rpt02NotSubmittedVm? NotSubmitted { get; set; }
}

/// <summary>Page model of /reports/rpt-02.</summary>
public class Rpt02PageVm
{
    /// <summary>Filters as entered.</summary>
    public Rpt02FilterVm Filter { get; set; } = new();

    /// <summary>Competitions for the "not submitted" dropdown.</summary>
    public IReadOnlyList<CompetitionOptionVm> Competitions { get; set; } = Array.Empty<CompetitionOptionVm>();

    /// <summary>Report result; null when the filters are invalid.</summary>
    public Rpt02ResultVm? Result { get; set; }
}
