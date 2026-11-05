using InstituteOfFineArts.Web.ViewModels.Common;
using InstituteOfFineArts.Web.ViewModels.Student.ExhibitionEntries;

namespace InstituteOfFineArts.Web.Services.Interfaces;

/// <summary>
/// A student's own exhibition entries (task BE1-12; F-M09-04, F-M09-10, F-M09-12; SCR-49): propose a painting,
/// change the asking price or withdraw while PENDING (BR-46), follow the sale and the payment (US-13 AC5).
/// Business rule violations throw BusinessException (with the form field when there is one); unknown ids and
/// other students' entries throw NotFoundException.
/// </summary>
public interface IStudentExhibitionEntryService
{
    /// <summary>My entries with filters (status, exhibition) and paging, newest first.</summary>
    /// <param name="studentProfileId">student_profile.id of the signed-in student</param>
    /// <param name="search">Filters</param>
    /// <param name="pageSize">Rows per page</param>
    Task<PagedResult<MyEntryListItemVm>> SearchMineAsync(long studentProfileId, MyEntrySearchVm search, int pageSize = 10);

    /// <summary>Exhibitions the student has entries in (options of the exhibition filter).</summary>
    /// <param name="studentProfileId">student_profile.id of the signed-in student</param>
    Task<List<MyExhibitionOptionVm>> GetMyExhibitionsAsync(long studentProfileId);

    /// <summary>Empty proposal form with the paintings that may be proposed. Throws BusinessException when the exhibition is not UPCOMING.</summary>
    /// <param name="exhibitionId">Exhibition id</param>
    /// <param name="studentProfileId">student_profile.id of the signed-in student</param>
    Task<ProposeEntryFormVm> GetNewFormAsync(long exhibitionId, long studentProfileId);

    /// <summary>Creates a PENDING entry (BR-40..BR-43) proposed by the student's account; returns its id.</summary>
    /// <param name="form">Form data</param>
    /// <param name="studentProfileId">student_profile.id of the signed-in student</param>
    /// <param name="accountId">account.id of the signed-in student (ProposedBy)</param>
    Task<long> CreateAsync(ProposeEntryFormVm form, long studentProfileId, long accountId);

    /// <summary>Details of an own entry.</summary>
    /// <param name="id">Entry id</param>
    /// <param name="studentProfileId">student_profile.id of the signed-in student</param>
    Task<MyEntryDetailsVm> GetDetailsAsync(long id, long studentProfileId);

    /// <summary>Current asking price for the edit form. Throws BusinessException when the entry is no longer PENDING (BR-46).</summary>
    /// <param name="id">Entry id</param>
    /// <param name="studentProfileId">student_profile.id of the signed-in student</param>
    Task<EditPriceFormVm> GetForEditAsync(long id, long studentProfileId);

    /// <summary>Saves a new asking price while the entry is PENDING (BR-41, BR-46).</summary>
    /// <param name="id">Entry id</param>
    /// <param name="form">Form data</param>
    /// <param name="studentProfileId">student_profile.id of the signed-in student</param>
    Task UpdatePriceAsync(long id, EditPriceFormVm form, long studentProfileId);

    /// <summary>Withdraws (deletes) an own proposal while it is PENDING (F-M09-12, BR-46).</summary>
    /// <param name="id">Entry id</param>
    /// <param name="studentProfileId">student_profile.id of the signed-in student</param>
    Task WithdrawAsync(long id, long studentProfileId);
}
