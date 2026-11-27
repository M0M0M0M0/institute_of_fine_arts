using InstituteOfFineArts.Web.Data;
using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Helpers;
using InstituteOfFineArts.Web.Infrastructure.Push;
using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace InstituteOfFineArts.Web.Services.Implementations;

/// <summary>
/// Phones of the mobile app and push notifications to them (see <see cref="IPushService"/>). Delivery itself is done by
/// <see cref="IPushSender"/> (Firebase Cloud Messaging); this service picks the phones and keeps the token list clean.
/// </summary>
public class PushService : IPushService
{
    /// <summary>Longest token accepted (column length).</summary>
    public const int MaxTokenLength = 512;

    /// <summary>Platforms of the mobile app.</summary>
    public static readonly IReadOnlySet<string> Platforms = new HashSet<string> { "android", "ios" };

    /// <summary>Push text is cut to this length: phones show only the first lines anyway.</summary>
    private const int MaxBody = 240;

    private readonly AppDbContext _db;
    private readonly IPushSender _sender;
    private readonly ILogger<PushService> _logger;
    private readonly int _staleAfterDays;

    /// <summary>Receives the required services through Dependency Injection.</summary>
    public PushService(AppDbContext db, IPushSender sender, ILogger<PushService> logger, IOptions<PushOptions>? options = null)
    {
        _db = db;
        _sender = sender;
        _logger = logger;
        _staleAfterDays = options?.Value.StaleAfterDays ?? new PushOptions().StaleAfterDays;
    }

    /// <inheritdoc />
    public async Task RegisterAsync(long accountId, string token, string platform)
    {
        token = token?.Trim() ?? string.Empty;
        platform = platform?.Trim().ToLowerInvariant() ?? string.Empty;
        if (token.Length == 0 || token.Length > MaxTokenLength)
        {
            throw new BusinessException("The device token is missing or too long.", "Token");
        }
        if (!Platforms.Contains(platform))
        {
            throw new BusinessException("The platform must be android or ios.", "Platform");
        }

        var now = DateTimeHelper.Now;
        var row = await _db.DeviceTokens.FirstOrDefaultAsync(d => d.Token == token);
        if (row == null)
        {
            _db.DeviceTokens.Add(new DeviceToken { AccountId = accountId, Token = token, Platform = platform, LastSeenAt = now });
            _logger.LogInformation("Phone ({Platform}) registered for push by account {AccountId}", platform, accountId);
        }
        else
        {
            if (row.AccountId != accountId)
            {
                // Another user signed in on this phone: from now on the pushes are for the new user only
                _logger.LogInformation("Phone moved from account {Old} to account {New}", row.AccountId, accountId);
                row.AccountId = accountId;
            }
            row.Platform = platform;
            row.LastSeenAt = now;
        }
        await _db.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task UnregisterAsync(long accountId, string token)
    {
        token = token?.Trim() ?? string.Empty;
        var row = await _db.DeviceTokens.FirstOrDefaultAsync(d => d.Token == token && d.AccountId == accountId);
        if (row == null) return;
        _db.DeviceTokens.Remove(row);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Phone removed from push for account {AccountId}", accountId);
    }

    /// <inheritdoc />
    public async Task<int> SendAsync(IReadOnlyCollection<long> accountIds, string title, string message, string? link,
        CancellationToken cancellationToken = default)
    {
        if (!_sender.IsEnabled || accountIds.Count == 0)
        {
            return 0;
        }

        try
        {
            var since = DateTimeHelper.Now.AddDays(-_staleAfterDays);
            var tokens = await _db.DeviceTokens.AsNoTracking()
                .Where(d => accountIds.Contains(d.AccountId) && d.LastSeenAt >= since)
                .Select(d => d.Token)
                .ToListAsync(cancellationToken);
            if (tokens.Count == 0)
            {
                return 0;
            }

            var body = message.Length <= MaxBody ? message : message[..(MaxBody - 1)] + "…";
            var result = await _sender.SendAsync(tokens, new PushMessage(title, body, link), cancellationToken);
            await ForgetAsync(result.InvalidTokens, since, cancellationToken);
            return result.Sent;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A push is a convenience: the in-app notification is already saved
            _logger.LogError(ex, "Push \"{Title}\" could not be sent", title);
            return 0;
        }
    }

    /// <summary>Deletes the rejected tokens and every phone quiet since before <paramref name="staleBefore"/>.</summary>
    private async Task ForgetAsync(IReadOnlyList<string> invalidTokens, DateTime staleBefore, CancellationToken cancellationToken)
    {
        var gone = await _db.DeviceTokens
            .Where(d => invalidTokens.Contains(d.Token) || d.LastSeenAt < staleBefore)
            .ToListAsync(cancellationToken);
        if (gone.Count == 0) return;
        _db.DeviceTokens.RemoveRange(gone);
        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("{Count} phone(s) removed from push (token no longer valid or unused)", gone.Count);
    }
}
