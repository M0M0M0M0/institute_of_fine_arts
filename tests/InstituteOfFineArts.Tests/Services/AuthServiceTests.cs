using System.Security.Claims;
using InstituteOfFineArts.Web.Data;
using InstituteOfFineArts.Web.Infrastructure.Security;
using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.Services.Implementations;
using InstituteOfFineArts.Web.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace InstituteOfFineArts.Tests.Services;

/// <summary>
/// Unit tests for AuthService (task BE1-02): correct/incorrect login, locked account, claims, home page by role.
/// Uses an in-memory database (EF Core InMemory), so no SQL Server is needed.
/// </summary>
public class AuthServiceTests
{
    /// <summary>Fake scheme name used when creating the ClaimsIdentity in tests.</summary>
    private const string Scheme = "TestCookie";

    /// <summary>Shared password of the test accounts.</summary>
    private const string Password = "Student@123";

    /// <summary>
    /// Creates an AppDbContext backed by an in-memory database (a separate one per test) with 2 students and 1 staff member.
    /// </summary>
    private static AppDbContext CreateDbWithAccounts()
    {
        // Random database name so tests do not affect each other
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new AppDbContext(options);

        // Hash the password once (low work factor to keep tests fast) and share it
        var hash = BCrypt.Net.BCrypt.HashPassword(Password, workFactor: 4);

        // Active student, locked student, staff member
        db.StudentProfiles.Add(NewStudent("student01", hash, active: true, code: "HV0001"));
        db.StudentProfiles.Add(NewStudent("student10", hash, active: false, code: "HV0010"));
        db.StaffProfiles.Add(new StaffProfile
        {
            StaffCode = "NV0003",
            JoinedDate = new DateOnly(2024, 1, 1),
            Account = new Account
            {
                Username = "staff01", PasswordHash = hash, FullName = "Le Minh Hoang",
                Email = "staff01@ifa.local", Role = Role.STAFF, Active = true,
            },
        });
        db.SaveChanges();
        return db;
    }

    /// <summary>Creates a student profile with its account for the test data.</summary>
    private static StudentProfile NewStudent(string username, string hash, bool active, string code) => new()
    {
        StudentCode = code,
        EnrollmentDate = new DateOnly(2025, 1, 1),
        CourseName = "Painting",
        Account = new Account
        {
            Username = username, PasswordHash = hash, FullName = "Nguyen Minh Khoa",
            Email = $"{username}@ifa.local", Role = Role.STUDENT, Active = active,
        },
    };

    /// <summary>Creates the AuthService with an empty logger.</summary>
    private static AuthService CreateService(AppDbContext db) => new(db, NullLogger<AuthService>.Instance);

    /// <summary>Correct username and password → success, full claims, last_login_at updated.</summary>
    [Fact]
    public async Task Login_CorrectPassword_SucceedsWithClaims()
    {
        using var db = CreateDbWithAccounts();
        var service = CreateService(db);

        var result = await service.LoginAsync("student01", Password, Scheme);

        // Successful result with user information
        Assert.Equal(LoginStatus.Success, result.Status);
        Assert.NotNull(result.Principal);
        var user = result.Principal!;
        Assert.Equal("student01", user.Identity!.Name);
        Assert.True(user.IsInRole("STUDENT"));

        // Account id and student profile id are readable through the helper methods
        var account = await db.Accounts.Include(a => a.StudentProfile).SingleAsync(a => a.Username == "student01");
        Assert.Equal(account.Id, user.GetAccountId());
        Assert.Equal(account.StudentProfile!.Id, user.GetProfileId());
        Assert.Equal("Nguyen Minh Khoa", user.GetFullName());

        // The login time has been recorded
        Assert.NotNull(account.LastLoginAt);
    }

    /// <summary>A username with extra spaces still logs in.</summary>
    [Fact]
    public async Task Login_UsernameWithSpaces_Succeeds()
    {
        using var db = CreateDbWithAccounts();
        var result = await CreateService(db).LoginAsync("  student01 ", Password, Scheme);
        Assert.Equal(LoginStatus.Success, result.Status);
    }

    /// <summary>Wrong password → invalid credentials, no principal (TC-M01-03).</summary>
    [Fact]
    public async Task Login_WrongPassword_ReturnsInvalidCredentials()
    {
        using var db = CreateDbWithAccounts();
        var result = await CreateService(db).LoginAsync("student01", "wrong123", Scheme);
        Assert.Equal(LoginStatus.InvalidCredentials, result.Status);
        Assert.Null(result.Principal);
    }

    /// <summary>Unknown account → same result as a wrong password (TC-M01-04).</summary>
    [Fact]
    public async Task Login_UnknownAccount_SameAsWrongPassword()
    {
        using var db = CreateDbWithAccounts();
        var result = await CreateService(db).LoginAsync("doesnotexist", Password, Scheme);
        Assert.Equal(LoginStatus.InvalidCredentials, result.Status);
    }

    /// <summary>SQL injection string as username → login fails without errors (TC-M01-06).</summary>
    [Fact]
    public async Task Login_SqlInjection_Fails()
    {
        using var db = CreateDbWithAccounts();
        var result = await CreateService(db).LoginAsync("' OR '1'='1' --", "x", Scheme);
        Assert.Equal(LoginStatus.InvalidCredentials, result.Status);
    }

    /// <summary>Locked account with the correct password → locked, cannot log in (TC-M01-07).</summary>
    [Fact]
    public async Task Login_LockedAccount_ReturnsLocked()
    {
        using var db = CreateDbWithAccounts();
        var result = await CreateService(db).LoginAsync("student10", Password, Scheme);
        Assert.Equal(LoginStatus.Locked, result.Status);
        Assert.Null(result.Principal);
    }

    /// <summary>Locked account with a wrong password → only "invalid credentials" (the locked state is not revealed).</summary>
    [Fact]
    public async Task Login_LockedAccountWrongPassword_DoesNotRevealLock()
    {
        using var db = CreateDbWithAccounts();
        var result = await CreateService(db).LoginAsync("student10", "wrong123", Scheme);
        Assert.Equal(LoginStatus.InvalidCredentials, result.Status);
    }

    /// <summary>Staff login → ProfileId is the staff_profile id.</summary>
    [Fact]
    public async Task Login_Staff_ProfileIdIsStaffProfile()
    {
        using var db = CreateDbWithAccounts();
        var result = await CreateService(db).LoginAsync("staff01", Password, Scheme);
        var staff = await db.StaffProfiles.SingleAsync(s => s.StaffCode == "NV0003");
        Assert.Equal(staff.Id, result.Principal!.GetProfileId());
    }

    /// <summary>Each role goes to its own home page (TC-M01-02).</summary>
    [Theory]
    [InlineData("ADMIN", "/admin")]
    [InlineData("MANAGER", "/manager")]
    [InlineData("STAFF", "/staff")]
    [InlineData("STUDENT", "/student")]
    public void GetHomePath_ByRole(string role, string expected)
    {
        using var db = CreateDbWithAccounts();
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, role) }, Scheme));
        Assert.Equal(expected, CreateService(db).GetHomePath(user));
    }
}
