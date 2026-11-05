using InstituteOfFineArts.Web.ViewModels.Reports;

namespace InstituteOfFineArts.Web.Services.Interfaces;

/// <summary>
/// Competition reports of M10 (task BE2-12 part 1; BA doc §7): RPT-01 submissions by rating, RPT-02 participation
/// and ratings per competition, RPT-03 awards issued, RPT-06 student achievement profile. Read only; invalid filters
/// (From after To, no competition / student) throw BusinessException. Results are not paged (they are printed) but
/// capped at <c>maxRows</c> rows; totals always cover the whole result.
/// </summary>
public interface ICompetitionReportService
{
    /// <summary>RPT-01: entries of one competition submitted in the date range with the chosen ratings / class.</summary>
    /// <param name="filter">Filters</param>
    /// <param name="maxRows">Row cap</param>
    Task<Rpt01ResultVm> GetSubmissionsByRatingAsync(Rpt01FilterVm filter, int maxRows = 1000);

    /// <summary>RPT-02: per competition counts, participation of active students and the BR-12 non-submitter list.</summary>
    /// <param name="filter">Filters</param>
    /// <param name="today">Day used for the competition status (default: today)</param>
    /// <param name="maxRows">Row cap</param>
    Task<Rpt02ResultVm> GetParticipationAsync(Rpt02FilterVm filter, DateOnly? today = null, int maxRows = 1000);

    /// <summary>RPT-03: issued awards with the number of awards and the total prize value.</summary>
    /// <param name="filter">Filters</param>
    /// <param name="maxRows">Row cap</param>
    Task<Rpt03ResultVm> GetAwardsIssuedAsync(Rpt03FilterVm filter, int maxRows = 1000);

    /// <summary>RPT-06: entries of one student with ratings, awards, exhibitions and the achievement totals.</summary>
    /// <param name="filter">Filters</param>
    /// <param name="maxRows">Row cap</param>
    Task<Rpt06ResultVm> GetStudentProfileAsync(Rpt06FilterVm filter, int maxRows = 1000);

    /// <summary>Distinct class names of the students, for the RPT-01 class dropdown.</summary>
    Task<IReadOnlyList<string>> GetClassOptionsAsync();

    /// <summary>Every student ordered by student code, for the RPT-06 dropdown.</summary>
    Task<IReadOnlyList<StudentOptionVm>> GetStudentOptionsAsync();
}
