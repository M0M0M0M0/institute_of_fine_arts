using InstituteOfFineArts.Web.ViewModels.Teacher;

namespace InstituteOfFineArts.Web.Services.Interfaces;

/// <summary>Teacher dashboard (/teacher): closed competitions with their grading progress and the ones closing soon.</summary>
public interface ITeacherDashboardService
{
    /// <summary>Builds the dashboard.</summary>
    /// <param name="judgeId">staff_profile.id of the teacher: only the competitions they judge</param>
    /// <param name="today">Current date (default: today in the institute time zone); tests pass a fixed date</param>
    Task<TeacherDashboardVm> GetAsync(long judgeId, DateOnly? today = null);
}
