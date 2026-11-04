using InstituteOfFineArts.Web.Data;
using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.Services.Implementations;
using Microsoft.EntityFrameworkCore;

namespace InstituteOfFineArts.Tests.Services;

/// <summary>
/// AdminDashboardService (task BE2-04): counts of accounts, students, competitions (calculated status),
/// submissions, exhibitions and the most recent logins on a database with known data.
/// </summary>
public class AdminDashboardServiceTests
{
    /// <summary>Fixed "today" used by every test.</summary>
    private static readonly DateOnly Today = new(2026, 6, 15);

    /// <summary>Base time for the login times.</summary>
    private static readonly DateTime Noon = new(2026, 6, 15, 12, 0, 0);

    /// <summary>
    /// Database with: 3 staff + 1 manager + 2 admins (1 staff and 1 admin locked); 4 students (1 locked);
    /// competitions UPCOMING, ONGOING, 2 × CLOSED (one with a stale stored status) and GRADED;
    /// 6 submissions, 2 of them without evaluation in closed competitions; exhibitions upcoming, ongoing, 2 × finished.
    /// </summary>
    private static AppDbContext CreateDb()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        // Accounts: login minutes after noon decide the order of the "recent logins" table
        Account Account(string user, Role role, bool active = true, int? loginMinutes = null) => new()
        {
            Username = user, FullName = user.ToUpperInvariant(), Email = $"{user}@ifa.local", Role = role, Active = active,
            PasswordHash = "x", LastLoginAt = loginMinutes is { } m ? Noon.AddMinutes(m) : null,
        };
        db.Accounts.AddRange(
            Account("staff01", Role.STAFF, loginMinutes: 10),
            Account("staff02", Role.STAFF, loginMinutes: 20),
            Account("staff03", Role.STAFF, active: false),
            Account("manager01", Role.MANAGER, loginMinutes: 30),
            Account("admin01", Role.ADMIN, loginMinutes: 60),
            Account("admin02", Role.ADMIN, active: false));

        // Students: 4 profiles with different enrollment statuses
        StudentProfile Student(string user, EnrollmentStatus status, bool active = true, int? loginMinutes = null) => new()
        {
            StudentCode = user.ToUpperInvariant(), CourseName = "Fine Arts", EnrollmentDate = new DateOnly(2025, 9, 1),
            EnrollmentStatus = status, Account = Account(user, Role.STUDENT, active, loginMinutes),
        };
        db.StudentProfiles.AddRange(
            Student("student01", EnrollmentStatus.STUDYING, loginMinutes: 50),
            Student("student02", EnrollmentStatus.STUDYING, loginMinutes: 40),
            Student("student03", EnrollmentStatus.GRADUATED, loginMinutes: 5),
            Student("student04", EnrollmentStatus.SUSPENDED, active: false));

        // Competitions: C-CLOSED-STALE is stored as UPCOMING but has ended (counts as CLOSED)
        Competition Competition(long id, int start, int end, CompetitionStatus stored) => new()
        {
            Id = id, Code = $"C{id}", Title = $"C{id}", StartDate = Today.AddDays(start), EndDate = Today.AddDays(end), Status = stored, CreatedBy = 1,
        };
        db.Competitions.AddRange(
            Competition(1, 5, 10, CompetitionStatus.UPCOMING),
            Competition(2, -5, 5, CompetitionStatus.ONGOING),
            Competition(3, -20, -10, CompetitionStatus.CLOSED),
            Competition(4, -20, -1, CompetitionStatus.UPCOMING),
            Competition(5, -60, -30, CompetitionStatus.GRADED));

        // Submissions: closed competitions 3 and 4 have 3 entries, one evaluated; the others do not count as waiting
        Submission Submission(long id, long competitionId, bool evaluated) => new()
        {
            Id = id, CompetitionId = competitionId, StudentId = 1, Title = $"S{id}", ImagePath = "x.jpg", OriginalFileName = "x.jpg",
            TextContent = "t", Description = "d", Evaluation = evaluated ? new Evaluation { EvaluatedBy = 1, Rating = Rating.GOOD } : null,
        };
        db.Submissions.AddRange(
            Submission(1, 2, evaluated: false),
            Submission(2, 3, evaluated: true),
            Submission(3, 3, evaluated: false),
            Submission(4, 4, evaluated: false),
            Submission(5, 5, evaluated: true),
            Submission(6, 5, evaluated: false));

        // Exhibitions: stored statuses are wrong on purpose, the dates decide
        Exhibition Exhibition(int start, int end) => new()
        {
            Name = $"X{start}", Venue = "Hall", City = "Hanoi", StartDate = Today.AddDays(start), EndDate = Today.AddDays(end),
            Status = ExhibitionStatus.UPCOMING, CreatedBy = 1,
        };
        db.Exhibitions.AddRange(Exhibition(3, 9), Exhibition(-1, 1), Exhibition(-9, -2), Exhibition(-30, -20));

        db.SaveChanges();
        return db;
    }

    /// <summary>Staff accounts are counted per role and per active/locked; students are not included.</summary>
    [Fact]
    public async Task Get_StaffCounts()
    {
        using var db = CreateDb();

        var vm = await new AdminDashboardService(db).GetAsync(Today);

        Assert.Equal(3, vm.StaffByRole[Role.STAFF]);
        Assert.Equal(1, vm.StaffByRole[Role.MANAGER]);
        Assert.Equal(2, vm.StaffByRole[Role.ADMIN]);
        Assert.False(vm.StaffByRole.ContainsKey(Role.STUDENT));
        Assert.Equal(4, vm.StaffActive);
        Assert.Equal(2, vm.StaffLocked);
        Assert.Equal(6, vm.StaffTotal);
    }

    /// <summary>Students: active/locked and every enrollment status (0 when nobody has it).</summary>
    [Fact]
    public async Task Get_StudentCounts()
    {
        using var db = CreateDb();

        var vm = await new AdminDashboardService(db).GetAsync(Today);

        Assert.Equal(3, vm.StudentsActive);
        Assert.Equal(1, vm.StudentsLocked);
        Assert.Equal(4, vm.StudentsTotal);
        Assert.Equal(2, vm.StudentsByEnrollment[EnrollmentStatus.STUDYING]);
        Assert.Equal(1, vm.StudentsByEnrollment[EnrollmentStatus.GRADUATED]);
        Assert.Equal(1, vm.StudentsByEnrollment[EnrollmentStatus.SUSPENDED]);
        Assert.Equal(0, vm.StudentsByEnrollment[EnrollmentStatus.DROPPED]);
    }

    /// <summary>BR-07: competitions are counted by calculated status (a stale UPCOMING that has ended counts as CLOSED).</summary>
    [Fact]
    public async Task Get_CompetitionsByCalculatedStatus()
    {
        using var db = CreateDb();

        var vm = await new AdminDashboardService(db).GetAsync(Today);

        Assert.Equal(1, vm.CompetitionsByStatus[CompetitionStatus.UPCOMING]);
        Assert.Equal(1, vm.CompetitionsByStatus[CompetitionStatus.ONGOING]);
        Assert.Equal(2, vm.CompetitionsByStatus[CompetitionStatus.CLOSED]);
        Assert.Equal(1, vm.CompetitionsByStatus[CompetitionStatus.GRADED]);
    }

    /// <summary>Submissions: total, and the ones without evaluation only in CLOSED competitions (not ONGOING, not GRADED).</summary>
    [Fact]
    public async Task Get_SubmissionCounts()
    {
        using var db = CreateDb();

        var vm = await new AdminDashboardService(db).GetAsync(Today);

        Assert.Equal(6, vm.TotalSubmissions);
        Assert.Equal(2, vm.SubmissionsAwaitingEvaluation);
    }

    /// <summary>BR-42: exhibitions are counted by the status calculated from the dates.</summary>
    [Fact]
    public async Task Get_ExhibitionsByCalculatedStatus()
    {
        using var db = CreateDb();

        var vm = await new AdminDashboardService(db).GetAsync(Today);

        Assert.Equal(1, vm.ExhibitionsByStatus[ExhibitionStatus.UPCOMING]);
        Assert.Equal(1, vm.ExhibitionsByStatus[ExhibitionStatus.ONGOING]);
        Assert.Equal(2, vm.ExhibitionsByStatus[ExhibitionStatus.FINISHED]);
    }

    /// <summary>Recent logins: the 5 newest of any role, newest first; accounts that never logged in are skipped.</summary>
    [Fact]
    public async Task Get_RecentLogins_NewestFive()
    {
        using var db = CreateDb();

        var vm = await new AdminDashboardService(db).GetAsync(Today);

        Assert.Equal(AdminDashboardService.RecentLoginCount, vm.RecentLogins.Count);
        Assert.Equal(new[] { "admin01", "student01", "student02", "manager01", "staff02" }, vm.RecentLogins.Select(l => l.Username));
        var first = vm.RecentLogins[0];
        Assert.Equal("ADMIN01", first.FullName);
        Assert.Equal(Role.ADMIN, first.Role);
        Assert.Equal(Noon.AddMinutes(60), first.LastLoginAt);
    }

    /// <summary>An empty database gives zeros everywhere and no recent logins.</summary>
    [Fact]
    public async Task Get_EmptyDatabase_Zeros()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        var vm = await new AdminDashboardService(db).GetAsync(Today);

        Assert.Equal(0, vm.StaffTotal);
        Assert.Equal(0, vm.StudentsTotal);
        Assert.All(vm.CompetitionsByStatus.Values, v => Assert.Equal(0, v));
        Assert.All(vm.ExhibitionsByStatus.Values, v => Assert.Equal(0, v));
        Assert.Equal(0, vm.TotalSubmissions);
        Assert.Empty(vm.RecentLogins);
    }
}
