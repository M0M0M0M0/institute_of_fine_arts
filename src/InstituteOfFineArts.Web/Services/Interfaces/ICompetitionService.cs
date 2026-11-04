using InstituteOfFineArts.Web.ViewModels.Common;
using InstituteOfFineArts.Web.ViewModels.Staff.Competitions;

namespace InstituteOfFineArts.Web.Services.Interfaces;

/// <summary>
/// Competitions managed by staff (M05, task BE1-05): create, search, details, edit (BR-10), delete (BR-11).
/// Business rule violations throw BusinessException (with the form field when there is one);
/// unknown ids throw NotFoundException.
/// </summary>
public interface ICompetitionService
{
    /// <summary>Searches competitions by keyword, calculated status and date range, newest start first.</summary>
    Task<PagedResult<CompetitionListItemVm>> SearchAsync(CompetitionSearchVm search, int pageSize = 10);

    /// <summary>Details with awards, number of entries, status and what staff may do.</summary>
    Task<CompetitionDetailsVm> GetDetailsAsync(long id);

    /// <summary>Current values for the edit form. Throws BusinessException when results are published (read only).</summary>
    Task<CompetitionFormVm> GetForEditAsync(long id);

    /// <summary>Creates a competition with a generated code (CTyyyy-nn) and optional cover; returns its id.</summary>
    /// <param name="form">Form data</param>
    /// <param name="staffProfileId">staff_profile.id of the creator</param>
    Task<long> CreateAsync(CompetitionFormVm form, long staffProfileId);

    /// <summary>Saves changes within BR-10 (entries exist → only extend the end date, description, rules, cover).</summary>
    Task UpdateAsync(long id, CompetitionFormVm form);

    /// <summary>Deletes a competition without entries, with its awards and cover (BR-11).</summary>
    Task DeleteAsync(long id);
}
