namespace InstituteOfFineArts.Web.Services.Interfaces;

/// <summary>
/// Push notifications to the phones of the mobile app (Firebase Cloud Messaging). The app registers its token after
/// signing in; <see cref="INotificationService"/> pushes every in-app notification to the receiving accounts' phones.
/// Push is off until Firebase is configured (Push:ServiceAccountPath); tokens are still kept, so phones get pushes as
/// soon as it is turned on.
/// </summary>
public interface IPushService
{
    /// <summary>
    /// Registers (or refreshes) the phone of <paramref name="accountId"/>. A token already registered for another
    /// account moves to this one (another user signed in on the same phone).
    /// Throws BusinessException for an empty or too long token or an unknown platform.
    /// </summary>
    /// <param name="accountId">Signed-in account</param>
    /// <param name="token">FCM registration token of the app</param>
    /// <param name="platform">"android" or "ios"</param>
    Task RegisterAsync(long accountId, string token, string platform);

    /// <summary>Removes the phone (signing out); a token of another account or an unknown token is ignored.</summary>
    /// <param name="accountId">Signed-in account</param>
    /// <param name="token">FCM registration token of the app</param>
    Task UnregisterAsync(long accountId, string token);

    /// <summary>
    /// Pushes a notification to every registered phone of the accounts, then removes the tokens the push service
    /// rejected for good and phones that have been quiet too long. Never throws; returns the number of phones reached.
    /// </summary>
    /// <param name="accountIds">Receiving accounts</param>
    /// <param name="title">Notification title</param>
    /// <param name="message">Notification text (plain text)</param>
    /// <param name="link">Relative link opened from the notification, or null</param>
    /// <param name="cancellationToken">Cancellation</param>
    Task<int> SendAsync(IReadOnlyCollection<long> accountIds, string title, string message, string? link,
        CancellationToken cancellationToken = default);
}
