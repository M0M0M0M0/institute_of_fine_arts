using InstituteOfFineArts.Web.ViewModels.Common;
using InstituteOfFineArts.Web.ViewModels.Notifications;

namespace InstituteOfFineArts.Web.Services.Interfaces;

/// <summary>
/// Notifications (optional feature M12, tasks BE1-OPT 3 + BE2-OPT 5): in-app notifications under the bell
/// (F-M12-02) and the same text by e-mail through <see cref="IEmailService"/> (F-M12-01).
/// The event methods are called by other services right after their SaveChanges; they never throw, so a failed
/// notification or e-mail never breaks the user's action (it is logged instead).
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Inserts one notification per account and, when <paramref name="sendEmail"/> is true, e-mails each account.
    /// Never throws: failures are logged.
    /// </summary>
    /// <param name="accountIds">Receiving accounts (duplicates are ignored)</param>
    /// <param name="title">Short title, e.g. "New competition"</param>
    /// <param name="message">Message text (plain text; HTML-encoded in the e-mail)</param>
    /// <param name="link">Relative URL opened from the notification, or null</param>
    /// <param name="sendEmail">Also send an e-mail</param>
    /// <param name="cancellationToken">Cancellation</param>
    /// <returns>Number of notifications inserted (0 when saving failed)</returns>
    Task<int> NotifyAsync(IEnumerable<long> accountIds, string title, string message, string? link, bool sendEmail,
        CancellationToken cancellationToken = default);

    /// <summary>Notifications of the account, newest first, one page.</summary>
    /// <param name="accountId">Signed-in account</param>
    /// <param name="page">Page number (from 1)</param>
    /// <param name="pageSize">Rows per page</param>
    Task<PagedResult<NotificationItemVm>> GetListAsync(long accountId, int page, int pageSize = 10);

    /// <summary>Unread count and the newest notifications for the bell in the menu.</summary>
    /// <param name="accountId">Signed-in account</param>
    /// <param name="count">How many of the newest notifications to return</param>
    Task<NotificationBellVm> GetBellAsync(long accountId, int count = 5);

    /// <summary>Number of unread notifications of the account.</summary>
    /// <param name="accountId">Signed-in account</param>
    Task<int> CountUnreadAsync(long accountId);

    /// <summary>
    /// Marks one notification of the account as read and returns its link (null when it has none).
    /// Throws NotFoundException for an unknown id or a notification of another account.
    /// </summary>
    /// <param name="id">notification.id</param>
    /// <param name="accountId">Signed-in account</param>
    Task<string?> MarkReadAsync(long id, long accountId);

    /// <summary>Marks every unread notification of the account as read and returns how many changed.</summary>
    /// <param name="accountId">Signed-in account</param>
    Task<int> MarkAllReadAsync(long accountId);

    /// <summary>Event 1: a new competition was created → every active student (in-app + e-mail).</summary>
    /// <param name="competitionId">The new competition</param>
    Task CompetitionCreatedAsync(long competitionId);

    /// <summary>Event 2: results were published → every student who submitted to the competition (in-app + e-mail).</summary>
    /// <param name="competitionId">The published competition</param>
    Task ResultsPublishedAsync(long competitionId);

    /// <summary>Event 3: an award was issued → the winner (in-app + e-mail).</summary>
    /// <param name="awardIssuedId">award_issued.id</param>
    Task AwardIssuedAsync(long awardIssuedId);

    /// <summary>Event 4a: a painting was sold → the student (in-app + e-mail).</summary>
    /// <param name="entryId">exhibition_entry.id</param>
    Task EntrySoldAsync(long entryId);

    /// <summary>Event 4b: the sale price was paid to the student → the student (in-app).</summary>
    /// <param name="entryId">exhibition_entry.id</param>
    Task EntryPaidAsync(long entryId);

    /// <summary>Event 5: an exhibition proposal was accepted or rejected → the student (in-app).</summary>
    /// <param name="entryId">exhibition_entry.id</param>
    Task EntryReviewedAsync(long entryId);
}
