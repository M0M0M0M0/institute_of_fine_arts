using InstituteOfFineArts.Web.ViewModels.Common;
using InstituteOfFineArts.Web.ViewModels.Staff.ExhibitionEntries;

namespace InstituteOfFineArts.Web.Services.Interfaces;

/// <summary>
/// Exhibition entries managed by staff (tasks BE1-12, BE1-13; F-M09-05, 06, 08, 09; SCR-30, SCR-31): list and search,
/// direct create with the student's price, accept / reject proposals, record the sale or UNSOLD, record the payment
/// to the student, delete before the exhibition. Business rule violations throw BusinessException (with the form
/// field when there is one); unknown ids throw NotFoundException.
/// </summary>
public interface IExhibitionEntryService
{
    /// <summary>Entries with filters (exhibition, status, paid, student) and paging, newest first.</summary>
    /// <param name="search">Filters</param>
    /// <param name="pageSize">Rows per page</param>
    Task<PagedResult<EntryListItemVm>> SearchAsync(EntrySearchVm search, int pageSize = 10);

    /// <summary>Every exhibition, newest first (options of the exhibition filter).</summary>
    Task<List<EntryExhibitionOptionVm>> GetExhibitionOptionsAsync();

    /// <summary>Empty direct-create form with the paintings that may be sent. Throws BusinessException when the exhibition is not UPCOMING.</summary>
    /// <param name="exhibitionId">Exhibition id</param>
    Task<EntryCreateFormVm> GetNewFormAsync(long exhibitionId);

    /// <summary>Creates an ACCEPTED entry with the student's price and the note about its source (BR-40..BR-43); returns its id.</summary>
    /// <param name="form">Form data</param>
    /// <param name="staffProfileId">staff_profile.id of the signed-in staff member (ReviewedBy)</param>
    /// <param name="accountId">account.id of the signed-in staff member (ProposedBy)</param>
    Task<long> CreateAsync(EntryCreateFormVm form, long staffProfileId, long accountId);

    /// <summary>Details with the allowed actions and default form values.</summary>
    /// <param name="id">Entry id</param>
    Task<EntryDetailsVm> GetDetailsAsync(long id);

    /// <summary>PENDING → ACCEPTED while the exhibition is UPCOMING.</summary>
    /// <param name="id">Entry id</param>
    /// <param name="staffProfileId">staff_profile.id of the reviewer</param>
    Task AcceptAsync(long id, long staffProfileId);

    /// <summary>PENDING → REJECTED with a reason.</summary>
    /// <param name="id">Entry id</param>
    /// <param name="form">Reason</param>
    /// <param name="staffProfileId">staff_profile.id of the reviewer</param>
    Task RejectAsync(long id, RejectFormVm form, long staffProfileId);

    /// <summary>ACCEPTED → SOLD with sold price, sale date and customer (BR-44).</summary>
    /// <param name="id">Entry id</param>
    /// <param name="form">Sale data</param>
    /// <param name="staffProfileId">staff_profile.id of the staff member recording the sale</param>
    Task MarkSoldAsync(long id, SoldFormVm form, long staffProfileId);

    /// <summary>ACCEPTED → UNSOLD after the exhibition has finished.</summary>
    /// <param name="id">Entry id</param>
    /// <param name="staffProfileId">staff_profile.id of the staff member</param>
    Task MarkUnsoldAsync(long id, long staffProfileId);

    /// <summary>Records the payment of the full sale price to the student (BR-45); the entry must be SOLD.</summary>
    /// <param name="id">Entry id</param>
    /// <param name="form">Amount and date</param>
    /// <param name="staffProfileId">staff_profile.id of the staff member recording the payment</param>
    Task MarkPaidAsync(long id, PaidFormVm form, long staffProfileId);

    /// <summary>Deletes an entry that is not SOLD/UNSOLD while its exhibition has not started.</summary>
    /// <param name="id">Entry id</param>
    /// <returns>Id of the exhibition the entry belonged to</returns>
    Task<long> DeleteAsync(long id);
}
