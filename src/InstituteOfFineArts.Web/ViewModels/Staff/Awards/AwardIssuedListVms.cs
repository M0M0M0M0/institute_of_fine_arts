using System.ComponentModel.DataAnnotations;

namespace InstituteOfFineArts.Web.ViewModels.Staff.Awards;

/// <summary>
/// Filters of the issued award list /staff/awards (F-M08-04): competition, student name or code, award name,
/// issued date range, page.
/// </summary>
public class AwardIssuedSearchVm
{
    /// <summary>Only awards of this competition (null = all competitions).</summary>
    [Display(Name = "Competition")]
    public long? CompetitionId { get; set; }

    /// <summary>Part of the student's full name or student code.</summary>
    [StringLength(100)]
    [Display(Name = "Student name or code")]
    public string? Student { get; set; }

    /// <summary>Part of the award name, e.g. "First".</summary>
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

/// <summary>
/// One row of the issued award list: competition, award, prize, student, artwork, issued date, remarks.
/// </summary>
public class AwardIssuedListItemVm
{
    /// <summary>award_issued.id.</summary>
    public long Id { get; set; }

    /// <summary>Competition id.</summary>
    public long CompetitionId { get; set; }

    /// <summary>Competition code.</summary>
    public string CompetitionCode { get; set; } = string.Empty;

    /// <summary>Competition title.</summary>
    public string CompetitionTitle { get; set; } = string.Empty;

    /// <summary>Award name, e.g. First Prize.</summary>
    public string AwardName { get; set; } = string.Empty;

    /// <summary>Award rank, 1 = highest.</summary>
    public int Rank { get; set; }

    /// <summary>Prize value in USD (CompetitionAward.PrizeValue, BR-33).</summary>
    public decimal PrizeValue { get; set; }

    /// <summary>student_profile.id of the winner.</summary>
    public long StudentId { get; set; }

    /// <summary>Full name of the winner.</summary>
    public string StudentName { get; set; } = string.Empty;

    /// <summary>Student code of the winner.</summary>
    public string StudentCode { get; set; } = string.Empty;

    /// <summary>Winning submission id (also used for /files/submissions/{id}/thumb).</summary>
    public long SubmissionId { get; set; }

    /// <summary>Title of the winning painting.</summary>
    public string SubmissionTitle { get; set; } = string.Empty;

    /// <summary>Date the award was issued.</summary>
    public DateOnly IssuedDate { get; set; }

    /// <summary>Remarks about the award / the competition.</summary>
    public string? Remarks { get; set; }

    /// <summary>Name of the staff member who issued the award.</summary>
    public string IssuedByName { get; set; } = string.Empty;
}
