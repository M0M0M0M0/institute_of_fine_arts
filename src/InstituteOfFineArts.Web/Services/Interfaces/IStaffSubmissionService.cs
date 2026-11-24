using InstituteOfFineArts.Web.ViewModels.Common;
using InstituteOfFineArts.Web.ViewModels.Staff.Submissions;

namespace InstituteOfFineArts.Web.Services.Interfaces;

/// <summary>
/// Read-only views of the submissions for staff (M07, tasks BE2-06, BE2-07): list with filters and evaluation
/// progress, details, rating search, study history of one student. Shared by the Staff and Teacher areas.
/// Unknown ids throw NotFoundException.
/// <para>
/// <c>judgeId</c> = the Teacher view: only entries of the competitions that teacher judges, and only once the
/// submission period is over (CLOSED or GRADED), so a painting cannot leak before the deadline. Any other entry is
/// reported as not found.
/// Staff see every entry, also while the competition is open (support for the students).
/// </para>
/// </summary>
public interface IStaffSubmissionService
{
    /// <summary>Searches every submission by competition, evaluated or not, rating, student and date range; newest first.</summary>
    Task<PagedResult<SubmissionListItemVm>> SearchAsync(SubmissionSearchVm search, int pageSize = 10, long? judgeId = null);

    /// <summary>All competitions (for a judge: the CLOSED / GRADED ones they judge) for the filter dropdown, newest start first.</summary>
    Task<IReadOnlyList<CompetitionOptionVm>> GetCompetitionOptionsAsync(long? judgeId = null);

    /// <summary>"Evaluated X / total Y" of one competition (F-M07-09); null when the competition does not exist.</summary>
    Task<EvaluationProgressVm?> GetProgressAsync(long competitionId);

    /// <summary>Details with evaluation, award, exhibition entries and the late flag.</summary>
    /// <param name="id">Submission id</param>
    /// <param name="judgeId">Teacher view: NotFoundException unless they judge the competition and it is closed for entries</param>
    Task<SubmissionDetailsVm> GetDetailsAsync(long id, long? judgeId = null);

    /// <summary>Entries with the given rating (BEST when none) in CLOSED or GRADED competitions (F-M07-06).</summary>
    Task<PagedResult<SubmissionListItemVm>> SearchByRatingAsync(RatingSearchVm search, int pageSize = 10);

    /// <summary>CLOSED and GRADED competitions (the only ones with evaluations) for the rating search dropdown.</summary>
    Task<IReadOnlyList<CompetitionOptionVm>> GetEvaluatedCompetitionOptionsAsync();

    /// <summary>All entries of one student with rating, comments, award and exhibition entries (F-M07-07).</summary>
    /// <param name="studentId">student_profile.id</param>
    Task<StudentHistoryVm> GetStudentHistoryAsync(long studentId);
}
