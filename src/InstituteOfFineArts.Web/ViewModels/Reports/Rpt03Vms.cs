using System.ComponentModel.DataAnnotations;
using InstituteOfFineArts.Web.ViewModels.Staff.Submissions;

namespace InstituteOfFineArts.Web.ViewModels.Reports;

/// <summary>Filters of RPT-03 "Awards issued" (F-M10-03): competition, issued date range, student.</summary>
public class Rpt03FilterVm
{
    /// <summary>Only awards of this competition (null = all).</summary>
    [Display(Name = "Competition")]
    public long? CompetitionId { get; set; }

    /// <summary>Issued on or after this day.</summary>
    [DataType(DataType.Date)]
    [Display(Name = "Issued from")]
    public DateOnly? From { get; set; }

    /// <summary>Issued on or before this day.</summary>
    [DataType(DataType.Date)]
    [Display(Name = "Issued to")]
    public DateOnly? To { get; set; }

    /// <summary>Part of the student's full name or student code.</summary>
    [StringLength(100)]
    [Display(Name = "Student name or code")]
    public string? Student { get; set; }
}

/// <summary>One issued award of RPT-03.</summary>
public class Rpt03RowVm
{
    /// <summary>Row number.</summary>
    public int No { get; set; }

    /// <summary>Competition code.</summary>
    public string CompetitionCode { get; set; } = string.Empty;

    /// <summary>Competition title.</summary>
    public string CompetitionTitle { get; set; } = string.Empty;

    /// <summary>Award name.</summary>
    public string AwardName { get; set; } = string.Empty;

    /// <summary>Rank (1 = highest).</summary>
    public int Rank { get; set; }

    /// <summary>Prize value in USD.</summary>
    public decimal PrizeValue { get; set; }

    /// <summary>Student code.</summary>
    public string StudentCode { get; set; } = string.Empty;

    /// <summary>Student full name.</summary>
    public string StudentName { get; set; } = string.Empty;

    /// <summary>Submission id of the painting.</summary>
    public long SubmissionId { get; set; }

    /// <summary>Title of the painting.</summary>
    public string SubmissionTitle { get; set; } = string.Empty;

    /// <summary>Issued date.</summary>
    public DateOnly IssuedDate { get; set; }

    /// <summary>Staff member who recorded the award.</summary>
    public string IssuedByName { get; set; } = string.Empty;

    /// <summary>Remarks.</summary>
    public string? Remarks { get; set; }
}

/// <summary>Result of RPT-03 with the number of awards and the total prize value (Financial).</summary>
public class Rpt03ResultVm
{
    /// <summary>Rows, newest issued first.</summary>
    public IReadOnlyList<Rpt03RowVm> Rows { get; set; } = Array.Empty<Rpt03RowVm>();

    /// <summary>Number of matching awards.</summary>
    public int TotalCount { get; set; }

    /// <summary>True when more awards matched than the report shows.</summary>
    public bool IsTruncated => TotalCount > Rows.Count;

    /// <summary>Sum of the prize values of every matching award.</summary>
    public decimal TotalPrizeValue { get; set; }
}

/// <summary>Page model of /reports/rpt-03.</summary>
public class Rpt03PageVm
{
    /// <summary>Filters as entered.</summary>
    public Rpt03FilterVm Filter { get; set; } = new();

    /// <summary>Competitions for the dropdown.</summary>
    public IReadOnlyList<CompetitionOptionVm> Competitions { get; set; } = Array.Empty<CompetitionOptionVm>();

    /// <summary>Report result; null when the filters are invalid.</summary>
    public Rpt03ResultVm? Result { get; set; }
}
