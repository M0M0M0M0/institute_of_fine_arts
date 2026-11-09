using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using InstituteOfFineArts.Web.Data;
using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Helpers;
using InstituteOfFineArts.Web.Infrastructure.Email;
using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Services.Interfaces;
using InstituteOfFineArts.Web.Validation;
using InstituteOfFineArts.Web.ViewModels.Account;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace InstituteOfFineArts.Web.Services.Implementations;

/// <summary>
/// Forgot password with a one-time e-mail link (F-M16-01, task BE1-OPT).
/// A random 32-byte token is e-mailed; only its SHA-256 hash is stored, so a database leak does not reveal usable
/// links. A token is valid for 30 minutes, works once, and requesting a new one invalidates the older ones.
/// </summary>
public class PasswordResetService : IPasswordResetService
{
    /// <summary>Neutral message shown after every request, whether or not the account exists (no account enumeration).</summary>
    public const string RequestAcceptedMessage =
        "If the account exists, we have sent a link to reset the password to its e-mail address. The link is valid for 30 minutes.";

    /// <summary>Message when the reset link does not exist, was already used or has expired.</summary>
    public const string InvalidLinkMessage =
        "This password reset link is invalid or has expired. Please request a new one.";

    /// <summary>Message when the account of the link has been locked in the meantime (BR-05).</summary>
    public const string LockedMessage = "Your account has been locked. Please contact the administrator.";

    /// <summary>Message after the password has been reset.</summary>
    public const string PasswordResetMessage = "Your password has been reset. Please log in with your new password.";

    /// <summary>Subject of the reset e-mail.</summary>
    public const string EmailSubject = "Reset your password – Institute of Fine Arts";

    /// <summary>How long a reset link stays valid.</summary>
    public static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(30);

    /// <summary>Maximum number of reset e-mails per account within <see cref="RateLimitWindow"/>.</summary>
    public const int MaxRequestsPerWindow = 3;

    /// <summary>Window of the per-account request limit.</summary>
    public static readonly TimeSpan RateLimitWindow = TimeSpan.FromHours(1);

    /// <summary>Number of random bytes in a token (256 bits).</summary>
    private const int TokenBytes = 32;

    /// <summary>Database access.</summary>
    private readonly AppDbContext _db;

    /// <summary>Sends the reset link.</summary>
    private readonly IEmailService _emailService;

    /// <summary>E-mail settings (AppBaseUrl for the link).</summary>
    private readonly EmailOptions _emailOptions;

    /// <summary>Logs requests and resets (never tokens or passwords).</summary>
    private readonly ILogger<PasswordResetService> _logger;

    /// <summary>Receives the required services through Dependency Injection.</summary>
    public PasswordResetService(AppDbContext db, IEmailService emailService, IOptions<EmailOptions> emailOptions,
        ILogger<PasswordResetService> logger)
    {
        _db = db;
        _emailService = emailService;
        _emailOptions = emailOptions.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task RequestResetAsync(string userNameOrEmail, string requestBaseUrl,
        CancellationToken cancellationToken = default)
    {
        // Match the user name or the e-mail; the LINQ query is parameterized
        var input = (userNameOrEmail ?? string.Empty).Trim();
        if (input.Length == 0)
        {
            return;
        }
        var account = await _db.Accounts
            .FirstOrDefaultAsync(a => a.Username == input || a.Email == input, cancellationToken);

        // Unknown or locked account: do nothing, the caller shows the same neutral message
        if (account == null || !account.Active)
        {
            _logger.LogWarning("Password reset requested for an unknown or locked account: {Input}", input);
            return;
        }

        // Simple rate limit: at most 3 e-mails per account per hour (protects the mailbox and the SMTP quota)
        var now = DateTimeHelper.Now;
        var recentRequests = await _db.PasswordResetTokens
            .CountAsync(t => t.AccountId == account.Id && t.CreatedAt >= now - RateLimitWindow, cancellationToken);
        if (recentRequests >= MaxRequestsPerWindow)
        {
            _logger.LogWarning("Password reset request limit reached for {Username}", account.Username);
            return;
        }

        // Only the newest link works: expire the older unused ones (UsedAt stays reserved for real use)
        var openTokens = await _db.PasswordResetTokens
            .Where(t => t.AccountId == account.Id && t.UsedAt == null && t.ExpiresAt > now)
            .ToListAsync(cancellationToken);
        foreach (var old in openTokens)
        {
            old.ExpiresAt = now;
        }

        // New random token: the e-mail gets the token, the database only its hash
        var token = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(TokenBytes));
        _db.PasswordResetTokens.Add(new PasswordResetToken
        {
            AccountId = account.Id,
            TokenHash = HashToken(token),
            ExpiresAt = now + TokenLifetime,
        });
        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Password reset token created for {Username}", account.Username);

        // E-mail errors are logged by the e-mail service; the request still ends with the neutral message
        var link = $"{ResolveBaseUrl(requestBaseUrl)}/reset-password?token={Uri.EscapeDataString(token)}";
        await _emailService.SendAsync(account.Email, account.FullName, EmailSubject,
            BuildEmailBody(account.FullName, account.Username, link), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> IsTokenValidAsync(string? token)
    {
        var resetToken = await FindUsableTokenAsync(token, tracking: false);
        return resetToken is { Account.Active: true };
    }

    /// <inheritdoc />
    public async Task ResetPasswordAsync(string? token, string newPassword)
    {
        // The link must exist, be unused and not expired
        var resetToken = await FindUsableTokenAsync(token, tracking: true)
            ?? throw new BusinessException(InvalidLinkMessage);

        // A locked account cannot get back in through a reset link (BR-05)
        var account = resetToken.Account;
        if (!account.Active)
        {
            throw new BusinessException(LockedMessage);
        }

        // BR-02 is also checked on the form; checked again here so no caller can skip it
        if (!StrongPasswordAttribute.IsStrong(newPassword))
        {
            throw new BusinessException(new StrongPasswordAttribute().FormatErrorMessage("New password"),
                nameof(ResetPasswordVm.NewPassword));
        }

        // Same BCrypt hashing as the rest of the application; the token can be used only once
        var now = DateTimeHelper.Now;
        account.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        resetToken.UsedAt = now;

        // Any other open link of this account stops working as well
        var otherTokens = await _db.PasswordResetTokens
            .Where(t => t.AccountId == account.Id && t.Id != resetToken.Id && t.UsedAt == null && t.ExpiresAt > now)
            .ToListAsync();
        foreach (var other in otherTokens)
        {
            other.ExpiresAt = now;
        }

        await _db.SaveChangesAsync();
        _logger.LogInformation("Password reset with e-mail link: {Username}", account.Username);
    }

    /// <summary>SHA-256 of the token as 64 lowercase hex characters (the value stored in password_reset_token).</summary>
    /// <param name="token">Token from the e-mail link</param>
    public static string HashToken(string token)
        => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    /// <summary>Loads the token with its account when it exists, is unused and has not expired; otherwise null.</summary>
    private async Task<PasswordResetToken?> FindUsableTokenAsync(string? token, bool tracking)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        // Look the link up by its hash; the plain token is never stored
        var hash = HashToken(token.Trim());
        var now = DateTimeHelper.Now;
        var query = _db.PasswordResetTokens.Include(t => t.Account).AsQueryable();
        if (!tracking)
        {
            query = query.AsNoTracking();
        }
        return await query.FirstOrDefaultAsync(t => t.TokenHash == hash && t.UsedAt == null && t.ExpiresAt > now);
    }

    /// <summary>Configured public address (Email:AppBaseUrl), or the address of the current request when not configured.</summary>
    private string ResolveBaseUrl(string requestBaseUrl)
        => (string.IsNullOrWhiteSpace(_emailOptions.AppBaseUrl) ? requestBaseUrl : _emailOptions.AppBaseUrl).TrimEnd('/');

    /// <summary>HTML body of the reset e-mail; user data is HTML-encoded.</summary>
    private static string BuildEmailBody(string fullName, string userName, string link)
    {
        var html = HtmlEncoder.Default;
        var minutes = (int)TokenLifetime.TotalMinutes;
        return $"""
            <p>Hello {html.Encode(fullName)},</p>
            <p>We received a request to reset the password of your account <strong>{html.Encode(userName)}</strong>
            at the Institute of Fine Arts.</p>
            <p><a href="{html.Encode(link)}">Reset my password</a></p>
            <p>The link is valid for {minutes} minutes and can be used once. If you did not ask for it, ignore this
            e-mail: your password stays unchanged.</p>
            """;
    }
}
