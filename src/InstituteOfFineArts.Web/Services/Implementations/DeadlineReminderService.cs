using System.Globalization;
using InstituteOfFineArts.Web.Data;
using InstituteOfFineArts.Web.Helpers;
using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace InstituteOfFineArts.Web.Services.Implementations;

/// <summary>
/// Deadline reminders (optional feature M12, F-M12-01; tasks BE1-OPT 3 + BE2-OPT 5): for every competition that is
/// ONGOING today and ends tomorrow, each active student without an entry receives an in-app notification and an
/// e-mail. A reminder already stored for the same student and competition (same title and link) is never sent again,
/// so the daily job may run several times a day (e.g. after a restart) without duplicates.
/// </summary>
public class DeadlineReminderService : IDeadlineReminderService
{
    /// <summary>Database access.</summary>
    private readonly AppDbContext _db;

    /// <summary>Creates the notifications and e-mails.</summary>
    private readonly INotificationService _notifications;

    /// <summary>Logs the number of reminders.</summary>
    private readonly ILogger<DeadlineReminderService> _logger;

    /// <summary>Receives the required services through Dependency Injection.</summary>
    public DeadlineReminderService(AppDbContext db, INotificationService notifications, ILogger<DeadlineReminderService> logger)
    {
        _db = db;
        _notifications = notifications;
        _logger = logger;
    }

    /// <summary>Message of the reminder for the competition <paramref name="title"/> ending on <paramref name="endDate"/>.</summary>
    public static string ReminderMessage(string title, DateOnly endDate)
        => $"The competition \"{title}\" closes tomorrow ({endDate.ToString("dd MMM yyyy", CultureInfo.InvariantCulture)} at 23:59) "
           + "and you have not submitted an entry yet.";

    /// <inheritdoc />
    public async Task<int> SendRemindersAsync(DateOnly? today = null, CancellationToken cancellationToken = default)
    {
        var day = today ?? DateTimeHelper.Today;
        var tomorrow = day.AddDays(1);

        // Competitions ending tomorrow; the resolver decides that they are still ONGOING today (BR-07)
        var competitions = (await _db.Competitions.AsNoTracking()
                .Where(c => c.EndDate == tomorrow)
                .ToListAsync(cancellationToken))
            .Where(c => CompetitionStatusResolver.Resolve(c, day) == CompetitionStatus.ONGOING)
            .ToList();

        var total = 0;
        foreach (var c in competitions)
        {
            var link = NotificationService.CompetitionLink(c.Id);

            // Active students without an entry in this competition and not reminded about it yet
            var students = await _db.Accounts.AsNoTracking()
                .Where(a => a.Role == Role.STUDENT && a.Active && a.StudentProfile != null
                    && !a.StudentProfile.Submissions.Any(s => s.CompetitionId == c.Id)
                    && !_db.Notifications.Any(n => n.AccountId == a.Id
                        && n.Title == NotificationService.DeadlineReminderTitle && n.Link == link))
                .Select(a => a.Id)
                .ToListAsync(cancellationToken);

            total += await _notifications.NotifyAsync(students, NotificationService.DeadlineReminderTitle,
                ReminderMessage(c.Title, c.EndDate), link, sendEmail: true, cancellationToken);
        }

        if (total > 0)
        {
            _logger.LogInformation("Deadline reminders sent: {Count} notification(s) for {Competitions} competition(s)",
                total, competitions.Count);
        }
        return total;
    }
}
