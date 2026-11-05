using InstituteOfFineArts.Web.Data;
using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.Services.Implementations;
using Microsoft.EntityFrameworkCore;

namespace InstituteOfFineArts.Tests.Services;

/// <summary>
/// ManagerDashboardService (task BE2-13; F-M11-01, US-15 AC3): students, staff, competitions by calculated status,
/// submissions, awards issued, paintings sold and total sales on a database with known data.
/// </summary>
public class ManagerDashboardServiceTests
{
    /// <summary>Fixed "today" used by every test.</summary>
    private static readonly DateOnly Today = new(2026, 6, 15);

    /// <summary>
    /// Database with: 2 staff + 1 manager + 1 admin; 4 students (1 locked); competitions UPCOMING, ONGOING,
    /// 2 × CLOSED (one with a stale stored status) and GRADED; 5 submissions (3 evaluated); 2 awards issued
    /// ($500 + $300); exhibition entries SOLD × 2 (one paid), ACCEPTED, UNSOLD and PENDING.
    /// </summary>
    private static AppDbContext CreateDb()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        // Accounts
        Account Account(string user, Role role, bool active = true) => new()
        {
            Username = user, FullName = user.ToUpperInvariant(), Email = $"{user}@ifa.local", Role = role, Active = active, PasswordHash = "x",
        };

        // Staff profiles of the three staff roles
        StaffProfile Staff(string user, Role role) => new() { StaffCode = user.ToUpperInvariant(), JoinedDate = new DateOnly(2024, 1, 1), Account = Account(user, role) };
        db.StaffProfiles.AddRange(Staff("staff01", Role.STAFF), Staff("staff02", Role.STAFF), Staff("manager01", Role.MANAGER), Staff("admin01", Role.ADMIN));

        // Students: 4 profiles with different enrollment statuses, one locked
        StudentProfile Student(string user, EnrollmentStatus status, bool active = true) => new()
        {
            StudentCode = user.ToUpperInvariant(), CourseName = "Fine Arts", EnrollmentDate = new DateOnly(2025, 9, 1),
            EnrollmentStatus = status, Account = Account(user, Role.STUDENT, active),
        };
        db.StudentProfiles.AddRange(
            Student("student01", EnrollmentStatus.STUDYING),
            Student("student02", EnrollmentStatus.STUDYING),
            Student("student03", EnrollmentStatus.GRADUATED),
            Student("student04", EnrollmentStatus.SUSPENDED, active: false));

        // Competitions: C4 is stored as UPCOMING but has ended (counts as CLOSED)
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

        // Submissions: 3 of 5 evaluated
        Submission Submission(long id, long competitionId, bool evaluated) => new()
        {
            Id = id, CompetitionId = competitionId, StudentId = 1, Title = $"S{id}", ImagePath = "x.jpg", OriginalFileName = "x.jpg",
            TextContent = "t", Description = "d", Evaluation = evaluated ? new Evaluation { EvaluatedBy = 1, Rating = Rating.BEST } : null,
        };
        db.Submissions.AddRange(
            Submission(1, 2, evaluated: false),
            Submission(2, 3, evaluated: true),
            Submission(3, 5, evaluated: true),
            Submission(4, 5, evaluated: true),
            Submission(5, 4, evaluated: false));

        // Awards of the GRADED competition: both given once
        db.CompetitionAwards.AddRange(
            new CompetitionAward { Id = 1, CompetitionId = 5, AwardName = "First Prize", Rank = 1, PrizeValue = 500m },
            new CompetitionAward { Id = 2, CompetitionId = 5, AwardName = "Second Prize", Rank = 2, PrizeValue = 300m });
        db.AwardsIssued.AddRange(
            new AwardIssued { CompetitionAwardId = 1, SubmissionId = 3, IssuedBy = 1, IssuedDate = Today },
            new AwardIssued { CompetitionAwardId = 2, SubmissionId = 4, IssuedBy = 1, IssuedDate = Today });

        // Exhibition entries: only SOLD ones count as sales; SoldPrice of other statuses must be ignored
        db.Exhibitions.Add(new Exhibition { Id = 1, Name = "X", Venue = "Hall", City = "Hanoi", StartDate = Today.AddDays(-10), EndDate = Today.AddDays(-1), CreatedBy = 1 });
        ExhibitionEntry Entry(long submissionId, EntryStatus status, decimal? sold = null, decimal? paid = null) => new()
        {
            ExhibitionId = 1, SubmissionId = submissionId, AskingPrice = 100m, Status = status, SoldPrice = sold,
            PaidToStudent = paid != null, PaidAmount = paid, ProposedBy = 1,
        };
        db.ExhibitionEntries.AddRange(
            Entry(3, EntryStatus.SOLD, sold: 1200.50m, paid: 1000m),
            Entry(4, EntryStatus.SOLD, sold: 800m),
            Entry(2, EntryStatus.ACCEPTED),
            Entry(2, EntryStatus.UNSOLD),
            Entry(1, EntryStatus.PENDING));

        db.SaveChanges();
        return db;
    }

    /// <summary>F-M11-01: students total, active accounts and every enrollment status (0 when nobody has it).</summary>
    [Fact]
    public async Task Get_StudentCounts()
    {
        using var db = CreateDb();

        var vm = await new ManagerDashboardService(db).GetAsync(Today);

        Assert.Equal(4, vm.StudentsTotal);
        Assert.Equal(3, vm.StudentsActive);
        Assert.Equal(2, vm.StudentsByEnrollment[EnrollmentStatus.STUDYING]);
        Assert.Equal(1, vm.StudentsByEnrollment[EnrollmentStatus.GRADUATED]);
        Assert.Equal(1, vm.StudentsByEnrollment[EnrollmentStatus.SUSPENDED]);
        Assert.Equal(0, vm.StudentsByEnrollment[EnrollmentStatus.DROPPED]);
    }

    /// <summary>F-M11-01: staff profiles per role; students are not counted as staff.</summary>
    [Fact]
    public async Task Get_StaffCounts()
    {
        using var db = CreateDb();

        var vm = await new ManagerDashboardService(db).GetAsync(Today);

        Assert.Equal(2, vm.StaffByRole[Role.STAFF]);
        Assert.Equal(1, vm.StaffByRole[Role.MANAGER]);
        Assert.Equal(1, vm.StaffByRole[Role.ADMIN]);
        Assert.False(vm.StaffByRole.ContainsKey(Role.STUDENT));
        Assert.Equal(4, vm.StaffTotal);
    }

    /// <summary>US-15 AC3 / BR-07: competitions by calculated status (a stale UPCOMING that has ended counts as CLOSED).</summary>
    [Fact]
    public async Task Get_CompetitionsByCalculatedStatus()
    {
        using var db = CreateDb();

        var vm = await new ManagerDashboardService(db).GetAsync(Today);

        Assert.Equal(1, vm.CompetitionsByStatus[CompetitionStatus.UPCOMING]);
        Assert.Equal(1, vm.CompetitionsByStatus[CompetitionStatus.ONGOING]);
        Assert.Equal(2, vm.CompetitionsByStatus[CompetitionStatus.CLOSED]);
        Assert.Equal(1, vm.CompetitionsByStatus[CompetitionStatus.GRADED]);
        Assert.Equal(5, vm.CompetitionsTotal);
    }

    /// <summary>US-15 AC3: submissions total and evaluated.</summary>
    [Fact]
    public async Task Get_SubmissionCounts()
    {
        using var db = CreateDb();

        var vm = await new ManagerDashboardService(db).GetAsync(Today);

        Assert.Equal(5, vm.SubmissionsTotal);
        Assert.Equal(3, vm.SubmissionsEvaluated);
    }

    /// <summary>US-15 AC3: awards issued and their total prize value.</summary>
    [Fact]
    public async Task Get_AwardsIssued()
    {
        using var db = CreateDb();

        var vm = await new ManagerDashboardService(db).GetAsync(Today);

        Assert.Equal(2, vm.AwardsIssued);
        Assert.Equal(800m, vm.AwardsPrizeTotal);
    }

    /// <summary>US-15 AC3 / BR-45: only SOLD entries count as paintings sold; total sales and the amount paid to students.</summary>
    [Fact]
    public async Task Get_Sales_SoldEntriesOnly()
    {
        using var db = CreateDb();

        var vm = await new ManagerDashboardService(db).GetAsync(Today);

        Assert.Equal(2, vm.PaintingsSold);
        Assert.Equal(2000.50m, vm.TotalSales);
        Assert.Equal(1000m, vm.PaidToStudents);
    }

    /// <summary>An empty database gives zeros everywhere (no NULL sums).</summary>
    [Fact]
    public async Task Get_EmptyDatabase_Zeros()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        var vm = await new ManagerDashboardService(db).GetAsync(Today);

        Assert.Equal(0, vm.StudentsTotal);
        Assert.Equal(0, vm.StaffTotal);
        Assert.All(vm.CompetitionsByStatus.Values, v => Assert.Equal(0, v));
        Assert.Equal(0, vm.SubmissionsTotal);
        Assert.Equal(0, vm.AwardsIssued);
        Assert.Equal(0m, vm.AwardsPrizeTotal);
        Assert.Equal(0, vm.PaintingsSold);
        Assert.Equal(0m, vm.TotalSales);
        Assert.Equal(0m, vm.PaidToStudents);
    }
}
