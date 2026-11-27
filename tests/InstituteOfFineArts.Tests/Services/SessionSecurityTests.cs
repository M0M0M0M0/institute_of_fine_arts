using System.Security.Claims;
using InstituteOfFineArts.Web.Data;
using InstituteOfFineArts.Web.Helpers;
using InstituteOfFineArts.Web.Infrastructure.Push;
using InstituteOfFineArts.Web.Infrastructure.Security;
using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.Services.Implementations;
using InstituteOfFineArts.Web.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace InstituteOfFineArts.Tests.Services;

/// <summary>
/// Sessions and accounts (review 26/11/2026, cluster 1): locking an account, changing its role or password ends every
/// session at once (security stamp, A1); wrong passwords block sign-in for a while (B7); pushes only go to active
/// accounts (B5); the sign-in and password pages are written to the audit log, the phone registration is not (B6).
/// </summary>
public class SessionSecurityTests
{
    private const string Password = "Student@123";

    private readonly AppDbContext _db =
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private readonly Account _anna;

    public SessionSecurityTests()
    {
        _anna = new Account
        {
            Username = "student01", FullName = "Anna", Email = "a@ifa.local", Role = Role.STUDENT,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password, workFactor: 4),
        };
        _db.StudentProfiles.Add(new StudentProfile
        {
            StudentCode = "HV0001", EnrollmentDate = new DateOnly(2025, 9, 1), Account = _anna,
        });
        _db.SaveChanges();
    }

    private async Task<ClaimsPrincipal> SignInAsync()
    {
        var result = await new AuthService(_db, NullLogger<AuthService>.Instance).LoginAsync("student01", Password, "Test");
        Assert.Equal(LoginStatus.Success, result.Status);
        return result.Principal!;
    }

    /// <summary>A fresh sign-in is valid; locking the account ends it, unlocking does not bring it back.</summary>
    [Fact]
    public async Task LockingTheAccount_EndsTheSession()
    {
        var session = await SignInAsync();
        Assert.True(await SessionValidator.IsCurrentAsync(_db, session));

        _anna.Active = false;
        await _db.SaveChangesAsync();
        Assert.False(await SessionValidator.IsCurrentAsync(_db, session));

        _anna.Active = true;
        await _db.SaveChangesAsync();
        Assert.False(await SessionValidator.IsCurrentAsync(_db, session));
    }

    /// <summary>A new role or a new password also ends every session; editing the name does not.</summary>
    [Theory]
    [InlineData("role", false)]
    [InlineData("password", false)]
    [InlineData("name", true)]
    public async Task ChangingRoleOrPassword_EndsTheSession(string change, bool stillValid)
    {
        var session = await SignInAsync();
        switch (change)
        {
            case "role": _anna.Role = Role.TEACHER; break;
            case "password": _anna.PasswordHash = BCrypt.Net.BCrypt.HashPassword("Another@2026", workFactor: 4); break;
            default: _anna.FullName = "Anna Nguyen"; break;
        }
        await _db.SaveChangesAsync();
        Assert.Equal(stillValid, await SessionValidator.IsCurrentAsync(_db, session));
    }

    /// <summary>A session from before the stamp existed (no claim) ends once.</summary>
    [Fact]
    public async Task SessionWithoutStamp_Ends()
    {
        var old = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, _anna.Id.ToString())], "Test"));
        Assert.False(await SessionValidator.IsCurrentAsync(_db, old));
    }

    /// <summary>Changing one's own password returns the new stamp, so the page can keep its own session.</summary>
    [Fact]
    public async Task ChangeOwnPassword_ReturnsTheNewStamp()
    {
        var before = _anna.SecurityStamp;
        var stamp = await new ProfileService(_db, NullLogger<ProfileService>.Instance)
            .ChangePasswordAsync(_anna.Id, Password, "NewPass2026");
        Assert.NotEqual(before, stamp);
        Assert.Equal(stamp, (await _db.Accounts.AsNoTracking().SingleAsync(a => a.Id == _anna.Id)).SecurityStamp);
    }

    /// <summary>
    /// After 5 wrong passwords in 15 minutes even the right one is refused; failures before the last successful
    /// sign-in and older than 15 minutes do not count; an unknown user name is treated the same way.
    /// </summary>
    [Fact]
    public async Task TooManyWrongPasswords_BlockSignIn()
    {
        var auth = new AuthService(_db, NullLogger<AuthService>.Instance);
        void Log(string user, string action, int minutesAgo) => _db.AuditLogs.Add(new AuditLog
        {
            Username = user, Action = action, CreatedAt = DateTimeHelper.Now.AddMinutes(-minutesAgo),
        });

        // Old failures, and failures before a successful sign-in, do not count
        for (var i = 0; i < 5; i++) Log("student01", AuditLogService.LoginFailedAction, 30);
        for (var i = 0; i < 4; i++) Log("student01", AuditLogService.LoginFailedAction, 10);
        Log("student01", AuditLogService.LoginAction, 9);
        for (var i = 0; i < 4; i++) Log("student01", AuditLogService.LoginFailedAction, 5);
        await SaveLogsAsync();
        Assert.Equal(LoginStatus.Success, (await auth.LoginAsync("student01", Password, "Test")).Status);

        Log("student01", AuditLogService.LoginFailedAction, 1);
        await SaveLogsAsync();
        Assert.Equal(LoginStatus.TooManyAttempts, (await auth.LoginAsync("student01", Password, "Test")).Status);

        for (var i = 0; i < 5; i++) Log("nobody", AuditLogService.LoginFailedAction, 1);
        await SaveLogsAsync();
        Assert.Equal(LoginStatus.TooManyAttempts, (await auth.LoginAsync("nobody", "whatever1", "Test")).Status);
    }

    /// <summary>Saves the audit rows without the automatic created time overriding the test times.</summary>
    private async Task SaveLogsAsync()
    {
        var times = _db.ChangeTracker.Entries<AuditLog>().Where(e => e.State == EntityState.Added)
            .Select(e => (e.Entity, e.Entity.CreatedAt)).ToList();
        await _db.SaveChangesAsync();
        foreach (var (log, at) in times) log.CreatedAt = at;
        await _db.SaveChangesAsync();
    }

    /// <summary>A locked account gets no push, even with a registered phone.</summary>
    [Fact]
    public async Task Push_SkipsLockedAccounts()
    {
        var sender = new Mock<IPushSender>();
        sender.SetupGet(s => s.IsEnabled).Returns(true);
        sender.Setup(s => s.SendAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<PushMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<string> t, PushMessage _, CancellationToken _) => new PushResult(t.Count, []));
        var push = new PushService(_db, sender.Object, NullLogger<PushService>.Instance);
        await push.RegisterAsync(_anna.Id, "tok-anna", "android");
        Assert.Equal(1, await push.SendAsync([_anna.Id], "Hello", "Text", null));

        _anna.Active = false;
        await _db.SaveChangesAsync();
        Assert.Equal(0, await push.SendAsync([_anna.Id], "Hello", "Text", null));
    }

    /// <summary>The password reset pages are named in the audit log.</summary>
    [Theory]
    [InlineData("/forgot-password", "Asked for a password reset link")]
    [InlineData("/reset-password", "Set a new password with a reset link")]
    [InlineData("/profile/change-password", "Changed their own password")]
    public async Task PasswordPages_HaveAReadableAuditSentence(string path, string summary)
    {
        var target = await new AuditTargetService(_db).ResolveAsync("POST", path, null);
        Assert.Equal(summary, target.Summary);
        Assert.Equal(AuditCategory.ACCOUNT, target.Category);
    }
}
