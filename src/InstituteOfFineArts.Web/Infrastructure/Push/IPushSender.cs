namespace InstituteOfFineArts.Web.Infrastructure.Push;

/// <summary>One push message: what the phone shows, and the in-app link it opens when tapped.</summary>
/// <param name="Title">Notification title</param>
/// <param name="Body">Notification text</param>
/// <param name="Link">Relative link of the notification (same as the in-app notification), or null</param>
public sealed record PushMessage(string Title, string Body, string? Link);

/// <summary>Result of a send: how many phones got it and which tokens are no longer valid (app removed, token renewed).</summary>
/// <param name="Sent">Messages accepted by the push service</param>
/// <param name="InvalidTokens">Tokens the push service rejected for good; the caller deletes them</param>
public sealed record PushResult(int Sent, IReadOnlyList<string> InvalidTokens)
{
    /// <summary>Nothing sent.</summary>
    public static readonly PushResult None = new(0, Array.Empty<string>());
}

/// <summary>
/// Delivers push messages to phones. <see cref="FcmPushSender"/> sends through Firebase Cloud Messaging when it is
/// configured (<see cref="PushOptions"/>); otherwise nothing is sent and <see cref="IsEnabled"/> is false.
/// </summary>
public interface IPushSender
{
    /// <summary>False when push is not configured: callers can skip preparing messages.</summary>
    bool IsEnabled { get; }

    /// <summary>Sends <paramref name="message"/> to every token. Never throws: failures are logged.</summary>
    Task<PushResult> SendAsync(IReadOnlyCollection<string> tokens, PushMessage message, CancellationToken cancellationToken = default);
}
