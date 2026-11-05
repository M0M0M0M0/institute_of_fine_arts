using InstituteOfFineArts.Web.ViewModels.Common;
using InstituteOfFineArts.Web.ViewModels.Staff.Awards;
using InstituteOfFineArts.Web.ViewModels.Staff.Submissions;

namespace InstituteOfFineArts.Web.Services.Interfaces;

/// <summary>
/// Awards given to submissions, managed by staff (F-M08-01..04, task BE1-11): issue, edit, revoke, search.
/// Business rule violations throw BusinessException (with the form field when there is one);
/// unknown ids in the URL throw NotFoundException.
/// </summary>
public interface IAwardIssuedService
{
    /// <summary>Searches issued awards by competition, student, award name and issued date range, newest first.</summary>
    /// <param name="search">Filters and page</param>
    /// <param name="pageSize">Rows per page</param>
    Task<PagedResult<AwardIssuedListItemVm>> SearchAsync(AwardIssuedSearchVm search, int pageSize = 10);

    /// <summary>Competitions for the list filter: every CLOSED or GRADED competition, newest first.</summary>
    Task<IReadOnlyList<CompetitionOptionVm>> GetCompetitionOptionsAsync();

    /// <summary>
    /// Issue form: the CLOSED / GRADED competitions and, when a competition is given, its awards with slots left
    /// and its eligible entries. Throws BusinessException when that competition does not accept awards yet.
    /// </summary>
    /// <param name="competitionId">Chosen competition (null = choose one first)</param>
    /// <param name="competitionAwardId">Award to pre-select (optional)</param>
    Task<AwardIssuedFormVm> GetForCreateAsync(long? competitionId, long? competitionAwardId = null);

    /// <summary>Edit form with the current values and the options of the award's competition.</summary>
    /// <param name="id">award_issued.id</param>
    Task<AwardIssuedFormVm> GetForEditAsync(long id);

    /// <summary>Fills the display data and options of a form shown again after an error (typed values are kept).</summary>
    /// <param name="form">Form posted by the browser</param>
    Task FillOptionsAsync(AwardIssuedFormVm form);

    /// <summary>Issues an award (BR-30, BR-31, BR-32); returns the new award_issued.id.</summary>
    /// <param name="form">Form data</param>
    /// <param name="staffProfileId">staff_profile.id of the staff member issuing it</param>
    Task<long> CreateAsync(AwardIssuedFormVm form, long staffProfileId);

    /// <summary>Changes the entry, award, date or remarks of an issued award with the same rules.</summary>
    /// <param name="id">award_issued.id</param>
    /// <param name="form">Form data</param>
    /// <returns>Competition id of the award (to go back to its page)</returns>
    Task<long> UpdateAsync(long id, AwardIssuedFormVm form);

    /// <summary>Revokes (deletes) an issued award; the reason is required and written to the log (BR-33).</summary>
    /// <param name="id">award_issued.id</param>
    /// <param name="reason">Why the award is revoked</param>
    /// <param name="staffProfileId">staff_profile.id of the staff member revoking it</param>
    /// <returns>Competition id of the revoked award (to go back to its page)</returns>
    Task<long> RevokeAsync(long id, string? reason, long staffProfileId);
}
