using InstituteOfFineArts.Web.ViewModels.Common;
using InstituteOfFineArts.Web.ViewModels.Staff.Submissions;

namespace InstituteOfFineArts.Web.Services.Interfaces;

/// <summary>
/// Read-only views of the submissions for staff (M07, tasks BE2-06, BE2-07): list with filters and evaluation
/// progress, details, rating search, study history of one student. Not tied to the Staff area, so the Manager
/// area can reuse it (BE2-13). Unknown ids throw NotFoundException.
/// </summary>
public interface IStaffSubmissionService
{
    /// <summary>Searches every submission by competition, evaluated or not, rating, student and date range; newest first.</summary>
    Task<PagedResult<SubmissionListItemVm>> SearchAsync(SubmissionSearchVm search, int pageSize = 10);

    /// <summary>All competitions for the filter dropdown, newest start first.</summary>
    Task<IReadOnlyList<CompetitionOptionVm>> GetCompetitionOptionsAsync();

    /// <summary>"Evaluated X / total Y" of one competition (F-M07-09); null when the competition does not exist.</summary>
    Task<EvaluationProgressVm?> GetProgressAsync(long competitionId);

    /// <summary>Details with evaluation, award, exhibition entries and the late flag.</summary>
    /// <param name="id">Submission id</param>
    Task<SubmissionDetailsVm> GetDetailsAsync(long id);

    /// <summary>Entries with the given rating (BEST when none) in CLOSED or GRADED competitions (F-M07-06).</summary>
    Task<PagedResult<SubmissionListItemVm>> SearchByRatingAsync(RatingSearchVm search, int pageSize = 10);

    /// <summary>CLOSED and GRADED competitions (the only ones with evaluations) for the rating search dropdown.</summary>
    Task<IReadOnlyList<CompetitionOptionVm>> GetEvaluatedCompetitionOptionsAsync();

    /// <summary>All entries of one student with rating, comments, award and exhibition entries (F-M07-07).</summary>
    /// <param name="studentId">student_profile.id</param>
    Task<StudentHistoryVm> GetStudentHistoryAsync(long studentId);
}
