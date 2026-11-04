using InstituteOfFineArts.Web.ViewModels.Admin.Staff;
using InstituteOfFineArts.Web.ViewModels.Common;

namespace InstituteOfFineArts.Web.Services.Interfaces;

/// <summary>
/// Admin – staff accounts (M03, task BE2-02): STAFF, MANAGER and ADMIN accounts with their staff_profile.
/// Ids are staff_profile ids. Rule violations throw BusinessException (with the form field when there is one);
/// unknown ids throw NotFoundException.
/// </summary>
public interface IStaffService
{
    /// <summary>Searches by keyword, role, subject, class and status, ordered by staff code.</summary>
    Task<PagedResult<StaffListItemVm>> SearchAsync(StaffSearchVm search, int pageSize = 10);

    /// <summary>Details with counts of related data.</summary>
    /// <param name="id">staff_profile.id</param>
    /// <param name="currentAccountId">account.id of the signed-in administrator</param>
    Task<StaffDetailsVm> GetDetailsAsync(long id, long currentAccountId);

    /// <summary>Current values for the edit form.</summary>
    Task<StaffFormVm> GetForEditAsync(long id);

    /// <summary>Creates the account and its staff profile in one save (one transaction); returns the profile id.</summary>
    Task<long> CreateAsync(CreateStaffVm form);

    /// <summary>Saves changes (not the username). Applies BR-01 and BR-03.</summary>
    Task UpdateAsync(long id, StaffFormVm form, long currentAccountId);

    /// <summary>Sets a temporary password (BR-02, stored as BCrypt).</summary>
    Task ResetPasswordAsync(long id, string newPassword);

    /// <summary>Locks or unlocks the account (BR-03: not yourself, not the last active administrator).</summary>
    Task SetActiveAsync(long id, bool active, long currentAccountId);

    /// <summary>Deletes the account, or locks it when related data exists (BR-04).</summary>
    Task<StaffDeleteResult> DeleteAsync(long id, long currentAccountId);
}
