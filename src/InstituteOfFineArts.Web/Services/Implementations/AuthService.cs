using System.Security.Claims;
using InstituteOfFineArts.Web.Data;
using InstituteOfFineArts.Web.Helpers;
using InstituteOfFineArts.Web.Infrastructure.Security;
using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace InstituteOfFineArts.Web.Services.Implementations;

/// <summary>
/// Authenticates users: compares passwords with BCrypt, blocks locked accounts, builds the claims for the login cookie.
/// </summary>
public class AuthService : IAuthService
{
    /// <summary>
    /// Dummy BCrypt hash used when the account does not exist: BCrypt.Verify still runs so the response time
    /// is the same as for a wrong password → an attacker cannot tell which usernames exist.
    /// </summary>
    private static readonly string DummyHash = BCrypt.Net.BCrypt.HashPassword("not-a-real-password");

    /// <summary>Database access.</summary>
    private readonly AppDbContext _db;

    /// <summary>Logs successful / failed logins.</summary>
    private readonly ILogger<AuthService> _logger;

    /// <summary>Receives the required services through Dependency Injection.</summary>
    public AuthService(AppDbContext db, ILogger<AuthService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<LoginResult> LoginAsync(string username, string password, string authenticationScheme)
    {
        // Trim the username; the LINQ query is parameterized (protects against SQL injection)
        var name = (username ?? string.Empty).Trim();

        // Too many wrong passwords for this user name lately: refuse without checking, so guessing stays slow. Same
        // answer whether or not the account exists (no user name enumeration)
        if (await TooManyFailuresAsync(name))
        {
            _logger.LogWarning("Login blocked: too many failed attempts for {Username}", name);
            return new LoginResult(LoginStatus.TooManyAttempts);
        }
        var account = await _db.Accounts
            .Include(a => a.StaffProfile)
            .Include(a => a.StudentProfile)
            .FirstOrDefaultAsync(a => a.Username == name);

        // No such account: still run BCrypt with the dummy hash so the timing matches a wrong password
        if (account == null)
        {
            BCrypt.Net.BCrypt.Verify(password ?? string.Empty, DummyHash);
            _logger.LogWarning("Login failed: unknown username {Username}", name);
            return new LoginResult(LoginStatus.InvalidCredentials);
        }

        // Wrong password
        if (!BCrypt.Net.BCrypt.Verify(password ?? string.Empty, account.PasswordHash))
        {
            _logger.LogWarning("Login failed: wrong password for {Username}", name);
            return new LoginResult(LoginStatus.InvalidCredentials);
        }

        // Correct password but the account is locked (BR-05) – "locked" is only revealed after a correct password
        if (!account.Active)
        {
            _logger.LogWarning("Login blocked: account {Username} is locked", name);
            return new LoginResult(LoginStatus.Locked);
        }

        // Record the time of this login
        account.LastLoginAt = DateTimeHelper.Now;
        await _db.SaveChangesAsync();

        // Profile id: staff_profile for ADMIN/STAFF/TEACHER, student_profile for STUDENT
        long? profileId = account.Role == Role.STUDENT ? account.StudentProfile?.Id : account.StaffProfile?.Id;

        // Claims stored in the cookie (convention in Infrastructure/Security/ClaimsPrincipalExtensions)
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, account.Id.ToString()),
            new(ClaimTypes.Name, account.Username),
            new(ClaimTypes.GivenName, account.FullName),
            new(ClaimTypes.Role, account.Role.ToString()),
            // Ends this session when the account is locked, its role or its password changes (SessionValidator)
            new(ClaimsPrincipalExtensions.SecurityStampClaim, account.SecurityStamp),
        };
        if (profileId.HasValue)
        {
            claims.Add(new Claim(ClaimsPrincipalExtensions.ProfileIdClaim, profileId.Value.ToString()));
        }

        _logger.LogInformation("Login succeeded: {Username} ({Role})", account.Username, account.Role);
        var identity = new ClaimsIdentity(claims, authenticationScheme);
        return new LoginResult(LoginStatus.Success, new ClaimsPrincipal(identity));
    }

    /// <summary>Wrong passwords allowed for one user name within <see cref="FailedLoginWindow"/>.</summary>
    public const int MaxFailedLogins = 5;

    /// <summary>How long failed attempts count, and so how long sign-in stays blocked after the last one.</summary>
    public static readonly TimeSpan FailedLoginWindow = TimeSpan.FromMinutes(15);

    /// <summary>
    /// True when <paramref name="name"/> had <see cref="MaxFailedLogins"/> failed logins (web or app, read from the
    /// audit log) within the last <see cref="FailedLoginWindow"/> and since its last successful login.
    /// </summary>
    private async Task<bool> TooManyFailuresAsync(string name)
    {
        if (name.Length == 0) return false;
        var since = DateTimeHelper.Now - FailedLoginWindow;
        var lastSuccess = await _db.AuditLogs.AsNoTracking()
            .Where(l => l.Username == name && l.Action == AuditLogService.LoginAction && l.CreatedAt >= since)
            .MaxAsync(l => (DateTime?)l.CreatedAt);
        if (lastSuccess > since) since = lastSuccess.Value;
        var failures = await _db.AuditLogs.AsNoTracking()
            .CountAsync(l => l.Username == name && l.Action == AuditLogService.LoginFailedAction && l.CreatedAt > since);
        return failures >= MaxFailedLogins;
    }

    /// <inheritdoc />
    public string GetHomePath(ClaimsPrincipal user)
    {
        // Each role has its own Area; unknown roles go to the home page
        if (user.IsInRole(RoleNames.Admin)) return "/admin";
        if (user.IsInRole(RoleNames.Teacher)) return "/teacher";
        if (user.IsInRole(RoleNames.Staff)) return "/staff";
        if (user.IsInRole(RoleNames.Student)) return "/student";
        return "/";
    }
}
