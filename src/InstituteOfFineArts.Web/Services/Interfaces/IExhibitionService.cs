using InstituteOfFineArts.Web.ViewModels.Common;
using InstituteOfFineArts.Web.ViewModels.Staff.Exhibitions;

namespace InstituteOfFineArts.Web.Services.Interfaces;

/// <summary>
/// Exhibitions managed by Staff and Manager (M09, task BE2-09; F-M09-01, F-M09-02): create, search, details,
/// edit, delete (BR-42). Business rule violations throw BusinessException (with the form field when there is one);
/// unknown ids throw NotFoundException.
/// </summary>
public interface IExhibitionService
{
    /// <summary>Searches exhibitions by name, city, calculated status and date range, newest start first.</summary>
    Task<PagedResult<ExhibitionListItemVm>> SearchAsync(ExhibitionSearchVm search, int pageSize = 10);

    /// <summary>Details with status, creator and the number of entries for each entry status.</summary>
    Task<ExhibitionDetailsVm> GetDetailsAsync(long id);

    /// <summary>Current values for the edit form, with the flags that lock the dates.</summary>
    Task<ExhibitionFormVm> GetForEditAsync(long id);

    /// <summary>Creates an exhibition; returns its id.</summary>
    /// <param name="form">Form data</param>
    /// <param name="staffProfileId">staff_profile.id of the creator (STAFF or MANAGER account)</param>
    Task<long> CreateAsync(ExhibitionFormVm form, long staffProfileId);

    /// <summary>Saves changes; dates are limited once artworks have been sent (see ExhibitionService).</summary>
    Task UpdateAsync(long id, ExhibitionFormVm form);

    /// <summary>Deletes an exhibition without entries (BR-42, US-12 AC2).</summary>
    Task DeleteAsync(long id);
}
