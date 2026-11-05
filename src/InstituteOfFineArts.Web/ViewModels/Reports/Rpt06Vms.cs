using System.ComponentModel.DataAnnotations;
using InstituteOfFineArts.Web.Models.Enums;

namespace InstituteOfFineArts.Web.ViewModels.Reports;

/// <summary>Filters of RPT-06 "Student achievement profile" (F-M10-06): student (required) and submitted date range.</summary>
public class Rpt06FilterVm
{
    /// <summary>student_profile.id (required).</summary>
    [Required(ErrorMessage = "Please choose a student.")]
    [Display(Name = "Student")]
    public long? StudentId { get; set; }

    /// <summary>Entries submitted on or after this day.</summary>
    [DataType(DataType.Date)]
    [Display(Name = "Submitted from")]
    public DateOnly? From { get; set; }

    /// <summary>Entries submitted on or before this day (whole day included).</summary>
    [DataType(DataType.Date)]
    [Display(Name = "Submitted to")]
    public DateOnly? To { get; set; }
}

/// <summary>Student of the RPT-06 dropdown.</summary>
/// <param name="Id">student_profile.id</param>
/// <param name="Code">Student code</param>
/// <param name="FullName">Full name</param>
public record StudentOptionVm(long Id, string Code, string FullName);

/// <summary>One exhibition entry of a painting in RPT-06.</summary>
public class Rpt06ExhibitionVm
{
    /// <summary>Exhibition name.</summary>
    public string ExhibitionName { get; set; } = string.Empty;

    /// <summary>Entry status (SOLD = sold).</summary>
    public EntryStatus Status { get; set; }

    /// <summary>Sold price when sold.</summary>
    public decimal? SoldPrice { get; set; }
}

/// <summary>One entry of the student in RPT-06.</summary>
public class Rpt06RowVm
{
    /// <summary>Row number.</summary>
    public int No { get; set; }

    /// <summary>Submission id.</summary>
    public long SubmissionId { get; set; }

    /// <summary>Competition code.</summary>
    public string CompetitionCode { get; set; } = string.Empty;

    /// <summary>Competition title.</summary>
    public string CompetitionTitle { get; set; } = string.Empty;

    /// <summary>Title of the painting.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Submission time.</summary>
    public DateTime SubmittedAt { get; set; }

    /// <summary>Rating, or null when not graded.</summary>
    public Rating? Rating { get; set; }

    /// <summary>Short summary of the evaluation comments (positive points, cut to a fixed length).</summary>
    public string? CommentSummary { get; set; }

    /// <summary>Award name, or null.</summary>
    public string? AwardName { get; set; }

    /// <summary>Prize value of the award, or null.</summary>
    public decimal? PrizeValue { get; set; }

    /// <summary>Exhibition entries of the painting.</summary>
    public List<Rpt06ExhibitionVm> Exhibitions { get; set; } = new();
}

/// <summary>Result of RPT-06: the student, the entries and the achievement totals.</summary>
public class Rpt06ResultVm
{
    /// <summary>Student code.</summary>
    public string StudentCode { get; set; } = string.Empty;

    /// <summary>Full name.</summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>Class.</summary>
    public string? ClassName { get; set; }

    /// <summary>Course.</summary>
    public string CourseName { get; set; } = string.Empty;

    /// <summary>Entries, oldest submission first.</summary>
    public IReadOnlyList<Rpt06RowVm> Rows { get; set; } = Array.Empty<Rpt06RowVm>();

    /// <summary>Number of matching entries.</summary>
    public int TotalCount { get; set; }

    /// <summary>True when more entries matched than the report shows.</summary>
    public bool IsTruncated => TotalCount > Rows.Count;

    /// <summary>Number of competitions joined (distinct competitions of the matching entries).</summary>
    public int CompetitionsJoined { get; set; }

    /// <summary>Number of entries rated BEST.</summary>
    public int BestCount { get; set; }

    /// <summary>Number of awards received.</summary>
    public int AwardCount { get; set; }

    /// <summary>Sum of the prize values of those awards.</summary>
    public decimal TotalPrizeValue { get; set; }

    /// <summary>Sum of the sold prices of the student's paintings sold at exhibitions.</summary>
    public decimal TotalSales { get; set; }
}

/// <summary>Page model of /reports/rpt-06.</summary>
public class Rpt06PageVm
{
    /// <summary>Filters as entered.</summary>
    public Rpt06FilterVm Filter { get; set; } = new();

    /// <summary>Students for the dropdown.</summary>
    public IReadOnlyList<StudentOptionVm> Students { get; set; } = Array.Empty<StudentOptionVm>();

    /// <summary>Report result; null before the first run or when the filters are invalid.</summary>
    public Rpt06ResultVm? Result { get; set; }
}
