using System.Net;
using System.Text.RegularExpressions;
using InstituteOfFineArts.Web.Data;
using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Helpers;
using InstituteOfFineArts.Web.Infrastructure.Email;
using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.Services.Implementations;
using InstituteOfFineArts.Web.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace InstituteOfFineArts.Tests.Services;

/// <summary>
/// Unit tests for PasswordResetService (task BE1-OPT, forgot password F-M16-01): neutral answer, hashed token,
/// 30-minute expiry, single use, locked accounts and the hourly request limit.
/// </summary>
public class PasswordResetServiceTests
{
    /// <summary>Original password of the test accounts.</summary>
    private const string Password = "Student@123";

    /// <summary>Configured public address used in the links.</summary>
    private const string BaseUrl = "https://ifa.example.com";

    /// <summary>Fake e-mail service that keeps the sent messages in memory.</summary>
    private sealed class FakeEmailService : IEmailService
    {
        /// <summary>Sent messages (recipient, subject, body).</summary>
        public List<(string To, string Subject, string Body)> Sent { get; } = new();

        /// <summary>Records the message and reports success.</summary>
        public Task<bool> SendAsync(string toEmail, string toName, string subject, string htmlBody,
            CancellationToken cancellationToken = default)
        {
            Sent.Add((toEmail, subject, htmlBody));
            return Task.FromResult(true);
        }
    }

    /// <summary>In-memory database with an active student (student03) and a locked one (student10).</summary>
    private static (AppDbContext Db, long ActiveId, long LockedId) CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new AppDbContext(options);
        var hash = BCrypt.Net.BCrypt.HashPassword(Password, workFactor: 4);

        var active = new Account
        {
            Username = "student03", PasswordHash = hash, FullName = "Le Van Nam",
            Email = "student03@ifa.local", Role = Role.STUDENT, Active = true,
        };
        var locked = new Account
        {
            Username = "student10", PasswordHash = hash, FullName = "Pham Thi Lan",
            Email = "student10@ifa.local", Role = Role.STUDENT, Active = false,
        };
        db.Accounts.AddRange(active, locked);
        db.SaveChanges();
        return (db, active.Id, locked.Id);
    }

    /// <summary>Creates the service with the fake e-mail service and an empty logger.</summary>
    private static PasswordResetService CreateService(AppDbContext db, FakeEmailService email)
        => new(db, email, Options.Create(new EmailOptions { AppBaseUrl = BaseUrl }),
            NullLogger<PasswordResetService>.Instance);

    /// <summary>Reads the token from the link in an e-mail body.</summary>
    private static string ExtractToken(string body)
    {
        var match = Regex.Match(WebUtility.HtmlDecode(body), @"/reset-password\?token=([A-Za-z0-9_\-%]+)");
        Assert.True(match.Success, "The e-mail contains no reset link");
        return Uri.UnescapeDataString(match.Groups[1].Value);
    }

    /// <summary>Requests a reset for student03 and returns the token from the e-mail.</summary>
    private static async Task<string> RequestTokenAsync(PasswordResetService service, FakeEmailService email)
    {
        await service.RequestResetAsync("student03", "http://localhost");
        return ExtractToken(email.Sent[^1].Body);
    }

    /// <summary>An active account by user name: one token row, one e-mail to its address with the configured link.</summary>
    [Fact]
    public async Task Request_ActiveAccount_SendsLink()
    {
        var (db, activeId, _) = CreateDb();
        using var _ = db;
        var email = new FakeEmailService();

        await CreateService(db, email).RequestResetAsync("student03", "http://localhost");

        var mail = Assert.Single(email.Sent);
        Assert.Equal("student03@ifa.local", mail.To);
        Assert.Equal(PasswordResetService.EmailSubject, mail.Subject);
        Assert.Contains(BaseUrl + "/reset-password?token=", WebUtility.HtmlDecode(mail.Body));
        var row = Assert.Single(db.PasswordResetTokens);
        Assert.Equal(activeId, row.AccountId);
        Assert.Null(row.UsedAt);
    }

    /// <summary>The e-mail address works as well as the user name.</summary>
    [Fact]
    public async Task Request_ByEmail_SendsLink()
    {
        var (db, _, _) = CreateDb();
        using var _ = db;
        var email = new FakeEmailService();

        await CreateService(db, email).RequestResetAsync("  student03@ifa.local ", "http://localhost");

        Assert.Single(email.Sent);
        Assert.Single(db.PasswordResetTokens);
    }

    /// <summary>No account enumeration: an unknown account ends without error, without e-mail and without a row.</summary>
    [Fact]
    public async Task Request_UnknownAccount_NothingHappens()
    {
        var (db, _, _) = CreateDb();
        using var _ = db;
        var email = new FakeEmailService();

        await CreateService(db, email).RequestResetAsync("nobody", "http://localhost");

        Assert.Empty(email.Sent);
        Assert.Empty(db.PasswordResetTokens);
    }

    /// <summary>A locked account (BR-05) gets no reset link.</summary>
    [Fact]
    public async Task Request_LockedAccount_NothingHappens()
    {
        var (db, _, _) = CreateDb();
        using var _ = db;
        var email = new FakeEmailService();

        await CreateService(db, email).RequestResetAsync("student10", "http://localhost");

        Assert.Empty(email.Sent);
        Assert.Empty(db.PasswordResetTokens);
    }

    /// <summary>The database keeps only the SHA-256 hex of the token, never the token itself.</summary>
    [Fact]
    public async Task Request_StoresHashNotToken()
    {
        var (db, _, _) = CreateDb();
        using var _ = db;
        var email = new FakeEmailService();

        var token = await RequestTokenAsync(CreateService(db, email), email);

        var row = Assert.Single(db.PasswordResetTokens);
        Assert.NotEqual(token, row.TokenHash);
        Assert.Equal(PasswordResetService.HashToken(token), row.TokenHash);
        Assert.Matches("^[0-9a-f]{64}$", row.TokenHash);
        // 32 random bytes in Base64Url = 43 characters
        Assert.Equal(43, token.Length);
    }

    /// <summary>The link expires 30 minutes after the request.</summary>
    [Fact]
    public async Task Request_ExpiresAfter30Minutes()
    {
        var (db, _, _) = CreateDb();
        using var _ = db;
        var email = new FakeEmailService();
        var before = DateTimeHelper.Now;

        await CreateService(db, email).RequestResetAsync("student03", "http://localhost");

        var row = Assert.Single(db.PasswordResetTokens);
        Assert.InRange(row.ExpiresAt, before.AddMinutes(30), DateTimeHelper.Now.AddMinutes(30));
    }

    /// <summary>A new request invalidates the older unused link of the same account.</summary>
    [Fact]
    public async Task Request_Again_OldLinkInvalid()
    {
        var (db, _, _) = CreateDb();
        using var _ = db;
        var email = new FakeEmailService();
        var service = CreateService(db, email);

        var first = await RequestTokenAsync(service, email);
        var second = await RequestTokenAsync(service, email);

        Assert.False(await service.IsTokenValidAsync(first));
        Assert.True(await service.IsTokenValidAsync(second));
    }

    /// <summary>Rate limit: at most 3 reset e-mails per account per hour; the 4th request is ignored silently.</summary>
    [Fact]
    public async Task Request_FourthWithinHour_Ignored()
    {
        var (db, _, _) = CreateDb();
        using var _ = db;
        var email = new FakeEmailService();
        var service = CreateService(db, email);

        for (var i = 0; i < 4; i++)
        {
            await service.RequestResetAsync("student03", "http://localhost");
        }

        Assert.Equal(PasswordResetService.MaxRequestsPerWindow, email.Sent.Count);
        Assert.Equal(PasswordResetService.MaxRequestsPerWindow, db.PasswordResetTokens.Count());
    }

    /// <summary>Requests older than one hour no longer count toward the limit.</summary>
    [Fact]
    public async Task Request_OldRequestsOutsideWindow_Allowed()
    {
        var (db, activeId, _) = CreateDb();
        using var _ = db;
        var email = new FakeEmailService();
        var service = CreateService(db, email);
        for (var i = 0; i < 3; i++)
        {
            await service.RequestResetAsync("student03", "http://localhost");
        }

        // Move the three requests two hours back (CreatedAt is set on insert, so change it afterwards)
        foreach (var row in db.PasswordResetTokens.Where(t => t.AccountId == activeId))
        {
            row.CreatedAt = DateTimeHelper.Now.AddHours(-2);
        }
        db.SaveChanges();

        await service.RequestResetAsync("student03", "http://localhost");

        Assert.Equal(4, email.Sent.Count);
    }

    /// <summary>Without Email:AppBaseUrl the link uses the address of the current request.</summary>
    [Fact]
    public async Task Request_NoAppBaseUrl_UsesRequestAddress()
    {
        var (db, _, _) = CreateDb();
        using var _ = db;
        var email = new FakeEmailService();
        var service = new PasswordResetService(db, email, Options.Create(new EmailOptions()),
            NullLogger<PasswordResetService>.Instance);

        await service.RequestResetAsync("student03", "http://localhost:5142/");

        Assert.Contains("http://localhost:5142/reset-password?token=", WebUtility.HtmlDecode(email.Sent[0].Body));
    }

    /// <summary>A valid link sets a BCrypt hash of the new password and marks the token as used.</summary>
    [Fact]
    public async Task Reset_Valid_ChangesPassword()
    {
        var (db, activeId, _) = CreateDb();
        using var _ = db;
        var email = new FakeEmailService();
        var service = CreateService(db, email);
        var token = await RequestTokenAsync(service, email);

        await service.ResetPasswordAsync(token, "NewPass123");

        var account = db.Accounts.Single(a => a.Id == activeId);
        Assert.True(BCrypt.Net.BCrypt.Verify("NewPass123", account.PasswordHash));
        Assert.NotNull(db.PasswordResetTokens.Single().UsedAt);
    }

    /// <summary>A link works only once.</summary>
    [Fact]
    public async Task Reset_UsedToken_Rejected()
    {
        var (db, _, _) = CreateDb();
        using var _ = db;
        var email = new FakeEmailService();
        var service = CreateService(db, email);
        var token = await RequestTokenAsync(service, email);
        await service.ResetPasswordAsync(token, "NewPass123");

        var ex = await Assert.ThrowsAsync<BusinessException>(() => service.ResetPasswordAsync(token, "Other1234"));

        Assert.Equal(PasswordResetService.InvalidLinkMessage, ex.Message);
        Assert.False(await service.IsTokenValidAsync(token));
    }

    /// <summary>An expired link is refused and the password stays unchanged.</summary>
    [Fact]
    public async Task Reset_ExpiredToken_Rejected()
    {
        var (db, activeId, _) = CreateDb();
        using var _ = db;
        var email = new FakeEmailService();
        var service = CreateService(db, email);
        var token = await RequestTokenAsync(service, email);
        db.PasswordResetTokens.Single().ExpiresAt = DateTimeHelper.Now.AddSeconds(-1);
        db.SaveChanges();

        Assert.False(await service.IsTokenValidAsync(token));
        var ex = await Assert.ThrowsAsync<BusinessException>(() => service.ResetPasswordAsync(token, "NewPass123"));

        Assert.Equal(PasswordResetService.InvalidLinkMessage, ex.Message);
        Assert.True(BCrypt.Net.BCrypt.Verify(Password, db.Accounts.Single(a => a.Id == activeId).PasswordHash));
    }

    /// <summary>An unknown or empty token is refused.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-real-token")]
    public async Task Reset_UnknownToken_Rejected(string? token)
    {
        var (db, _, _) = CreateDb();
        using var _ = db;
        var service = CreateService(db, new FakeEmailService());

        Assert.False(await service.IsTokenValidAsync(token));
        var ex = await Assert.ThrowsAsync<BusinessException>(() => service.ResetPasswordAsync(token, "NewPass123"));
        Assert.Equal(PasswordResetService.InvalidLinkMessage, ex.Message);
    }

    /// <summary>An account locked after the e-mail was sent cannot reset its password (BR-05).</summary>
    [Fact]
    public async Task Reset_AccountLockedMeanwhile_Rejected()
    {
        var (db, activeId, _) = CreateDb();
        using var _ = db;
        var email = new FakeEmailService();
        var service = CreateService(db, email);
        var token = await RequestTokenAsync(service, email);
        db.Accounts.Single(a => a.Id == activeId).Active = false;
        db.SaveChanges();

        Assert.False(await service.IsTokenValidAsync(token));
        var ex = await Assert.ThrowsAsync<BusinessException>(() => service.ResetPasswordAsync(token, "NewPass123"));

        Assert.Equal(PasswordResetService.LockedMessage, ex.Message);
        Assert.Null(db.PasswordResetTokens.Single().UsedAt);
    }

    /// <summary>The service re-checks BR-02: a weak password is refused and the token stays usable.</summary>
    [Fact]
    public async Task Reset_WeakPassword_Rejected()
    {
        var (db, _, _) = CreateDb();
        using var _ = db;
        var email = new FakeEmailService();
        var service = CreateService(db, email);
        var token = await RequestTokenAsync(service, email);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => service.ResetPasswordAsync(token, "short"));

        Assert.Equal("NewPassword", ex.Field);
        Assert.True(await service.IsTokenValidAsync(token));
    }
}
