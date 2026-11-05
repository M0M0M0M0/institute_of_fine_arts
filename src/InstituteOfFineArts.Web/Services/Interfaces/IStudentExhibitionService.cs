using InstituteOfFineArts.Web.ViewModels.Common;
using InstituteOfFineArts.Web.ViewModels.Staff.Exhibitions;
using InstituteOfFineArts.Web.ViewModels.Student.Exhibitions;

namespace InstituteOfFineArts.Web.Services.Interfaces;

/// <summary>
/// Exhibitions as seen by a student (task BE2-09; F-M09-02, F-M09-03): read-only queries, status always
/// calculated with CompetitionStatusResolver. Unknown ids throw NotFoundException.
/// </summary>
public interface IStudentExhibitionService
{
    /// <summary>Searches exhibitions (name, city, status, date range); UPCOMING first, then ONGOING, then FINISHED.</summary>
    /// <param name="search">Filters</param>
    /// <param name="studentProfileId">student_profile.id of the signed-in student (to count own entries)</param>
    /// <param name="pageSize">Rows per page</param>
    Task<PagedResult<StudentExhibitionListItemVm>> SearchAsync(ExhibitionSearchVm search, long studentProfileId, int pageSize = 10);

    /// <summary>Details with the student's own entries and whether an artwork can be proposed (UPCOMING only, BR-42).</summary>
    /// <param name="exhibitionId">Exhibition id</param>
    /// <param name="studentProfileId">student_profile.id of the signed-in student</param>
    Task<StudentExhibitionDetailsVm> GetDetailsAsync(long exhibitionId, long studentProfileId);
}
