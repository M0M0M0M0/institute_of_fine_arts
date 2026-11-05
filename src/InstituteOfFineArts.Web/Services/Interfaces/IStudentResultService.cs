using InstituteOfFineArts.Web.ViewModels.Common;
using InstituteOfFineArts.Web.ViewModels.Student.Results;

namespace InstituteOfFineArts.Web.Services.Interfaces;

/// <summary>
/// Results pages of the Student area (task BE2-08; F-M06-07..09, F-M08-04 pub; SCR-46, SCR-47): the student's own
/// results, the BEST works of every student and the issued awards. Read only. An evaluation or award is never
/// returned before its competition is GRADED (BR-24).
/// </summary>
public interface IStudentResultService
{
    /// <summary>
    /// Every entry of the student, newest first, with rating, comments and award once the competition is GRADED
    /// (F-M06-07, US-10 AC2/AC3). The rating filter only matches published results (BR-24).
    /// </summary>
    /// <param name="studentProfileId">student_profile.id of the signed-in student</param>
    /// <param name="search">Filters</param>
    /// <param name="pageSize">Rows per page</param>
    Task<PagedResult<MyResultItemVm>> SearchMyResultsAsync(long studentProfileId, MyResultSearchVm search, int pageSize = 10);

    /// <summary>BEST entries of all GRADED competitions with the staff comments (F-M06-08, US-10 AC4).</summary>
    /// <param name="search">Filters</param>
    /// <param name="pageSize">Rows per page</param>
    Task<PagedResult<BestWorkItemVm>> SearchBestWorksAsync(BestWorkSearchVm search, int pageSize = 10);

    /// <summary>Awards issued in GRADED competitions (F-M06-09, F-M08-04 pub, US-11 AC4).</summary>
    /// <param name="search">Filters</param>
    /// <param name="pageSize">Rows per page</param>
    Task<PagedResult<IssuedAwardItemVm>> SearchAwardsAsync(IssuedAwardSearchVm search, int pageSize = 10);

    /// <summary>GRADED competitions, most recently published first (options of the competition filters).</summary>
    Task<List<GradedCompetitionOptionVm>> GetGradedCompetitionsAsync();
}
