namespace InstituteOfFineArts.Web.ViewModels.Teacher;

/// <summary>Home page of the Teacher area (/teacher): what is waiting to be graded and what closes soon.</summary>
public class TeacherDashboardVm
{
    /// <summary>CLOSED competitions (submission period over, results not published) with their grading progress, entries left to grade first.</summary>
    public List<GradingCompetitionVm> ToGrade { get; set; } = [];

    /// <summary>ONGOING competitions closing soonest first (no entry is visible until they close).</summary>
    public List<ClosingCompetitionVm> ClosingSoon { get; set; } = [];

    /// <summary>Entries left to grade over every closed competition.</summary>
    public int EntriesLeft => ToGrade.Sum(c => c.Total - c.Evaluated);
}

/// <summary>One closed competition and how far the grading is.</summary>
/// <param name="Id">Competition id</param>
/// <param name="Code">Competition code</param>
/// <param name="Title">Title</param>
/// <param name="EndDate">Last day for entries</param>
/// <param name="Total">Entries</param>
/// <param name="Evaluated">Entries already graded</param>
public record GradingCompetitionVm(long Id, string Code, string Title, DateOnly EndDate, int Total, int Evaluated)
{
    /// <summary>Entries still to grade.</summary>
    public int Left => Total - Evaluated;
}

/// <summary>One competition still taking entries.</summary>
/// <param name="Id">Competition id</param>
/// <param name="Title">Title</param>
/// <param name="EndDate">Last day for entries: grading opens the day after</param>
public record ClosingCompetitionVm(long Id, string Title, DateOnly EndDate);
