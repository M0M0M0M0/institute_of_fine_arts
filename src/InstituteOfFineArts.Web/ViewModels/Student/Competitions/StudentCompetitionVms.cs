using InstituteOfFineArts.Web.Models.Enums;

namespace InstituteOfFineArts.Web.ViewModels.Student.Competitions;

/// <summary>
/// One competition in the student list /student/competitions (SCR-41, F-M05-09): upcoming or ongoing only.
/// </summary>
public class StudentCompetitionListItemVm
{
    /// <summary>Competition id.</summary>
    public long Id { get; set; }

    /// <summary>Code, e.g. CT2026-03.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>First day to submit.</summary>
    public DateOnly StartDate { get; set; }

    /// <summary>Last day to submit (deadline 23:59:59).</summary>
    public DateOnly EndDate { get; set; }

    /// <summary>Status calculated for today (UPCOMING or ONGOING).</summary>
    public CompetitionStatus Status { get; set; }

    /// <summary>True when a cover image exists (/files/competitions/{id}/cover).</summary>
    public bool HasCover { get; set; }

    /// <summary>Number of defined awards.</summary>
    public int AwardCount { get; set; }

    /// <summary>Highest prize value (USD), 0 when there are no awards.</summary>
    public decimal TopPrize { get; set; }

    /// <summary>True when the signed-in student has already submitted an entry.</summary>
    public bool HasSubmitted { get; set; }
}

/// <summary>
/// Competition details for a student (SCR-42, F-M05-09): description, rules, awards, deadline and own entry.
/// </summary>
public class StudentCompetitionDetailsVm
{
    /// <summary>Competition id.</summary>
    public long Id { get; set; }

    /// <summary>Code.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Description / theme.</summary>
    public string? Description { get; set; }

    /// <summary>Rules and conditions.</summary>
    public string? Rules { get; set; }

    /// <summary>First day to submit.</summary>
    public DateOnly StartDate { get; set; }

    /// <summary>Last day to submit (deadline 23:59:59).</summary>
    public DateOnly EndDate { get; set; }

    /// <summary>Status calculated for today.</summary>
    public CompetitionStatus Status { get; set; }

    /// <summary>True when a cover image exists.</summary>
    public bool HasCover { get; set; }

    /// <summary>Defined awards, best rank first.</summary>
    public List<StudentAwardVm> Awards { get; set; } = new();

    /// <summary>The signed-in student's entry, or null when not submitted.</summary>
    public StudentOwnSubmissionVm? MySubmission { get; set; }

    /// <summary>"Submit an entry" is shown: the competition is ONGOING and the student has not submitted yet (BR-13, BR-15).</summary>
    public bool CanSubmit { get; set; }
}

/// <summary>One defined award as shown to students.</summary>
/// <param name="Name">Award name</param>
/// <param name="Rank">1 = highest</param>
/// <param name="PrizeValue">Prize in USD</param>
/// <param name="Quantity">How many can be given</param>
/// <param name="Description">Prize description</param>
public record StudentAwardVm(string Name, int Rank, decimal PrizeValue, int Quantity, string? Description);

/// <summary>The signed-in student's own entry in a competition.</summary>
/// <param name="Id">Submission id</param>
/// <param name="Title">Painting title</param>
/// <param name="SubmittedAt">When it was submitted</param>
public record StudentOwnSubmissionVm(long Id, string Title, DateTime SubmittedAt);
