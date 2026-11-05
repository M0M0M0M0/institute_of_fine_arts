using System.ComponentModel.DataAnnotations;
using InstituteOfFineArts.Web.Models.Enums;

namespace InstituteOfFineArts.Web.ViewModels.Staff.Submissions;

/// <summary>
/// Filters of the staff submission list /staff/submissions (F-M07-01): competition, evaluated or not, rating,
/// student name or code, submitted date range, page.
/// </summary>
public class SubmissionSearchVm
{
    /// <summary>Only entries of this competition (null = all competitions).</summary>
    [Display(Name = "Competition")]
    public long? CompetitionId { get; set; }

    /// <summary>true = evaluated only, false = not evaluated only, null = both.</summary>
    [Display(Name = "Evaluation")]
    public bool? Evaluated { get; set; }

    /// <summary>Only entries evaluated with this rating.</summary>
    [Display(Name = "Rating")]
    public Rating? Rating { get; set; }

    /// <summary>Part of the student's full name or student code.</summary>
    [StringLength(100)]
    [Display(Name = "Student name or code")]
    public string? Student { get; set; }

    /// <summary>Submitted on or after this day (from 00:00).</summary>
    [DataType(DataType.Date)]
    [Display(Name = "Submitted from")]
    public DateOnly? From { get; set; }

    /// <summary>Submitted on or before this day (until 23:59:59, TC-M10-02).</summary>
    [DataType(DataType.Date)]
    [Display(Name = "Submitted to")]
    public DateOnly? To { get; set; }

    /// <summary>Page number (1-based).</summary>
    public int Page { get; set; } = 1;
}

/// <summary>
/// Filters of the rating search /staff/submissions/by-rating (F-M07-06): rating, optional competition, page.
/// </summary>
public class RatingSearchVm
{
    /// <summary>Rating to look for (BEST when not given).</summary>
    [Display(Name = "Rating")]
    public Rating? Rating { get; set; }

    /// <summary>Only entries of this competition (null = every CLOSED or GRADED competition).</summary>
    [Display(Name = "Competition")]
    public long? CompetitionId { get; set; }

    /// <summary>Page number (1-based).</summary>
    public int Page { get; set; } = 1;
}

/// <summary>
/// One row of the staff submission list and of the rating search.
/// </summary>
public class SubmissionListItemVm
{
    /// <summary>Submission id (also used for /files/submissions/{id}/thumb).</summary>
    public long Id { get; set; }

    /// <summary>Title of the painting.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>student_profile.id of the author.</summary>
    public long StudentId { get; set; }

    /// <summary>Full name of the author.</summary>
    public string StudentName { get; set; } = string.Empty;

    /// <summary>Student code of the author.</summary>
    public string StudentCode { get; set; } = string.Empty;

    /// <summary>Competition id.</summary>
    public long CompetitionId { get; set; }

    /// <summary>Competition title.</summary>
    public string CompetitionTitle { get; set; } = string.Empty;

    /// <summary>Last day of the competition (deadline 23:59:59), used for the late flag.</summary>
    public DateOnly CompetitionEndDate { get; set; }

    /// <summary>When the entry was submitted.</summary>
    public DateTime SubmittedAt { get; set; }

    /// <summary>True when submitted after the deadline (BR-14).</summary>
    public bool IsLate { get; set; }

    /// <summary>Rating, or null when not evaluated yet.</summary>
    public Rating? Rating { get; set; }

    /// <summary>Name of the award given to this entry, if any.</summary>
    public string? AwardName { get; set; }

    /// <summary>Number of exhibition entries of this painting (any status).</summary>
    public int ExhibitionEntryCount { get; set; }
}

/// <summary>
/// Evaluation progress of one competition (F-M07-09): "evaluated X / total Y".
/// </summary>
/// <param name="CompetitionId">Competition id</param>
/// <param name="CompetitionTitle">Competition title</param>
/// <param name="Status">Status calculated for today</param>
/// <param name="Evaluated">Entries with an evaluation</param>
/// <param name="Total">All entries</param>
public record EvaluationProgressVm(long CompetitionId, string CompetitionTitle, CompetitionStatus Status, int Evaluated, int Total);

/// <summary>
/// One option of a competition dropdown.
/// </summary>
/// <param name="Id">Competition id</param>
/// <param name="Code">Code, e.g. CT2026-03</param>
/// <param name="Title">Title</param>
public record CompetitionOptionVm(long Id, string Code, string Title);
