using InstituteOfFineArts.Web.Infrastructure.Audit;
using InstituteOfFineArts.Web.ViewModels.Admin.AuditLog;
using InstituteOfFineArts.Web.ViewModels.Common;

namespace InstituteOfFineArts.Web.Services.Interfaces;

/// <summary>
/// Activity log (F-M17-01): records who did what and when, and lists it for the administrator.
/// Writing never throws: a logging failure must not break the user's action.
/// Never pass passwords or file contents in <c>details</c>.
/// </summary>
public interface IAuditLogService
{
    /// <summary>
    /// Records an action of the current user (account, user name and IP read from the current HTTP request).
    /// </summary>
    /// <param name="action">What was done, e.g. "LOGOUT" or "Competitions/Delete"</param>
    /// <param name="entity">Kind of object concerned</param>
    /// <param name="entityId">Id of the object concerned</param>
    /// <param name="details">Short description (cut to 500 characters)</param>
    Task LogAsync(string action, string? entity = null, long? entityId = null, string? details = null);

    /// <summary>
    /// Records an action for a user who is not (yet) the current user: a successful log in (the cookie is only
    /// read on the next request) or a failed one (anonymous, with the user name that was tried). The IP is still
    /// read from the current HTTP request.
    /// </summary>
    /// <param name="accountId">Account id, or null when unknown</param>
    /// <param name="username">User name (cut to 50 characters)</param>
    /// <param name="action">What was done</param>
    /// <param name="details">Short description (cut to 500 characters)</param>
    Task LogAsAsync(long? accountId, string? username, string action, string? details = null);

    /// <summary>Records an action of the current user with the objects it belongs to (see <see cref="AuditEntry"/>).</summary>
    /// <param name="entry">The row: action, kind of work, sentence, details and the competition / exhibition / entry / account</param>
    Task RecordAsync(AuditEntry entry);

    /// <summary>History of one object (competition, exhibition, entry) or one account, newest first.</summary>
    /// <param name="query">The object, an optional kind of work and the page</param>
    Task<PagedResult<AuditLogListItemVm>> GetHistoryAsync(AuditHistoryQuery query);

    /// <summary>Account id of a student profile (<paramref name="student"/>) or staff profile; null when unknown.</summary>
    Task<long?> AccountIdOfProfileAsync(bool student, long profileId);

    /// <summary>Log rows matching the filters, newest first, 10 per page.</summary>
    /// <param name="search">Filters (user, action, kind, competition, exhibition, date range) and page</param>
    /// <exception cref="Exceptions.BusinessException">From date after To date</exception>
    Task<PagedResult<AuditLogListItemVm>> SearchAsync(AuditLogSearchVm search);
}
