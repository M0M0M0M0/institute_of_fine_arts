namespace InstituteOfFineArts.Web.ViewModels.Staff.Competitions;

/// <summary>One criterion row of the competition form (CompetitionFormVm.Criteria). Empty rows are ignored.</summary>
public class CompetitionCriterionRowVm
{
    /// <summary>grading_criterion.id (0 = row left empty).</summary>
    public long CriterionId { get; set; }

    /// <summary>Weight in percent; the weights of a competition add up to 100.</summary>
    public decimal? Weight { get; set; }
}

/// <summary>A teacher that can be put on the jury.</summary>
/// <param name="StaffId">staff_profile.id</param>
/// <param name="Name">Full name</param>
/// <param name="Subjects">Subjects taught (helps choosing)</param>
/// <param name="HasGraded">Already graded entries of this competition: cannot be removed from the jury</param>
public record JudgeOptionVm(long StaffId, string Name, string? Subjects, bool HasGraded);

/// <summary>A criterion of the library that can be chosen.</summary>
/// <param name="Id">grading_criterion.id</param>
/// <param name="Name">Name</param>
/// <param name="Description">What the judges look at</param>
/// <param name="Active">False = hidden in the library (still listed when the competition already uses it)</param>
public record CriterionOptionVm(long Id, string Name, string? Description, bool Active);

/// <summary>A jury member shown on the competition details page.</summary>
/// <param name="Name">Full name</param>
/// <param name="IsChair">Chair of the jury</param>
/// <param name="Graded">Entries this judge has graded</param>
public record JudgeRowVm(string Name, bool IsChair, int Graded);

/// <summary>A criterion shown on the competition details page.</summary>
/// <param name="Name">Name</param>
/// <param name="Description">What the judges look at</param>
/// <param name="Weight">Weight in percent</param>
public record CriterionRowVm(string Name, string? Description, decimal Weight);

/// <summary>
/// One line of the results preview / published ranking: the entry, the jury's average, its place and the award the
/// scores give it (computed by the system; staff only publish).
/// </summary>
/// <param name="SubmissionId">Entry</param>
/// <param name="Title">Entry title</param>
/// <param name="StudentName">Student</param>
/// <param name="Average">Jury average (2 decimals for display), null when disqualified</param>
/// <param name="Rating">Rating from the thresholds</param>
/// <param name="Place">Dense place (1, 1, 2…), null when disqualified</param>
/// <param name="AwardName">Award won, null when none</param>
public record RankingRowVm(long SubmissionId, string Title, string StudentName, decimal? Average, Models.Enums.Rating? Rating,
    int? Place, string? AwardName);
