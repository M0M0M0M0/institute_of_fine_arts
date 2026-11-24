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
/// CompetitionResultService (F-M05-08, steps 4–5 of 11/2026): publish only a CLOSED competition with a jury whose
/// entries are all graded by every judge (BR-08), awards handed out from the ranking (BR-30, BR-31: dense places, tied
/// entries share an award even beyond its quantity, disqualified entries never win), hand-given awards replaced,
/// GRADED + who / when / remarks stored, read only afterwards; the ranking preview; and the details page flags.
/// </summary>
public class CompetitionResultServiceTests
{
    private static readonly DateOnly Today = DateTimeHelper.Today;

    private long _staffId;
    private long _judgeId;
    private readonly List<long> _students = new();

    /// <summary>Empty in-memory database with one staff member, one teacher and four students.</summary>
    private AppDbContext CreateDb()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var staff = new StaffProfile
        {
            StaffCode = "NV0001", JoinedDate = new DateOnly(2024, 1, 1),
            Account = new Account { Username = "staff01", FullName = "Le Minh Hoang", Email = "s1@ifa.local", Role = Role.STAFF, PasswordHash = "x" },
        };
        var judge = new StaffProfile
        {
            StaffCode = "NV0003", JoinedDate = new DateOnly(2024, 1, 1),
            Account = new Account { Username = "teacher01", FullName = "Pham Thu Ha", Email = "t1@ifa.local", Role = Role.TEACHER, PasswordHash = "x" },
        };
        db.StaffProfiles.AddRange(staff, judge);
        for (var n = 1; n <= 4; n++)
        {
            db.StudentProfiles.Add(new StudentProfile
            {
                StudentCode = $"HV{n:D4}", EnrollmentDate = new DateOnly(2025, 9, 1), CourseName = "Painting",
                Account = new Account { Username = $"student{n:D2}", FullName = $"Student {n}", Email = $"st{n}@ifa.local", Role = Role.STUDENT, PasswordHash = "x" },
            });
        }
        db.SaveChanges();
        _staffId = staff.Id;
        _judgeId = judge.Id;
        _students.AddRange(db.StudentProfiles.Select(s => s.Id).OrderBy(id => id));
        return db;
    }

    private readonly Mock<INotificationService> _notifications = new();

    private CompetitionResultService Service(AppDbContext db) => new(db, NullLogger<CompetitionResultService>.Instance, _notifications.Object);

    /// <summary>
    /// A competition in the given status with one entry per score (null = not graded yet, -1 = disqualified), a jury of
    /// one teacher and one criterion (100 %), and the awards First Prize (1) and Second Prize (<paramref name="secondQuantity"/>).
    /// The entries' results are computed as the service would.
    /// </summary>
    private async Task<Competition> AddAsync(AppDbContext db, CompetitionStatus status, decimal?[] scores, bool jury = true, int secondQuantity = 1)
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
        c.Awards.Add(new CompetitionAward { AwardName = "Second Prize", Rank = 2, PrizeValue = 100m, Quantity = secondQuantity });
        db.Competitions.Add(c);
        var criterion = new GradingCriterion { Name = "Overall " + Guid.NewGuid().ToString("N")[..6] };
        db.GradingCriteria.Add(criterion);
        db.SaveChanges();
        var cc = new CompetitionCriterion { CompetitionId = c.Id, CriterionId = criterion.Id, WeightPercent = 100m };
        db.CompetitionCriteria.Add(cc);
        if (jury)
        {
            db.CompetitionJudges.Add(new CompetitionJudge { CompetitionId = c.Id, JudgeId = _judgeId, IsChair = true });
        }
        db.SaveChanges();

        for (var i = 0; i < scores.Length; i++)
        {
            var s = new Submission
            {
                CompetitionId = c.Id, StudentId = _students[i], Title = $"Night {i + 1}", ImagePath = "p.jpg", OriginalFileName = "p.jpg",
                FileSize = 10, TextContent = "A poem", Description = "Why", SubmittedAt = start.ToDateTime(new TimeOnly(10, i)),
            };
            db.Submissions.Add(s);
            db.SaveChanges();
            if (scores[i] is { } score)
            {
                var grade = new JudgeEvaluation
                {
                    SubmissionId = s.Id, JudgeId = _judgeId, Disqualified = score < 0, EvaluatedAt = DateTime.Now,
                    PositivePoints = score < 0 ? null : "Nice", NegativePoints = score < 0 ? "Copied" : null,
                    TotalScore = score < 0 ? null : score,
                };
                if (score >= 0)
                {
                    grade.Scores.Add(new JudgeScore { CompetitionCriterionId = cc.Id, Score = score });
                }
                db.JudgeEvaluations.Add(grade);
                db.SaveChanges();
            }
        }
        await JuryResults.RecomputeAsync(db, c.Id);
        return c;
    }

    /// <summary>Award name → titles of the winning entries.</summary>
    private static Dictionary<string, List<string>> Winners(AppDbContext db, long competitionId)
        => db.AwardsIssued.Include(a => a.CompetitionAward).Include(a => a.Submission)
            .Where(a => a.CompetitionAward.CompetitionId == competitionId).AsEnumerable()
            .GroupBy(a => a.CompetitionAward.AwardName)
            .ToDictionary(g => g.Key, g => g.Select(a => a.Submission.Title).OrderBy(t => t).ToList());

    /// <summary>BR-08 / BR-30: everything graded → GRADED with publisher and trimmed remarks; awards from the ranking.</summary>
    [Fact]
    public async Task Publish_AllGraded_GradedAndAwarded()
    {
        using var db = CreateDb();
        var c = await AddAsync(db, CompetitionStatus.CLOSED, [70m, 95m, 82m]);
        var before = DateTimeHelper.Now.AddSeconds(-1);

        var count = await Service(db).PublishAsync(c.Id, "  Congratulations to the winners.  ", _staffId);

        Assert.Equal(2, count);
        var saved = await db.Competitions.AsNoTracking().SingleAsync();
        Assert.Equal(CompetitionStatus.GRADED, saved.Status);
        Assert.True(saved.PublishedAt >= before);
        Assert.Equal(_staffId, saved.PublishedBy);
        Assert.Equal("Congratulations to the winners.", saved.ResultRemarks);
        var winners = Winners(db, c.Id);
        Assert.Equal(["Night 2"], winners["First Prize"]);
        Assert.Equal(["Night 3"], winners["Second Prize"]);
        _notifications.Verify(n => n.ResultsPublishedAsync(c.Id), Times.Once);
    }

    /// <summary>BR-31: tied entries share the award even beyond its quantity; the next award goes to the next place.</summary>
    [Fact]
    public async Task Publish_Tie_SharesAwardBeyondQuantity()
    {
        using var db = CreateDb();
        var c = await AddAsync(db, CompetitionStatus.CLOSED, [91m, 91m, 85m, 60m]);

        Assert.Equal(3, await Service(db).PublishAsync(c.Id, null, _staffId));

        var winners = Winners(db, c.Id);
        Assert.Equal(["Night 1", "Night 2"], winners["First Prize"]);
        Assert.Equal(["Night 3"], winners["Second Prize"]);
    }

    /// <summary>BR-30: a disqualified entry never wins, whatever the others scored.</summary>
    [Fact]
    public async Task Publish_DisqualifiedNeverWins()
    {
        using var db = CreateDb();
        var c = await AddAsync(db, CompetitionStatus.CLOSED, [-1m, 40m, 30m]);

        await Service(db).PublishAsync(c.Id, null, _staffId);

        var winners = Winners(db, c.Id);
        Assert.Equal(["Night 2"], winners["First Prize"]);
        Assert.Equal(["Night 3"], winners["Second Prize"]);
        Assert.DoesNotContain(winners.Values.SelectMany(v => v), t => t == "Night 1");
    }

    /// <summary>An award given by hand before publishing is replaced by the ranking.</summary>
    [Fact]
    public async Task Publish_ReplacesHandGivenAwards()
    {
        using var db = CreateDb();
        var c = await AddAsync(db, CompetitionStatus.CLOSED, [70m, 95m]);
        var first = db.CompetitionAwards.Single(a => a.CompetitionId == c.Id && a.Rank == 1);
        var lower = db.Submissions.Single(s => s.Title == "Night 1");
        db.AwardsIssued.Add(new AwardIssued { CompetitionAwardId = first.Id, SubmissionId = lower.Id, IssuedBy = _staffId, IssuedDate = Today });
        db.SaveChanges();

        await Service(db).PublishAsync(c.Id, null, _staffId);

        var winners = Winners(db, c.Id);
        Assert.Equal(["Night 2"], winners["First Prize"]);
        Assert.Equal(["Night 1"], winners["Second Prize"]);
    }

    /// <summary>BR-08: entries not graded by the whole jury block publishing; nothing changes.</summary>
    [Fact]
    public async Task Publish_NotAllGraded_Refused()
    {
        using var db = CreateDb();
        var c = await AddAsync(db, CompetitionStatus.CLOSED, [80m, null, null]);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => Service(db).PublishAsync(c.Id, null, _staffId));

        Assert.Equal(CompetitionResultService.NotGradedMessage(2), ex.Message);
        Assert.Equal("1 submission has not been graded by the whole jury yet.", CompetitionResultService.NotGradedMessage(1));
        _notifications.Verify(n => n.ResultsPublishedAsync(It.IsAny<long>()), Times.Never);
        var saved = await db.Competitions.AsNoTracking().SingleAsync();
        Assert.Null(saved.PublishedAt);
        Assert.Empty(db.AwardsIssued);
    }

    /// <summary>BR-25: a competition without a jury cannot be published.</summary>
    [Fact]
    public async Task Publish_NoJury_Refused()
    {
        using var db = CreateDb();
        var c = await AddAsync(db, CompetitionStatus.CLOSED, [80m], jury: false);
        var ex = await Assert.ThrowsAsync<BusinessException>(() => Service(db).PublishAsync(c.Id, null, _staffId));
        Assert.Equal(CompetitionResultService.NoJuryMessage, ex.Message);
    }

    /// <summary>BR-08: UPCOMING / ONGOING → only after closing; GRADED → already published.</summary>
    [Theory]
    [InlineData(CompetitionStatus.UPCOMING, CompetitionResultService.NotClosedMessage)]
    [InlineData(CompetitionStatus.ONGOING, CompetitionResultService.NotClosedMessage)]
    [InlineData(CompetitionStatus.GRADED, CompetitionResultService.AlreadyPublishedMessage)]
    public async Task Publish_NotClosed_Refused(CompetitionStatus status, string message)
    {
        using var db = CreateDb();
        var c = await AddAsync(db, status, []);
        var ex = await Assert.ThrowsAsync<BusinessException>(() => Service(db).PublishAsync(c.Id, null, _staffId));
        Assert.Equal(message, ex.Message);
        Assert.Null((await db.Competitions.AsNoTracking().SingleAsync()).PublishedBy);
    }

    /// <summary>Remarks over 2000 characters → refused; unknown competition → not found.</summary>
    [Fact]
    public async Task Publish_InvalidInput_Refused()
    {
        using var db = CreateDb();
        var c = await AddAsync(db, CompetitionStatus.CLOSED, [80m]);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => Service(db).PublishAsync(c.Id, new string('a', 2001), _staffId));
        Assert.Equal(CompetitionResultService.RemarksTooLongMessage, ex.Message);
        await Assert.ThrowsAsync<NotFoundException>(() => Service(db).PublishAsync(999, null, _staffId));
    }

    /// <summary>BR-21 / BR-10: after publishing, grades and the defined awards are read only.</summary>
    [Fact]
    public async Task Publish_ThenGradesAndAwardDefinitionsReadOnly()
    {
        using var db = CreateDb();
        var c = await AddAsync(db, CompetitionStatus.CLOSED, [80m]);
        await Service(db).PublishAsync(c.Id, null, _staffId);
        var entry = await db.Submissions.FirstAsync(s => s.CompetitionId == c.Id);
        var award = await db.CompetitionAwards.FirstAsync(a => a.CompetitionId == c.Id);

        var evaluations = new EvaluationService(db, NullLogger<EvaluationService>.Instance);
        var evalEx = await Assert.ThrowsAsync<BusinessException>(() =>
            evaluations.SaveAsync(entry.Id, _judgeId, new EvaluationFormVm { Disqualified = true, NegativePoints = "x" }));
        Assert.Equal(EvaluationService.GradedReadOnlyMessage, evalEx.Message);
        var awards = new CompetitionAwardService(db, NullLogger<CompetitionAwardService>.Instance);
        var awardEx = await Assert.ThrowsAsync<BusinessException>(() => awards.DeleteAsync(c.Id, award.Id));
        Assert.Equal(CompetitionAwardService.GradedReadOnlyMessage, awardEx.Message);
    }

    /// <summary>BR-32: before publishing the ranking preview shows places and the awards publishing would give.</summary>
    [Fact]
    public async Task Ranking_PreviewPlacesAndAwards()
    {
        using var db = CreateDb();
        var c = await AddAsync(db, CompetitionStatus.CLOSED, [91m, 91m, 85m, -1m]);

        var rows = await Service(db).GetRankingAsync(c.Id);

        Assert.Equal(4, rows.Count);
        Assert.Equal([1, 1, 2], rows.Take(3).Select(r => r.Place!.Value));
        Assert.Equal("First Prize", rows[0].AwardName);
        Assert.Equal("First Prize", rows[1].AwardName);
        Assert.Equal("Second Prize", rows[2].AwardName);
        Assert.Equal(Rating.DISQUALIFIED, rows[3].Rating);
        Assert.Null(rows[3].Place);
        Assert.Empty(db.AwardsIssued); // a preview gives nothing
    }

    /// <summary>SCR-25: grading progress, the publish flag with its reason, and no hand issuing any more.</summary>
    [Fact]
    public async Task Details_ProgressAndPublishFlags()
    {
        using var db = CreateDb();
        var details = new CompetitionService(db, new Mock<IFileStorageService>().Object, NullLogger<CompetitionService>.Instance, _notifications.Object);
        var c = await AddAsync(db, CompetitionStatus.CLOSED, [80m, 70m, null]);

        var vm = await details.GetDetailsAsync(c.Id);
        Assert.Equal(2, vm.EvaluatedCount);
        Assert.Equal(3, vm.SubmissionCount);
        Assert.True(vm.CanPublish);
        Assert.NotNull(vm.PublishBlockedReason);
        Assert.False(vm.CanIssueAwards);

        // The last entry graded → publish allowed; after publishing → GRADED, publisher shown, winners listed
        var last = await db.Submissions.SingleAsync(s => s.CompetitionId == c.Id && s.Title == "Night 3");
        var evaluations = new EvaluationService(db, NullLogger<EvaluationService>.Instance);
        var cc = await db.CompetitionCriteria.SingleAsync(x => x.CompetitionId == c.Id);
        await evaluations.SaveAsync(last.Id, _judgeId, new EvaluationFormVm
        {
            PositivePoints = "Ok", Scores = [new CriterionScoreVm { CompetitionCriterionId = cc.Id, Score = 60m }],
        });
        Assert.Null((await details.GetDetailsAsync(c.Id)).PublishBlockedReason);
        await Service(db).PublishAsync(c.Id, "Well done", _staffId);
        vm = await details.GetDetailsAsync(c.Id);
        Assert.Equal(CompetitionStatus.GRADED, vm.Status);
        Assert.False(vm.CanPublish);
        Assert.Equal("Le Minh Hoang", vm.PublishedByName);
        Assert.Equal("Well done", vm.ResultRemarks);
        Assert.Equal("Night 1", Assert.Single(vm.Awards.Single(a => a.Rank == 1).Winners).SubmissionTitle);
    }
}
