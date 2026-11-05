using InstituteOfFineArts.Web.Data;
using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Helpers;
using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.Services.Implementations;
using InstituteOfFineArts.Web.Services.Interfaces;
using InstituteOfFineArts.Web.ViewModels.Staff.Evaluations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace InstituteOfFineArts.Tests.Services;

/// <summary>
/// CompetitionResultService (task BE1-11, F-M05-08): publish only a CLOSED competition whose entries are all
/// evaluated (BR-08, US-10 AC1), store GRADED + who / when / remarks, no award required (decision), read only
/// afterwards (BR-10, BR-21); plus the publish / issued-award data of the staff competition details page.
/// </summary>
public class CompetitionResultServiceTests
{
    /// <summary>Today in the institute time zone.</summary>
    private static readonly DateOnly Today = DateTimeHelper.Today;

    /// <summary>staff_profile.id of staff01 in the test database.</summary>
    private long _staffId;

    /// <summary>student_profile.id values in the test database.</summary>
    private readonly List<long> _students = new();

    /// <summary>Empty in-memory database with one staff member and three students.</summary>
    private AppDbContext CreateDb()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var staff = new StaffProfile
        {
            StaffCode = "NV0003", JoinedDate = new DateOnly(2024, 1, 1),
            Account = new Account { Username = "staff01", FullName = "Le Minh Hoang", Email = "s1@ifa.local", Role = Role.STAFF, PasswordHash = "x" },
        };
        db.StaffProfiles.Add(staff);
        for (var n = 1; n <= 3; n++)
        {
            db.StudentProfiles.Add(new StudentProfile
            {
                StudentCode = $"HV{n:D4}", EnrollmentDate = new DateOnly(2025, 9, 1), CourseName = "Painting",
                Account = new Account { Username = $"student{n:D2}", FullName = $"Student {n}", Email = $"st{n}@ifa.local", Role = Role.STUDENT, PasswordHash = "x" },
            });
        }
        db.SaveChanges();
        _staffId = staff.Id;
        _students.AddRange(db.StudentProfiles.Select(s => s.Id).OrderBy(id => id));
        return db;
    }

    /// <summary>Service under test.</summary>
    private static CompetitionResultService Service(AppDbContext db) => new(db, NullLogger<CompetitionResultService>.Instance);

    /// <summary>
    /// Adds a competition in the given calculated status with three entries; the first <paramref name="evaluated"/>
    /// are evaluated (the first one BEST), and a First Prize award (quantity 1).
    /// </summary>
    private Competition Add(AppDbContext db, CompetitionStatus status = CompetitionStatus.CLOSED, int evaluated = 3)
    {
        var (start, end) = status switch
        {
            CompetitionStatus.UPCOMING => (Today.AddDays(5), Today.AddDays(30)),
            CompetitionStatus.ONGOING => (Today.AddDays(-5), Today.AddDays(5)),
            _ => (Today.AddDays(-30), Today.AddDays(-5)),
        };
        var c = new Competition
        {
            Code = "X" + Guid.NewGuid().ToString("N")[..8], Title = "City at Night", StartDate = start, EndDate = end, CreatedBy = _staffId,
            Status = status == CompetitionStatus.GRADED ? CompetitionStatus.GRADED : CompetitionStatus.UPCOMING,
        };
        c.Awards.Add(new CompetitionAward { AwardName = "First Prize", Rank = 1, PrizeValue = 250m, Quantity = 1 });
        db.Competitions.Add(c);
        db.SaveChanges();

        for (var i = 0; i < 3; i++)
        {
            var s = new Submission
            {
                CompetitionId = c.Id, StudentId = _students[i], Title = $"Night {i + 1}", ImagePath = "p.jpg", OriginalFileName = "p.jpg",
                FileSize = 10, TextContent = "A poem", Description = "Why", SubmittedAt = start.ToDateTime(new TimeOnly(10, i)),
            };
            if (i < evaluated)
            {
                s.Evaluation = new Evaluation
                {
                    EvaluatedBy = _staffId, Rating = i == 0 ? Rating.BEST : Rating.GOOD, PositivePoints = "Nice", EvaluatedAt = DateTime.Now,
                };
            }
            db.Submissions.Add(s);
        }
        db.SaveChanges();
        return c;
    }

    /// <summary>Gives the First Prize to the BEST entry.</summary>
    private void IssueFirstPrize(AppDbContext db, Competition c)
    {
        var award = db.CompetitionAwards.Single(a => a.CompetitionId == c.Id);
        var best = db.Submissions.Single(s => s.CompetitionId == c.Id && s.Evaluation != null && s.Evaluation.Rating == Rating.BEST);
        db.AwardsIssued.Add(new AwardIssued { CompetitionAwardId = award.Id, SubmissionId = best.Id, IssuedBy = _staffId, IssuedDate = Today });
        db.SaveChanges();
    }

    /// <summary>UC-06 step 5 / BR-08: CLOSED and 100% evaluated → GRADED with publish time, publisher and trimmed remarks.</summary>
    [Fact]
    public async Task Publish_AllGraded_Graded()
    {
        using var db = CreateDb();
        var c = Add(db);
        IssueFirstPrize(db, c);
        var before = DateTimeHelper.Now.AddSeconds(-1);

        var awards = await Service(db).PublishAsync(c.Id, "  Congratulations to the winners.  ", _staffId);

        Assert.Equal(1, awards);
        var saved = await db.Competitions.AsNoTracking().SingleAsync();
        Assert.Equal(CompetitionStatus.GRADED, saved.Status);
        Assert.True(saved.PublishedAt >= before);
        Assert.Equal(_staffId, saved.PublishedBy);
        Assert.Equal("Congratulations to the winners.", saved.ResultRemarks);
    }

    /// <summary>US-10 AC1: two entries not evaluated → "2 submissions have not been graded yet."; nothing changes.</summary>
    [Fact]
    public async Task Publish_NotAllGraded_Refused()
    {
        using var db = CreateDb();
        var c = Add(db, evaluated: 1);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => Service(db).PublishAsync(c.Id, null, _staffId));

        Assert.Equal("2 submissions have not been graded yet.", ex.Message);
        Assert.Equal(CompetitionResultService.NotGradedMessage(2), ex.Message);
        Assert.Equal("1 submission has not been graded yet.", CompetitionResultService.NotGradedMessage(1));
        var saved = await db.Competitions.AsNoTracking().SingleAsync();
        Assert.NotEqual(CompetitionStatus.GRADED, saved.Status);
        Assert.Null(saved.PublishedAt);
        Assert.Null(saved.PublishedBy);
    }

    /// <summary>BR-08: UPCOMING / ONGOING → only after closing; GRADED → already published.</summary>
    [Theory]
    [InlineData(CompetitionStatus.UPCOMING, CompetitionResultService.NotClosedMessage)]
    [InlineData(CompetitionStatus.ONGOING, CompetitionResultService.NotClosedMessage)]
    [InlineData(CompetitionStatus.GRADED, CompetitionResultService.AlreadyPublishedMessage)]
    public async Task Publish_NotClosed_Refused(CompetitionStatus status, string message)
    {
        using var db = CreateDb();
        var c = Add(db, status);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => Service(db).PublishAsync(c.Id, null, _staffId));
        Assert.Equal(message, ex.Message);
        Assert.Null((await db.Competitions.AsNoTracking().SingleAsync()).PublishedBy);
    }

    /// <summary>Decision: no issued award is required to publish (the count 0 lets the page warn); blank remarks → null.</summary>
    [Fact]
    public async Task Publish_WithoutAwards_Allowed()
    {
        using var db = CreateDb();
        var c = Add(db);

        Assert.Equal(0, await Service(db).PublishAsync(c.Id, "   ", _staffId));
        var saved = await db.Competitions.AsNoTracking().SingleAsync();
        Assert.Equal(CompetitionStatus.GRADED, saved.Status);
        Assert.Null(saved.ResultRemarks);
    }

    /// <summary>Remarks over 2000 characters → refused; unknown competition → not found.</summary>
    [Fact]
    public async Task Publish_InvalidInput_Refused()
    {
        using var db = CreateDb();
        var c = Add(db);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => Service(db).PublishAsync(c.Id, new string('a', 2001), _staffId));
        Assert.Equal(CompetitionResultService.RemarksTooLongMessage, ex.Message);
        await Assert.ThrowsAsync<NotFoundException>(() => Service(db).PublishAsync(999, null, _staffId));
    }

    /// <summary>BR-21 / BR-10: after publishing, evaluations and the defined awards are read only.</summary>
    [Fact]
    public async Task Publish_ThenEvaluationsAndAwardDefinitionsReadOnly()
    {
        using var db = CreateDb();
        var c = Add(db);
        await Service(db).PublishAsync(c.Id, null, _staffId);
        var entry = await db.Submissions.FirstAsync(s => s.CompetitionId == c.Id);
        var award = await db.CompetitionAwards.FirstAsync(a => a.CompetitionId == c.Id);

        var evaluations = new EvaluationService(db, NullLogger<EvaluationService>.Instance);
        var evalEx = await Assert.ThrowsAsync<BusinessException>(() =>
            evaluations.SaveAsync(entry.Id, _staffId, new EvaluationFormVm { Rating = Rating.NORMAL, PositivePoints = "x" }));
        Assert.Equal(EvaluationService.GradedReadOnlyMessage, evalEx.Message);
        var awards = new CompetitionAwardService(db, NullLogger<CompetitionAwardService>.Instance);
        var awardEx = await Assert.ThrowsAsync<BusinessException>(() => awards.DeleteAsync(c.Id, award.Id));
        Assert.Equal(CompetitionAwardService.GradedReadOnlyMessage, awardEx.Message);
    }

    /// <summary>
    /// SCR-25 (CompetitionService.GetDetailsAsync): grading progress, the publish flag with the US-10 reason,
    /// winners per award and the issue flag (CLOSED with a free slot).
    /// </summary>
    [Fact]
    public async Task Details_ProgressWinnersAndPublishFlags()
    {
        using var db = CreateDb();
        var details = new CompetitionService(db, new Mock<IFileStorageService>().Object, NullLogger<CompetitionService>.Instance);
        var c = Add(db, evaluated: 2);

        // CLOSED, 2/3 graded, no award yet
        var vm = await details.GetDetailsAsync(c.Id);
        Assert.Equal(2, vm.EvaluatedCount);
        Assert.Equal(3, vm.SubmissionCount);
        Assert.True(vm.CanPublish);
        Assert.Equal("1 submission has not been graded yet.", vm.PublishBlockedReason);
        Assert.True(vm.CanIssueAwards);
        Assert.Empty(vm.Awards[0].Winners);

        // First Prize given → its row lists the winner; no slot left → no issue link
        IssueFirstPrize(db, c);
        vm = await details.GetDetailsAsync(c.Id);
        var winner = Assert.Single(vm.Awards[0].Winners);
        Assert.Equal("Night 1", winner.SubmissionTitle);
        Assert.Equal("Student 1", winner.StudentName);
        Assert.False(vm.CanIssueAwards);

        // Everything graded → publish allowed; after publishing → GRADED, publisher shown, no publish form
        var last = await db.Submissions.SingleAsync(s => s.CompetitionId == c.Id && s.Evaluation == null);
        db.Evaluations.Add(new Evaluation { SubmissionId = last.Id, EvaluatedBy = _staffId, Rating = Rating.NORMAL, PositivePoints = "Ok", EvaluatedAt = DateTime.Now });
        db.SaveChanges();
        Assert.Null((await details.GetDetailsAsync(c.Id)).PublishBlockedReason);
        await Service(db).PublishAsync(c.Id, "Well done", _staffId);
        vm = await details.GetDetailsAsync(c.Id);
        Assert.Equal(CompetitionStatus.GRADED, vm.Status);
        Assert.False(vm.CanPublish);
        Assert.Equal("Le Minh Hoang", vm.PublishedByName);
        Assert.Equal("Well done", vm.ResultRemarks);
    }

    /// <summary>An ONGOING competition shows neither the publish form nor the issue link.</summary>
    [Fact]
    public async Task Details_Ongoing_NoPublishNoIssue()
    {
        using var db = CreateDb();
        var details = new CompetitionService(db, new Mock<IFileStorageService>().Object, NullLogger<CompetitionService>.Instance);
        var c = Add(db, CompetitionStatus.ONGOING, evaluated: 0);

        var vm = await details.GetDetailsAsync(c.Id);
        Assert.False(vm.CanPublish);
        Assert.False(vm.CanIssueAwards);
    }
}
