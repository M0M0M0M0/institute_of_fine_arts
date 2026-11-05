using System.ComponentModel.DataAnnotations;
using System.Globalization;
using InstituteOfFineArts.Web.Models.Enums;

namespace InstituteOfFineArts.Web.ViewModels.Student.Submissions;

/// <summary>
/// Filters of "My entries" /student/submissions (F-M06-05, SCR-44): competition, title, submitted date range,
/// rating (only of published results, BR-24), page.
/// </summary>
public class MySubmissionSearchVm
{
    /// <summary>Only entries of this competition.</summary>
    [Display(Name = "Competition")]
    public long? CompetitionId { get; set; }

    /// <summary>Part of the painting title.</summary>
    [StringLength(100)]
    [Display(Name = "Title")]
    public string? Title { get; set; }

    /// <summary>Submitted on or after this day.</summary>
    [DataType(DataType.Date)]
    [Display(Name = "Submitted from")]
    public DateOnly? From { get; set; }

    /// <summary>Submitted on or before this day.</summary>
    [DataType(DataType.Date)]
    [Display(Name = "Submitted to")]
    public DateOnly? To { get; set; }

    /// <summary>Only entries with this rating; results that are not published yet never match (BR-24).</summary>
    [Display(Name = "Rating")]
    public Rating? Rating { get; set; }

    /// <summary>Page number (1-based).</summary>
    public int Page { get; set; } = 1;
}

/// <summary>A competition the student has entered (options of the competition filter).</summary>
/// <param name="Id">Competition id</param>
/// <param name="Title">Competition title</param>
public record MyCompetitionOptionVm(long Id, string Title);

/// <summary>One row of "My entries".</summary>
public class MySubmissionListItemVm
{
    /// <summary>Submission id.</summary>
    public long Id { get; set; }

    /// <summary>Painting title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>True when a thumbnail exists (/files/submissions/{id}/thumb).</summary>
    public bool HasThumbnail { get; set; }

    /// <summary>Competition id.</summary>
    public long CompetitionId { get; set; }

    /// <summary>Competition title.</summary>
    public string CompetitionTitle { get; set; } = string.Empty;

    /// <summary>First day of the competition (used to calculate the status).</summary>
    public DateOnly StartDate { get; set; }

    /// <summary>Last day to submit / edit (deadline 23:59:59).</summary>
    public DateOnly EndDate { get; set; }

    /// <summary>Competition status calculated for today (stored value until it is resolved).</summary>
    public CompetitionStatus CompetitionStatus { get; set; }

    /// <summary>Submission time.</summary>
    public DateTime SubmittedAt { get; set; }

    /// <summary>Rating, only when the competition is GRADED (BR-24); otherwise null.</summary>
    public Rating? Rating { get; set; }

    /// <summary>True while the competition is ONGOING, so the entry can still be edited or deleted (BR-13).</summary>
    public bool CanEdit { get; set; }
}

/// <summary>
/// Details of the signed-in student's own entry (F-M06-06, F-M06-07, SCR-45): image, texts, competition, deadline and
/// the evaluation once results are published (BR-24).
/// </summary>
public class MySubmissionDetailsVm
{
    /// <summary>Submission id.</summary>
    public long Id { get; set; }

    /// <summary>Painting title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Original file name (shown next to the download link).</summary>
    public string OriginalFileName { get; set; } = string.Empty;

    /// <summary>File size in bytes.</summary>
    public long FileSize { get; set; }

    /// <summary>Type of the accompanying text.</summary>
    public TextType TextType { get; set; }

    /// <summary>Poem / quotation / story.</summary>
    public string TextContent { get; set; } = string.Empty;

    /// <summary>Description.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Submission time.</summary>
    public DateTime SubmittedAt { get; set; }

    /// <summary>Last change, or null when never edited.</summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>Competition id.</summary>
    public long CompetitionId { get; set; }

    /// <summary>Competition code.</summary>
    public string CompetitionCode { get; set; } = string.Empty;

    /// <summary>Competition title.</summary>
    public string CompetitionTitle { get; set; } = string.Empty;

    /// <summary>Last day to submit / edit (deadline 23:59:59).</summary>
    public DateOnly EndDate { get; set; }

    /// <summary>Competition status calculated for today.</summary>
    public CompetitionStatus CompetitionStatus { get; set; }

    /// <summary>Edit / replace image / delete allowed: the competition is ONGOING (BR-13).</summary>
    public bool CanEdit { get; set; }

    /// <summary>Deadline as ISO 8601 local time (end of the last day), for the countdown script: 2026-11-14T23:59:59.</summary>
    public string DeadlineIso => Helpers.CompetitionStatusResolver.SubmissionDeadline(EndDate).ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);

    /// <summary>The evaluation, only when the competition is GRADED (BR-24); otherwise null.</summary>
    public MyEvaluationVm? Evaluation { get; set; }
}

/// <summary>Evaluation of the student's own entry, visible after results are published (BR-24).</summary>
/// <param name="Rating">Rating</param>
/// <param name="PositivePoints">Strengths</param>
/// <param name="NegativePoints">Weaknesses (reason when disqualified)</param>
/// <param name="ImprovementAreas">Areas of improvement</param>
/// <param name="EvaluatedAt">Grading time</param>
public record MyEvaluationVm(Rating Rating, string? PositivePoints, string? NegativePoints, string? ImprovementAreas, DateTime EvaluatedAt);
