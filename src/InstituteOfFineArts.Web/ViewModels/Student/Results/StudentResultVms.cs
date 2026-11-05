using System.ComponentModel.DataAnnotations;
using InstituteOfFineArts.Web.Models.Enums;

namespace InstituteOfFineArts.Web.ViewModels.Student.Results;

/// <summary>
/// Filters of "My Results" /student/results (F-M06-07, US-10 AC2/AC3): competition, rating (only of published
/// results, BR-24), page.
/// </summary>
public class MyResultSearchVm
{
    /// <summary>Only entries of this competition.</summary>
    [Display(Name = "Competition")]
    public long? CompetitionId { get; set; }

    /// <summary>Only entries with this rating; results that are not published yet never match (BR-24).</summary>
    [Display(Name = "Rating")]
    public Rating? Rating { get; set; }

    /// <summary>Page number (1-based).</summary>
    public int Page { get; set; } = 1;
}

/// <summary>
/// One entry of "My Results". The evaluation and award fields are filled only when the competition is GRADED
/// (BR-24); before that they stay null and the page shows "Awaiting results".
/// </summary>
public class MyResultItemVm
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

    /// <summary>Last day of the competition (used to calculate the status).</summary>
    public DateOnly EndDate { get; set; }

    /// <summary>Competition status calculated for today (stored value until it is resolved).</summary>
    public CompetitionStatus CompetitionStatus { get; set; }

    /// <summary>Submission time.</summary>
    public DateTime SubmittedAt { get; set; }

    /// <summary>Rating, only when the competition is GRADED (BR-24).</summary>
    public Rating? Rating { get; set; }

    /// <summary>Strengths, only when the competition is GRADED (BR-24).</summary>
    public string? PositivePoints { get; set; }

    /// <summary>Weaknesses (reason when disqualified), only when the competition is GRADED (BR-24).</summary>
    public string? NegativePoints { get; set; }

    /// <summary>Areas of improvement, only when the competition is GRADED (BR-24).</summary>
    public string? ImprovementAreas { get; set; }

    /// <summary>Award won by this entry, only when the competition is GRADED (BR-24); null when none.</summary>
    public string? AwardName { get; set; }

    /// <summary>Rank of the award (1 = highest), only with <see cref="AwardName"/>.</summary>
    public int? AwardRank { get; set; }

    /// <summary>Prize value in USD, only with <see cref="AwardName"/>.</summary>
    public decimal? PrizeValue { get; set; }

    /// <summary>True when the results of the competition are published (GRADED).</summary>
    public bool IsPublished => CompetitionStatus == CompetitionStatus.GRADED;
}

/// <summary>Filters of "Best Works" /student/best (F-M06-08, SCR-46): competition, page.</summary>
public class BestWorkSearchVm
{
    /// <summary>Only BEST entries of this (GRADED) competition.</summary>
    [Display(Name = "Competition")]
    public long? CompetitionId { get; set; }

    /// <summary>Page number (1-based).</summary>
    public int Page { get; set; } = 1;
}

/// <summary>One BEST entry of a GRADED competition with the staff comments (F-M06-08, US-10 AC4).</summary>
public class BestWorkItemVm
{
    /// <summary>Submission id.</summary>
    public long Id { get; set; }

    /// <summary>Painting title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>True when a thumbnail exists (/files/submissions/{id}/thumb).</summary>
    public bool HasThumbnail { get; set; }

    /// <summary>Full name of the student.</summary>
    public string StudentName { get; set; } = string.Empty;

    /// <summary>Competition id.</summary>
    public long CompetitionId { get; set; }

    /// <summary>Competition title.</summary>
    public string CompetitionTitle { get; set; } = string.Empty;

    /// <summary>Strengths written by staff.</summary>
    public string? PositivePoints { get; set; }

    /// <summary>Weaknesses written by staff.</summary>
    public string? NegativePoints { get; set; }

    /// <summary>Areas of improvement written by staff.</summary>
    public string? ImprovementAreas { get; set; }

    /// <summary>Award won by the entry, or null.</summary>
    public string? AwardName { get; set; }
}

/// <summary>
/// Filters of the issued awards /student/awards (F-M06-09, F-M08-04 pub, SCR-47): competition, student name,
/// award name, issued date range, page.
/// </summary>
public class IssuedAwardSearchVm
{
    /// <summary>Only awards of this (GRADED) competition.</summary>
    [Display(Name = "Competition")]
    public long? CompetitionId { get; set; }

    /// <summary>Part of the student's full name.</summary>
    [StringLength(100)]
    [Display(Name = "Student")]
    public string? StudentName { get; set; }

    /// <summary>Part of the award name.</summary>
    [StringLength(100)]
    [Display(Name = "Award")]
    public string? Award { get; set; }

    /// <summary>Issued on or after this day.</summary>
    [DataType(DataType.Date)]
    [Display(Name = "Issued from")]
    public DateOnly? From { get; set; }

    /// <summary>Issued on or before this day.</summary>
    [DataType(DataType.Date)]
    [Display(Name = "Issued to")]
    public DateOnly? To { get; set; }

    /// <summary>Page number (1-based).</summary>
    public int Page { get; set; } = 1;
}

/// <summary>One award issued in a GRADED competition (public data: no evaluation, no submission text).</summary>
public class IssuedAwardItemVm
{
    /// <summary>award_issued.id.</summary>
    public long Id { get; set; }

    /// <summary>Competition id.</summary>
    public long CompetitionId { get; set; }

    /// <summary>Competition title.</summary>
    public string CompetitionTitle { get; set; } = string.Empty;

    /// <summary>Award name.</summary>
    public string AwardName { get; set; } = string.Empty;

    /// <summary>Rank of the award (1 = highest).</summary>
    public int Rank { get; set; }

    /// <summary>Prize value in USD.</summary>
    public decimal PrizeValue { get; set; }

    /// <summary>Prize description (gift, certificate…).</summary>
    public string? PrizeDescription { get; set; }

    /// <summary>Full name of the winning student.</summary>
    public string StudentName { get; set; } = string.Empty;

    /// <summary>Winning submission id (its image is public once GRADED, BR-20).</summary>
    public long SubmissionId { get; set; }

    /// <summary>Title of the winning painting.</summary>
    public string ArtworkTitle { get; set; } = string.Empty;

    /// <summary>True when a thumbnail exists.</summary>
    public bool HasThumbnail { get; set; }

    /// <summary>Date the award was issued.</summary>
    public DateOnly IssuedDate { get; set; }

    /// <summary>Remarks of staff about the award (TC-M08-05).</summary>
    public string? Remarks { get; set; }
}

/// <summary>A GRADED competition (options of the competition filters on the Best Works and Awards pages).</summary>
/// <param name="Id">Competition id</param>
/// <param name="Title">Competition title</param>
public record GradedCompetitionOptionVm(long Id, string Title);
