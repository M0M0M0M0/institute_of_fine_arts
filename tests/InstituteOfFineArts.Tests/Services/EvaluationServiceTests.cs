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

namespace InstituteOfFineArts.Tests.Services;

/// <summary>
/// EvaluationService with a jury (steps 4–5, 11/2026): only judges of the competition grade (BR-21), only when CLOSED,
/// scores 0–100 with at most 2 decimals for every criterion (BR-23), weighted total, disqualification with a reason,
/// late entries only disqualified (BR-14), the entry's result once every judge graded (BR-22), one disqualification is
/// enough, grades changed in another window, delete, the page (no peeking before the deadline, jury progress) and the
/// score sheet for each viewer (BR-24).
/// </summary>
public class EvaluationServiceTests
{
    /// <summary>Today in the institute time zone.</summary>
    private static readonly DateOnly Today = DateTimeHelper.Today;

    private long _staff;
    private long _judge1;
    private long _judge2;
    private long _outsider;
    private long _studentId;

    /// <summary>Empty in-memory database with one staff member, three teachers and one student.</summary>
    private AppDbContext CreateDb()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        StaffProfile Person(string user, string name, Role role, string code) => new()
        {
            StaffCode = code, JoinedDate = new DateOnly(2024, 1, 1),
            Account = new Account { Username = user, FullName = name, Email = user + "@ifa.local", Role = role, PasswordHash = "x" },
        };
        var staff = Person("staff01", "Tran Thi Mai", Role.STAFF, "NV0001");
        var j1 = Person("teacher01", "Le Minh Hoang", Role.TEACHER, "NV0003");
        var j2 = Person("teacher02", "Pham Thu Ha", Role.TEACHER, "NV0004");
        var outsider = Person("teacher03", "Vo Quoc Bao", Role.TEACHER, "NV0005");
        var student = new StudentProfile
        {
            StudentCode = "HV0001", EnrollmentDate = new DateOnly(2025, 9, 1), CourseName = "Painting",
            Account = new Account { Username = "student01", FullName = "Nguyen Van An", Email = "st@ifa.local", Role = Role.STUDENT, PasswordHash = "x" },
        };
        db.StaffProfiles.AddRange(staff, j1, j2, outsider);
        db.StudentProfiles.Add(student);
        db.SaveChanges();
        (_staff, _judge1, _judge2, _outsider, _studentId) = (staff.Id, j1.Id, j2.Id, outsider.Id, student.Id);
        return db;
    }

    private static EvaluationService Service(AppDbContext db) => new(db, NullLogger<EvaluationService>.Instance);

    /// <summary>
    /// A competition in the given status with one entry, a jury (judge1 chair, judge2 when <paramref name="twoJudges"/>)
    /// and two criteria: Creativity 60 %, Technique 40 % (unless <paramref name="criteria"/> is false).
    /// </summary>
    private (Submission Entry, long Creativity, long Technique) AddEntry(AppDbContext db, CompetitionStatus status = CompetitionStatus.CLOSED,
        bool late = false, bool twoJudges = false, bool criteria = true)
    {
        var (start, end) = status switch
        {
            CompetitionStatus.UPCOMING => (Today.AddDays(5), Today.AddDays(30)),
            CompetitionStatus.ONGOING => (Today.AddDays(-5), Today.AddDays(5)),
            _ => (Today.AddDays(-30), Today.AddDays(-5)),
        };
        var c = new Competition
        {
            Code = "X" + Guid.NewGuid().ToString("N")[..8], Title = "City at Night", StartDate = start, EndDate = end,
            Status = status == CompetitionStatus.GRADED ? CompetitionStatus.GRADED : CompetitionStatus.UPCOMING, CreatedBy = _staff,
        };
        db.Competitions.Add(c);
        db.SaveChanges();
        db.CompetitionJudges.Add(new CompetitionJudge { CompetitionId = c.Id, JudgeId = _judge1, IsChair = true });
        if (twoJudges)
        {
            db.CompetitionJudges.Add(new CompetitionJudge { CompetitionId = c.Id, JudgeId = _judge2 });
        }
        long creativity = 0, technique = 0;
        if (criteria)
        {
            var g1 = new GradingCriterion { Name = "Creativity" + c.Id };
            var g2 = new GradingCriterion { Name = "Technique" + c.Id };
            db.GradingCriteria.AddRange(g1, g2);
            db.SaveChanges();
            var c1 = new CompetitionCriterion { CompetitionId = c.Id, CriterionId = g1.Id, WeightPercent = 60m, SortOrder = 0 };
            var c2 = new CompetitionCriterion { CompetitionId = c.Id, CriterionId = g2.Id, WeightPercent = 40m, SortOrder = 1 };
            db.CompetitionCriteria.AddRange(c1, c2);
            db.SaveChanges();
            (creativity, technique) = (c1.Id, c2.Id);
        }

        var submittedAt = late ? end.AddDays(1).ToDateTime(new TimeOnly(9, 0)) : start.ToDateTime(new TimeOnly(10, 0));
        var s = new Submission
        {
            CompetitionId = c.Id, StudentId = _studentId, Title = "Neon Street", ImagePath = "p.jpg", OriginalFileName = "p.jpg",
            FileSize = 10, TextContent = "A poem", Description = "Why I painted it", SubmittedAt = submittedAt,
        };
        db.Submissions.Add(s);
        db.SaveChanges();
        return (s, creativity, technique);
    }

    /// <summary>Form with the two scores.</summary>
    private static EvaluationFormVm Form(long creativity, decimal? cScore, long technique, decimal? tScore, string? positive = "Strong composition") => new()
    {
        PositivePoints = positive, NegativePoints = "Dark corners", ImprovementAreas = "Perspective",
        Scores =
        [
            new CriterionScoreVm { CompetitionCriterionId = creativity, Score = cScore, Comment = "  Fresh idea  " },
            new CriterionScoreVm { CompetitionCriterionId = technique, Score = tScore },
        ],
    };

    /// <summary>Disqualification form.</summary>
    private static EvaluationFormVm Disqualify(string? reason = "Copied from a photo") => new() { Disqualified = true, NegativePoints = reason };

    // ------------------------------------------------------------------ saving

    /// <summary>TC-M07-02: the only judge grades → weighted total, comments trimmed, and the entry's result at once.</summary>
    [Fact]
    public async Task Save_SingleJudge_TotalAndResult()
    {
        using var db = CreateDb();
        var (s, c, t) = AddEntry(db);

        var created = await Service(db).SaveAsync(s.Id, _judge1, Form(c, 95m, t, 80m));

        Assert.True(created);
        var grade = await db.JudgeEvaluations.Include(e => e.Scores).SingleAsync();
        Assert.Equal(89m, grade.TotalScore); // 95 × 0.6 + 80 × 0.4
        Assert.Equal("Fresh idea", grade.Scores.Single(x => x.CompetitionCriterionId == c).Comment);
        var result = await db.Evaluations.SingleAsync();
        Assert.Equal(89m, result.TotalScore);
        Assert.Equal(Rating.BETTER, result.Rating); // default thresholds: BEST from 90
    }

    /// <summary>BR-22: with two judges the result appears only after both graded; it is the average of their totals.</summary>
    [Fact]
    public async Task Save_TwoJudges_ResultWhenComplete()
    {
        using var db = CreateDb();
        var (s, c, t) = AddEntry(db, twoJudges: true);

        await Service(db).SaveAsync(s.Id, _judge1, Form(c, 95m, t, 95m));
        Assert.Empty(db.Evaluations);

        await Service(db).SaveAsync(s.Id, _judge2, Form(c, 84m, t, 84m));
        var result = await db.Evaluations.SingleAsync();
        Assert.Equal(89.5m, result.TotalScore);
        Assert.Equal(Rating.BETTER, result.Rating);
    }

    /// <summary>BR-27: thresholds stored by the admin are used instead of the defaults.</summary>
    [Fact]
    public async Task Save_UsesStoredThresholds()
    {
        using var db = CreateDb();
        db.RatingThresholds.Add(new RatingThreshold { Rating = Rating.BEST, MinScore = 85m });
        db.SaveChanges();
        var (s, c, t) = AddEntry(db);

        await Service(db).SaveAsync(s.Id, _judge1, Form(c, 86m, t, 86m));

        Assert.Equal(Rating.BEST, (await db.Evaluations.SingleAsync()).Rating);
    }

    /// <summary>BR-21: a teacher who is not on the jury cannot grade.</summary>
    [Fact]
    public async Task Save_NotOnJury_Refused()
    {
        using var db = CreateDb();
        var (s, c, t) = AddEntry(db);
        var ex = await Assert.ThrowsAsync<BusinessException>(() => Service(db).SaveAsync(s.Id, _outsider, Form(c, 90m, t, 90m)));
        Assert.Equal(EvaluationService.NotJudgeMessage, ex.Message);
        Assert.Empty(db.JudgeEvaluations);
    }

    /// <summary>BR-21: before the deadline and after publishing nobody grades.</summary>
    [Theory]
    [InlineData(CompetitionStatus.ONGOING, EvaluationService.NotClosedMessage)]
    [InlineData(CompetitionStatus.GRADED, EvaluationService.GradedReadOnlyMessage)]
    public async Task Save_WrongStatus_Refused(CompetitionStatus status, string message)
    {
        using var db = CreateDb();
        var (s, c, t) = AddEntry(db, status);
        var ex = await Assert.ThrowsAsync<BusinessException>(() => Service(db).SaveAsync(s.Id, _judge1, Form(c, 90m, t, 90m)));
        Assert.Equal(message, ex.Message);
    }

    /// <summary>A competition without criteria cannot be graded yet.</summary>
    [Fact]
    public async Task Save_NoCriteria_Refused()
    {
        using var db = CreateDb();
        var (s, _, _) = AddEntry(db, criteria: false);
        var ex = await Assert.ThrowsAsync<BusinessException>(() => Service(db).SaveAsync(s.Id, _judge1, new EvaluationFormVm { PositivePoints = "x" }));
        Assert.Equal(EvaluationService.NoCriteriaMessage, ex.Message);
    }

    /// <summary>BR-23: every criterion needs a score from 0 to 100 with at most 2 decimals.</summary>
    [Theory]
    [InlineData(null, EvaluationFormVm.ScoreRequiredMessage)]
    [InlineData(100.5, EvaluationFormVm.ScoreRangeMessage)]
    [InlineData(-1.0, EvaluationFormVm.ScoreRangeMessage)]
    [InlineData(80.125, EvaluationFormVm.ScoreRangeMessage)]
    public async Task Save_BadScore_Refused(double? score, string message)
    {
        using var db = CreateDb();
        var (s, c, t) = AddEntry(db);
        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => Service(db).SaveAsync(s.Id, _judge1, Form(c, (decimal?)score, t, 80m)));
        Assert.Equal(message, ex.Message);
        Assert.Empty(db.JudgeEvaluations);
    }

    /// <summary>BR-23: positive points are required for a scored entry.</summary>
    [Fact]
    public async Task Save_NoPositivePoints_Refused()
    {
        using var db = CreateDb();
        var (s, c, t) = AddEntry(db);
        var ex = await Assert.ThrowsAsync<BusinessException>(() => Service(db).SaveAsync(s.Id, _judge1, Form(c, 80m, t, 80m, positive: "  ")));
        Assert.Equal(EvaluationFormVm.PositivePointsMessage, ex.Message);
    }

    /// <summary>BR-23: a disqualification needs its reason.</summary>
    [Fact]
    public async Task Save_DisqualifyWithoutReason_Refused()
    {
        using var db = CreateDb();
        var (s, _, _) = AddEntry(db);
        var ex = await Assert.ThrowsAsync<BusinessException>(() => Service(db).SaveAsync(s.Id, _judge1, Disqualify(null)));
        Assert.Equal(EvaluationFormVm.DisqualifiedReasonMessage, ex.Message);
    }

    /// <summary>BR-23: one judge disqualifying is enough – the entry is DISQUALIFIED without a score.</summary>
    [Fact]
    public async Task Save_OneJudgeDisqualifies_EntryDisqualified()
    {
        using var db = CreateDb();
        var (s, c, t) = AddEntry(db, twoJudges: true);

        await Service(db).SaveAsync(s.Id, _judge1, Form(c, 95m, t, 95m));
        await Service(db).SaveAsync(s.Id, _judge2, Disqualify());

        var result = await db.Evaluations.SingleAsync();
        Assert.Equal(Rating.DISQUALIFIED, result.Rating);
        Assert.Null(result.TotalScore);
        Assert.Contains("Copied from a photo", result.NegativePoints);
        var dq = await db.JudgeEvaluations.Include(e => e.Scores).SingleAsync(e => e.JudgeId == _judge2);
        Assert.Null(dq.TotalScore);
        Assert.Empty(dq.Scores);
    }

    /// <summary>BR-14 / TC-M07-04: a late entry can only be disqualified.</summary>
    [Fact]
    public async Task Save_LateEntryScored_Refused()
    {
        using var db = CreateDb();
        var (s, c, t) = AddEntry(db, late: true);
        var ex = await Assert.ThrowsAsync<BusinessException>(() => Service(db).SaveAsync(s.Id, _judge1, Form(c, 90m, t, 90m)));
        Assert.Equal(EvaluationService.LateOnlyDisqualifiedMessage, ex.Message);

        await Service(db).SaveAsync(s.Id, _judge1, Disqualify("Late"));
        Assert.Equal(Rating.DISQUALIFIED, (await db.Evaluations.SingleAsync()).Rating);
    }

    /// <summary>Saving again updates the judge's grade in place (one row per judge and entry).</summary>
    [Fact]
    public async Task Save_Again_Updates()
    {
        using var db = CreateDb();
        var (s, c, t) = AddEntry(db);
        await Service(db).SaveAsync(s.Id, _judge1, Form(c, 70m, t, 70m));

        var created = await Service(db).SaveAsync(s.Id, _judge1, Form(c, 92m, t, 92m));

        Assert.False(created);
        var grade = await db.JudgeEvaluations.Include(e => e.Scores).SingleAsync();
        Assert.Equal(92m, grade.TotalScore);
        Assert.Equal(2, grade.Scores.Count);
        Assert.Equal(Rating.BEST, (await db.Evaluations.SingleAsync()).Rating);
    }

    /// <summary>BA-07: a grade changed in another window after the page was opened is not overwritten.</summary>
    [Fact]
    public async Task Save_StaleVersion_Refused()
    {
        using var db = CreateDb();
        var (s, c, t) = AddEntry(db);
        await Service(db).SaveAsync(s.Id, _judge1, Form(c, 70m, t, 70m));

        var form = Form(c, 80m, t, 80m);
        form.Version = "0";
        var ex = await Assert.ThrowsAsync<BusinessException>(() => Service(db).SaveAsync(s.Id, _judge1, form));
        Assert.Equal(EvaluationService.ChangedMeanwhileMessage, ex.Message);

        // A form opened before the first save ("" = no grade yet) is refused too
        form.Version = string.Empty;
        ex = await Assert.ThrowsAsync<BusinessException>(() => Service(db).SaveAsync(s.Id, _judge1, form));
        Assert.Equal(EvaluationService.ConcurrentCreateMessage, ex.Message);
    }

    /// <summary>Unknown entry → not found.</summary>
    [Fact]
    public async Task Save_UnknownEntry_NotFound()
    {
        using var db = CreateDb();
        await Assert.ThrowsAsync<NotFoundException>(() => Service(db).SaveAsync(999, _judge1, new EvaluationFormVm()));
    }

    // ------------------------------------------------------------------ deleting

    /// <summary>F-M07-05: a judge deletes their grade; the entry loses its result.</summary>
    [Fact]
    public async Task Delete_RemovesGradeAndResult()
    {
        using var db = CreateDb();
        var (s, c, t) = AddEntry(db);
        await Service(db).SaveAsync(s.Id, _judge1, Form(c, 90m, t, 90m));

        await Service(db).DeleteAsync(s.Id, _judge1);

        Assert.Empty(db.JudgeEvaluations);
        Assert.Empty(db.JudgeScores);
        Assert.Empty(db.Evaluations);
    }

    /// <summary>Nothing to delete, or results published → refused.</summary>
    [Fact]
    public async Task Delete_NoGradeOrGraded_Refused()
    {
        using var db = CreateDb();
        var (s, _, _) = AddEntry(db);
        var ex = await Assert.ThrowsAsync<BusinessException>(() => Service(db).DeleteAsync(s.Id, _judge1));
        Assert.Equal(EvaluationService.NoEvaluationMessage, ex.Message);

        var (g, _, _) = AddEntry(db, CompetitionStatus.GRADED);
        db.JudgeEvaluations.Add(new JudgeEvaluation { SubmissionId = g.Id, JudgeId = _judge1, PositivePoints = "x", TotalScore = 80m });
        db.SaveChanges();
        ex = await Assert.ThrowsAsync<BusinessException>(() => Service(db).DeleteAsync(g.Id, _judge1));
        Assert.Equal(EvaluationService.GradedReadOnlyMessage, ex.Message);
    }

    // ------------------------------------------------------------------ page

    /// <summary>BR-20: nobody sees an entry before the deadline; a teacher outside the jury never sees it.</summary>
    [Fact]
    public async Task Page_BeforeDeadlineOrNotJudge_NotFound()
    {
        using var db = CreateDb();
        var (open, _, _) = AddEntry(db, CompetitionStatus.ONGOING);
        await Assert.ThrowsAsync<NotFoundException>(() => Service(db).GetPageAsync(open.Id, _judge1));
        await Assert.ThrowsAsync<NotFoundException>(() => Service(db).GetPageAsync(open.Id, null));

        var (closed, _, _) = AddEntry(db);
        await Assert.ThrowsAsync<NotFoundException>(() => Service(db).GetPageAsync(closed.Id, _outsider));
    }

    /// <summary>The page shows the criteria, who in the jury graded (not their scores) and the judge's own form.</summary>
    [Fact]
    public async Task Page_JuryProgressAndOwnForm()
    {
        using var db = CreateDb();
        var (s, c, t) = AddEntry(db, twoJudges: true);
        await Service(db).SaveAsync(s.Id, _judge2, Form(c, 60m, t, 60m));

        var page = await Service(db).GetPageAsync(s.Id, _judge1);

        Assert.True(page.CanEvaluate);
        Assert.False(page.HasEvaluation);
        Assert.Equal(2, page.Criteria.Count);
        Assert.All(page.Form.Scores, x => Assert.Null(x.Score)); // judge2's scores are not shown
        Assert.Contains(page.Jury, j => j.Name == "Pham Thu Ha" && j.Done);
        Assert.Contains(page.Jury, j => j.IsMe && j.IsChair && !j.Done);
        Assert.Null(page.ScoreSheet);
        Assert.Equal(string.Empty, page.Form.Version);
    }

    /// <summary>F-M07-08: a late entry's form is suggested disqualified with the reason written.</summary>
    [Fact]
    public async Task Page_LateEntry_SuggestsDisqualification()
    {
        using var db = CreateDb();
        var (s, _, _) = AddEntry(db, late: true);
        var page = await Service(db).GetPageAsync(s.Id, _judge1);
        Assert.True(page.IsLate);
        Assert.True(page.Form.Disqualified);
        Assert.StartsWith("Submitted after the submission deadline", page.Form.NegativePoints);
    }

    // ------------------------------------------------------------------ score sheet (BR-24)

    /// <summary>The student sees the score sheet only after publishing; staff once the entry is complete; outsiders never.</summary>
    [Fact]
    public async Task ScoreSheet_ByViewer()
    {
        using var db = CreateDb();
        var (s, c, t) = AddEntry(db);
        await Service(db).SaveAsync(s.Id, _judge1, Form(c, 90m, t, 80m));
        var service = Service(db);

        Assert.Null(await service.GetScoreSheetAsync(s.Id, new ScoreSheetViewer(RoleNames.Student, _studentId)));
        Assert.Null(await service.GetScoreSheetAsync(s.Id, new ScoreSheetViewer(RoleNames.Teacher, _judge1)));
        var staffSheet = await service.GetScoreSheetAsync(s.Id, new ScoreSheetViewer(RoleNames.Staff, _staff));
        Assert.NotNull(staffSheet);
        Assert.Equal(86m, staffSheet!.Total);

        // Published: the owner and the jury see it, another teacher does not
        var competition = await db.Competitions.SingleAsync();
        competition.Status = CompetitionStatus.GRADED;
        db.SaveChanges();
        var sheet = await service.GetScoreSheetAsync(s.Id, new ScoreSheetViewer(RoleNames.Student, _studentId));
        Assert.NotNull(sheet);
        var judge = Assert.Single(sheet!.Judges);
        Assert.Equal(90m, judge.Scores[c].Score);
        Assert.NotNull(await service.GetScoreSheetAsync(s.Id, new ScoreSheetViewer(RoleNames.Teacher, _judge1)));
        Assert.Null(await service.GetScoreSheetAsync(s.Id, new ScoreSheetViewer(RoleNames.Teacher, _outsider)));
        Assert.Null(await service.GetScoreSheetAsync(s.Id, new ScoreSheetViewer(RoleNames.Student, _studentId + 99)));
    }
}
