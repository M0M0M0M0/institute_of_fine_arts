namespace InstituteOfFineArts.Web.Services.Interfaces;

/// <summary>
/// Deadline reminders (optional feature M12, F-M12-01 "sắp hết hạn"): reminds active students who have not submitted
/// to an ONGOING competition that ends tomorrow. Called once a day by the background job; safe to run any number of
/// times because a student is never reminded twice about the same competition.
/// </summary>
public interface IDeadlineReminderService
{
    /// <summary>Sends the reminders due on <paramref name="today"/> and returns how many notifications were created.</summary>
    /// <param name="today">Current date (default: today in the institute time zone); tests pass a fixed date</param>
    /// <param name="cancellationToken">Stops the work when the application shuts down</param>
    Task<int> SendRemindersAsync(DateOnly? today = null, CancellationToken cancellationToken = default);
}
