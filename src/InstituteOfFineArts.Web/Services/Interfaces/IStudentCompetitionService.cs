using InstituteOfFineArts.Web.ViewModels.Student.Competitions;

namespace InstituteOfFineArts.Web.Services.Interfaces;

/// <summary>
/// Competitions as seen by a student (F-M05-09, task BE1-06): read-only queries, status always calculated
/// with CompetitionStatusResolver. Unknown ids throw NotFoundException.
/// </summary>
public interface IStudentCompetitionService
{
    /// <summary>UPCOMING and ONGOING competitions (ongoing first, nearest deadline first) with award summary and own-entry flag.</summary>
    /// <param name="studentProfileId">student_profile.id of the signed-in student</param>
    Task<List<StudentCompetitionListItemVm>> GetOpenCompetitionsAsync(long studentProfileId);

    /// <summary>
    /// Details with rules, awards and the student's own entry. Closed / graded competitions can also be opened
    /// (read only, no submit link), so links from results or old bookmarks keep working.
    /// </summary>
    /// <param name="competitionId">Competition id</param>
    /// <param name="studentProfileId">student_profile.id of the signed-in student</param>
    Task<StudentCompetitionDetailsVm> GetDetailsAsync(long competitionId, long studentProfileId);
}
