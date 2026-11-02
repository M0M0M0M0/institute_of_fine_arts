namespace InstituteOfFineArts.Web.Models.Entities;

/// <summary>
/// Award issued to a submission (table award_issued).
/// </summary>
public class AwardIssued : BaseEntity
{
    /// <summary>Foreign key to the competition award.</summary>
    public long CompetitionAwardId { get; set; }

    /// <summary>Foreign key to the winning submission (unique ⇒ at most one award per student per competition).</summary>
    public long SubmissionId { get; set; }

    /// <summary>Id of the staff profile that issued the award.</summary>
    public long IssuedBy { get; set; }

    /// <summary>Date the award was issued.</summary>
    public DateOnly IssuedDate { get; set; }

    /// <summary>Remarks about the award / the competition.</summary>
    public string? Remarks { get; set; }

    /// <summary>Competition award.</summary>
    public CompetitionAward CompetitionAward { get; set; } = null!;

    /// <summary>Winning submission.</summary>
    public Submission Submission { get; set; } = null!;

    /// <summary>Staff member who issued the award.</summary>
    public StaffProfile IssuedByStaff { get; set; } = null!;
}
