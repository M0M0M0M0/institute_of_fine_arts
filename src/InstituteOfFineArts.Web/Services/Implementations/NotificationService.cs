using System.Globalization;
using System.Net;
using InstituteOfFineArts.Web.Data;
using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.Services.Interfaces;
using InstituteOfFineArts.Web.ViewModels.Common;
using InstituteOfFineArts.Web.ViewModels.Notifications;
using Microsoft.EntityFrameworkCore;

namespace InstituteOfFineArts.Web.Services.Implementations;

/// <summary>
/// Notifications (optional feature M12, tasks BE1-OPT 3 + BE2-OPT 5): one notification row per receiving account
/// for the bell (F-M12-02) and, for the important events, the same text by e-mail (F-M12-01).
/// Rules: a notification is only created after the user's action has been saved, and nothing here ever throws to the
/// caller – a database or SMTP failure is logged and the action still succeeds. User text (titles of competitions and
/// paintings, reject reasons) is HTML-encoded in e-mails. A user only sees and changes his own notifications.
/// Decision: the e-mail service is optional in Dependency Injection, so the in-app part keeps working when no
/// <see cref="IEmailService"/> is registered (e-mails are then skipped and logged).
/// </summary>
public class NotificationService : INotificationService
{
    /// <summary>Title of event 1 (new competition).</summary>
    public const string NewCompetitionTitle = "New competition";

    /// <summary>Title of event 2 (results published).</summary>
    public const string ResultsPublishedTitle = "Results are published";

    /// <summary>Title of event 3 (award issued).</summary>
    public const string AwardIssuedTitle = "You received an award";

    /// <summary>Title of event 4a (painting sold).</summary>
    public const string PaintingSoldTitle = "Painting sold";

    /// <summary>Title of event 4b (sale price paid to the student).</summary>
    public const string PaymentTitle = "Payment recorded";

    /// <summary>Title of event 5 when the proposal is accepted.</summary>
    public const string ProposalAcceptedTitle = "Exhibition proposal accepted";

    /// <summary>Title of event 5 when the proposal is rejected.</summary>
    public const string ProposalRejectedTitle = "Exhibition proposal rejected";

    /// <summary>Title of event 6 (deadline reminder); also the key that prevents a second reminder.</summary>
    public const string DeadlineReminderTitle = "Deadline reminder";

    /// <summary>Link of the results page (event 2).</summary>
    public const string ResultsLink = "/student/results";

    /// <summary>Link of the student's awards page (event 3).</summary>
    public const string AwardsLink = "/student/awards";

    /// <summary>Configuration key of the site address used to build absolute links in e-mails (optional).</summary>
    public const string BaseUrlKey = "Notifications:BaseUrl";

    /// <summary>Column lengths of table notification.</summary>
    private const int MaxTitle = 150, MaxMessage = 1000, MaxLink = 255;

    /// <summary>Format of dates in messages.</summary>
    private const string DateFormat = "dd MMM yyyy";

    /// <summary>Database access.</summary>
    private readonly AppDbContext _db;

    /// <summary>Sends the e-mails; null when no e-mail service is registered.</summary>
    private readonly IEmailService? _email;

    /// <summary>Logs notifications sent and failures.</summary>
    private readonly ILogger<NotificationService> _logger;

    /// <summary>Site address for absolute links in e-mails (from <see cref="BaseUrlKey"/>), or null.</summary>
    private readonly string? _baseUrl;

    /// <summary>Receives the required services through Dependency Injection.</summary>
    /// <param name="db">Database</param>
    /// <param name="logger">Logger</param>
    /// <param name="email">E-mail sender (optional: e-mails are skipped when it is not registered)</param>
    /// <param name="configuration">Configuration holding the optional site address</param>
    public NotificationService(AppDbContext db, ILogger<NotificationService> logger, IEmailService? email = null,
        IConfiguration? configuration = null)
    {
        _db = db;
        _logger = logger;
        _email = email;
        _baseUrl = configuration?[BaseUrlKey]?.TrimEnd('/');
    }

    /// <summary>Link of a competition for students.</summary>
    /// <param name="competitionId">competition.id</param>
    public static string CompetitionLink(long competitionId) => $"/student/competitions/{competitionId}";

    /// <summary>Link of an exhibition entry for its student.</summary>
    /// <param name="entryId">exhibition_entry.id</param>
    public static string EntryLink(long entryId) => $"/student/exhibition-entries/{entryId}";

    /// <summary>Formats an amount in USD (BR-47).</summary>
    private static string Money(decimal? amount) => (amount ?? 0m).ToString("$#,##0.00", CultureInfo.InvariantCulture);

    /// <summary>Formats a date as dd MMM yyyy whatever the culture of the current thread.</summary>
    private static string Date(DateOnly date) => date.ToString(DateFormat, CultureInfo.InvariantCulture);

    // ------------------------------------------------------------------ Core

    /// <inheritdoc />
    public async Task<int> NotifyAsync(IEnumerable<long> accountIds, string title, string message, string? link, bool sendEmail,
        CancellationToken cancellationToken = default)
    {
        var ids = accountIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return 0;
        }

        // One row per account; texts cut to the column lengths so a long competition title never fails the insert
        var rows = ids.Select(id => new Notification
        {
            AccountId = id,
            Title = Cut(title, MaxTitle),
            Message = Cut(message, MaxMessage),
            Link = link == null ? null : Cut(link, MaxLink),
        }).ToList();

        try
        {
            _db.Notifications.AddRange(rows);
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The user's action is already saved: forget the rows so a later SaveChanges does not retry them
            foreach (var row in rows)
            {
                _db.Entry(row).State = EntityState.Detached;
            }
            _logger.LogError(ex, "Notification \"{Title}\" could not be saved for {Count} account(s)", title, ids.Count);
            return 0;
        }
        _logger.LogInformation("Notification \"{Title}\" created for {Count} account(s)", title, ids.Count);

        if (sendEmail)
        {
            await SendEmailsAsync(ids, title, message, link, cancellationToken);
        }
        return rows.Count;
    }

    /// <summary>E-mails every account that has an address; a failure is logged and the next account is tried.</summary>
    private async Task SendEmailsAsync(List<long> ids, string title, string message, string? link, CancellationToken cancellationToken)
    {
        if (_email == null)
        {
            _logger.LogWarning("No e-mail service is registered; e-mail \"{Title}\" was not sent", title);
            return;
        }

        try
        {
            var recipients = await _db.Accounts.AsNoTracking()
                .Where(a => ids.Contains(a.Id) && a.Email != "")
                .Select(a => new { a.Id, a.Email, a.FullName })
                .ToListAsync(cancellationToken);

            foreach (var r in recipients)
            {
                try
                {
                    var sent = await _email.SendAsync(r.Email, r.FullName, title, EmailBody(r.FullName, message, link), cancellationToken);
                    if (!sent)
                    {
                        _logger.LogWarning("E-mail \"{Title}\" to account {AccountId} was not sent", title, r.Id);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // One bad address or SMTP hiccup must not stop the others
                    _logger.LogError(ex, "E-mail \"{Title}\" to account {AccountId} failed", title, r.Id);
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "E-mails \"{Title}\" could not be prepared", title);
        }
    }

    /// <summary>HTML body of a notification e-mail; every piece of text is HTML-encoded.</summary>
    /// <param name="fullName">Recipient name</param>
    /// <param name="message">Plain-text message</param>
    /// <param name="link">Relative link, or null</param>
    public string EmailBody(string fullName, string message, string? link)
    {
        var body = $"<p>Dear {WebUtility.HtmlEncode(fullName)},</p>"
                   + $"<p style=\"white-space: pre-line\">{WebUtility.HtmlEncode(message)}</p>";

        // Absolute link when the site address is configured, otherwise the page to open after signing in
        if (link != null)
        {
            body += _baseUrl != null
                ? $"<p><a href=\"{WebUtility.HtmlEncode(_baseUrl + link)}\">Open in the Institute of Fine Arts website</a></p>"
                : $"<p>Sign in to the Institute of Fine Arts website and open {WebUtility.HtmlEncode(link)} to see the details.</p>";
        }
        return body + "<p>Institute of Fine Arts</p>";
    }

    /// <summary>Cuts <paramref name="text"/> to <paramref name="max"/> characters.</summary>
    private static string Cut(string text, int max) => text.Length <= max ? text : text[..(max - 1)] + "…";

    // ------------------------------------------------------------------ Bell and list

    /// <inheritdoc />
    public Task<PagedResult<NotificationItemVm>> GetListAsync(long accountId, int page, int pageSize = 10)
        => PagedResult<NotificationItemVm>.CreateAsync(Project(accountId), page, pageSize);

    /// <inheritdoc />
    public async Task<NotificationBellVm> GetBellAsync(long accountId, int count = 5) => new()
    {
        UnreadCount = await CountUnreadAsync(accountId),
        Items = await Project(accountId).Take(count).ToListAsync(),
    };

    /// <inheritdoc />
    public Task<int> CountUnreadAsync(long accountId)
        => _db.Notifications.CountAsync(n => n.AccountId == accountId && !n.IsRead);

    /// <inheritdoc />
    public async Task<string?> MarkReadAsync(long id, long accountId)
    {
        // Another user's notification is "not found" (no information leak)
        var n = await _db.Notifications.FirstOrDefaultAsync(x => x.Id == id && x.AccountId == accountId)
                ?? throw new NotFoundException($"Notification {id} was not found.");
        if (!n.IsRead)
        {
            n.IsRead = true;
            await _db.SaveChangesAsync();
        }
        return n.Link;
    }

    /// <inheritdoc />
    public async Task<int> MarkAllReadAsync(long accountId)
    {
        var unread = await _db.Notifications.Where(n => n.AccountId == accountId && !n.IsRead).ToListAsync();
        foreach (var n in unread)
        {
            n.IsRead = true;
        }
        await _db.SaveChangesAsync();
        _logger.LogInformation("{Count} notification(s) of account {AccountId} marked as read", unread.Count, accountId);
        return unread.Count;
    }

    /// <summary>Notifications of one account, newest first, projected for display.</summary>
    private IQueryable<NotificationItemVm> Project(long accountId)
        => _db.Notifications.AsNoTracking()
            .Where(n => n.AccountId == accountId)
            .OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.Id)
            .Select(n => new NotificationItemVm
            {
                Id = n.Id, Title = n.Title, Message = n.Message, Link = n.Link, IsRead = n.IsRead, CreatedAt = n.CreatedAt,
            });

    // ------------------------------------------------------------------ Events

    /// <inheritdoc />
    public Task CompetitionCreatedAsync(long competitionId) => SafeAsync(nameof(CompetitionCreatedAsync), competitionId, async () =>
    {
        var c = await _db.Competitions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == competitionId);
        if (c == null)
        {
            return;
        }

        // Every student who can sign in (locked accounts are left out)
        var students = await ActiveStudentAccountIds().ToListAsync();
        await NotifyAsync(students, NewCompetitionTitle,
            $"A new competition \"{c.Title}\" is open from {Date(c.StartDate)} to {Date(c.EndDate)}.",
            CompetitionLink(c.Id), sendEmail: true);
    });

    /// <inheritdoc />
    public Task ResultsPublishedAsync(long competitionId) => SafeAsync(nameof(ResultsPublishedAsync), competitionId, async () =>
    {
        var c = await _db.Competitions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == competitionId);
        if (c == null)
        {
            return;
        }

        // Every student who submitted an entry to the competition
        var students = await _db.Submissions.AsNoTracking()
            .Where(s => s.CompetitionId == competitionId)
            .Select(s => s.Student.AccountId)
            .Distinct()
            .ToListAsync();
        await NotifyAsync(students, ResultsPublishedTitle,
            $"The results of the competition \"{c.Title}\" are published. See your rating and remarks.",
            ResultsLink, sendEmail: true);
    });

    /// <inheritdoc />
    public Task AwardIssuedAsync(long awardIssuedId) => SafeAsync(nameof(AwardIssuedAsync), awardIssuedId, async () =>
    {
        var a = await _db.AwardsIssued.AsNoTracking()
            .Where(x => x.Id == awardIssuedId)
            .Select(x => new
            {
                x.Submission.Student.AccountId,
                SubmissionTitle = x.Submission.Title,
                x.CompetitionAward.AwardName,
                CompetitionTitle = x.CompetitionAward.Competition.Title,
            })
            .FirstOrDefaultAsync();
        if (a == null)
        {
            return;
        }

        await NotifyAsync([a.AccountId], AwardIssuedTitle,
            $"Congratulations! Your entry \"{a.SubmissionTitle}\" received the award \"{a.AwardName}\" in the competition \"{a.CompetitionTitle}\".",
            AwardsLink, sendEmail: true);
    });

    /// <inheritdoc />
    public Task EntrySoldAsync(long entryId) => SafeAsync(nameof(EntrySoldAsync), entryId, async () =>
    {
        var e = await EntryAsync(entryId);
        if (e == null)
        {
            return;
        }

        await NotifyAsync([e.AccountId], PaintingSoldTitle,
            $"Your painting \"{e.SubmissionTitle}\" was sold at the exhibition \"{e.ExhibitionName}\" for {Money(e.SoldPrice)}"
            + (e.SoldDate is { } d ? $" on {Date(d)}." : "."),
            EntryLink(entryId), sendEmail: true);
    });

    /// <inheritdoc />
    public Task EntryPaidAsync(long entryId) => SafeAsync(nameof(EntryPaidAsync), entryId, async () =>
    {
        var e = await EntryAsync(entryId);
        if (e == null)
        {
            return;
        }

        await NotifyAsync([e.AccountId], PaymentTitle,
            $"{Money(e.PaidAmount)} for your painting \"{e.SubmissionTitle}\" has been paid to you"
            + (e.PaidDate is { } d ? $" on {Date(d)}." : "."),
            EntryLink(entryId), sendEmail: false);
    });

    /// <inheritdoc />
    public Task EntryReviewedAsync(long entryId) => SafeAsync(nameof(EntryReviewedAsync), entryId, async () =>
    {
        var e = await EntryAsync(entryId);

        // Only the two review outcomes are announced
        if (e is not { Status: EntryStatus.ACCEPTED or EntryStatus.REJECTED })
        {
            return;
        }

        var (title, message) = e.Status == EntryStatus.ACCEPTED
            ? (ProposalAcceptedTitle, $"Your painting \"{e.SubmissionTitle}\" was accepted for the exhibition \"{e.ExhibitionName}\".")
            : (ProposalRejectedTitle, $"Your painting \"{e.SubmissionTitle}\" was not accepted for the exhibition \"{e.ExhibitionName}\". Reason: {e.RejectReason}");
        await NotifyAsync([e.AccountId], title, message, EntryLink(entryId), sendEmail: false);
    });

    /// <summary>account.id of every active student.</summary>
    private IQueryable<long> ActiveStudentAccountIds()
        => _db.Accounts.AsNoTracking().Where(a => a.Role == Role.STUDENT && a.Active).Select(a => a.Id);

    /// <summary>Data of an exhibition entry needed by the messages, or null when it does not exist.</summary>
    private Task<EntryInfo?> EntryAsync(long entryId)
        => _db.ExhibitionEntries.AsNoTracking()
            .Where(e => e.Id == entryId)
            .Select(e => new EntryInfo(e.Submission.Student.AccountId, e.Submission.Title, e.Exhibition.Name, e.Status,
                e.RejectReason, e.SoldPrice, e.SoldDate, e.PaidAmount, e.PaidDate))
            .FirstOrDefaultAsync();

    /// <summary>Runs an event handler and logs any failure instead of throwing it to the caller.</summary>
    /// <param name="eventName">Name of the event (for the log)</param>
    /// <param name="id">Id of the changed row (for the log)</param>
    /// <param name="handler">Event handler</param>
    private async Task SafeAsync(string eventName, long id, Func<Task> handler)
    {
        try
        {
            await handler();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Notification event {Event} for id {Id} failed", eventName, id);
        }
    }

    /// <summary>Exhibition entry data used in the messages of events 4 and 5.</summary>
    private sealed record EntryInfo(long AccountId, string SubmissionTitle, string ExhibitionName, EntryStatus Status,
        string? RejectReason, decimal? SoldPrice, DateOnly? SoldDate, decimal? PaidAmount, DateOnly? PaidDate);
}
