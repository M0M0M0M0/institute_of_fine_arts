using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Options;

namespace InstituteOfFineArts.Web.Infrastructure.Push;

/// <summary>
/// Push through Firebase Cloud Messaging (HTTP v1 API, FirebaseAdmin SDK), signed with the service account key of
/// <see cref="PushOptions.ServiceAccountPath"/>. Without a key (or with an unreadable one) push is off and every send
/// returns <see cref="PushResult.None"/>; the rest of the application does not notice.
/// Registered once for the whole application (singleton): the key is read at the first use.
/// </summary>
public sealed class FcmPushSender : IPushSender
{
    /// <summary>FCM accepts at most 500 tokens per multicast request.</summary>
    private const int BatchSize = 500;

    /// <summary>Name of the Firebase app instance in this process.</summary>
    private const string AppName = "ifa-push";

    private readonly ILogger<FcmPushSender> _logger;
    private readonly FirebaseMessaging? _messaging;

    /// <summary>Reads the key once; a missing or broken key leaves push off (logged).</summary>
    public FcmPushSender(IOptions<PushOptions> options, IWebHostEnvironment environment, ILogger<FcmPushSender> logger)
    {
        _logger = logger;
        var path = options.Value.ServiceAccountPath;
        if (string.IsNullOrWhiteSpace(path))
        {
            _logger.LogInformation("Push notifications are off: no Firebase service account key is configured (Push:ServiceAccountPath)");
            return;
        }

        var fullPath = Path.IsPathRooted(path) ? path : Path.Combine(environment.ContentRootPath, path);
        try
        {
            var credential = CredentialFactory.FromFile<ServiceAccountCredential>(fullPath).ToGoogleCredential();
            var app = FirebaseApp.GetInstance(AppName) ?? FirebaseApp.Create(new AppOptions { Credential = credential }, AppName);
            _messaging = FirebaseMessaging.GetMessaging(app);
            _logger.LogInformation("Push notifications are on (Firebase project {Project})", app.Options.ProjectId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Push notifications are off: the Firebase service account key {Path} could not be loaded", fullPath);
        }
    }

    /// <inheritdoc />
    public bool IsEnabled => _messaging != null;

    /// <inheritdoc />
    public async Task<PushResult> SendAsync(IReadOnlyCollection<string> tokens, PushMessage message, CancellationToken cancellationToken = default)
    {
        if (_messaging == null || tokens.Count == 0)
        {
            return PushResult.None;
        }

        var sent = 0;
        var invalid = new List<string>();
        foreach (var batch in tokens.Distinct().Chunk(BatchSize))
        {
            try
            {
                var response = await _messaging.SendEachForMulticastAsync(Build(batch, message), cancellationToken);
                sent += response.SuccessCount;
                for (var i = 0; i < response.Responses.Count; i++)
                {
                    var error = response.Responses[i].Exception;
                    if (error == null) continue;
                    // The app was removed or its token renewed: that phone will never get anything, forget the token
                    if (error.MessagingErrorCode is MessagingErrorCode.Unregistered or MessagingErrorCode.SenderIdMismatch)
                    {
                        invalid.Add(batch[i]);
                    }
                    else
                    {
                        _logger.LogWarning(error, "Push \"{Title}\" to one phone failed ({Code})", message.Title, error.MessagingErrorCode);
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Push \"{Title}\" to {Count} phone(s) failed", message.Title, batch.Length);
            }
        }
        _logger.LogInformation("Push \"{Title}\" sent to {Sent} phone(s); {Invalid} token(s) no longer valid", message.Title, sent, invalid.Count);
        return new PushResult(sent, invalid);
    }

    /// <summary>
    /// The notification the phone shows, plus the link in the data part so the app opens the right screen when it is
    /// tapped. High priority on Android so it arrives at once.
    /// </summary>
    private static MulticastMessage Build(IReadOnlyList<string> tokens, PushMessage message) => new()
    {
        Tokens = tokens.ToList(),
        Notification = new Notification { Title = message.Title, Body = message.Body },
        Data = message.Link == null
            ? new Dictionary<string, string>()
            : new Dictionary<string, string> { ["link"] = message.Link },
        Android = new AndroidConfig { Priority = Priority.High },
    };
}
