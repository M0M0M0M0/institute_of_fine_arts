namespace InstituteOfFineArts.Web.Models.Entities;

/// <summary>
/// One teacher on the jury of one competition (table competition_judge). Only jury members grade the entries of the
/// competition; every member grades every entry on their own, and the entry's result is the average of the jury.
/// </summary>
public class CompetitionJudge : BaseEntity
{
    /// <summary>Competition.</summary>
    public long CompetitionId { get; set; }

    /// <summary>staff_profile.id of the teacher.</summary>
    public long JudgeId { get; set; }

    /// <summary>Chair of the jury (at most one per competition; shown first).</summary>
    public bool IsChair { get; set; }

    /// <summary>Competition.</summary>
    public Competition Competition { get; set; } = null!;

    /// <summary>The teacher.</summary>
    public StaffProfile Judge { get; set; } = null!;
}
