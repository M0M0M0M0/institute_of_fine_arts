using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using InstituteOfFineArts.Web.Controllers.Api;
using InstituteOfFineArts.Web.Controllers.Api.Dto;
using InstituteOfFineArts.Web.Data;
using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Helpers;
using InstituteOfFineArts.Web.Infrastructure.Security;
using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.Services.Implementations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace InstituteOfFineArts.Tests.Api;

/// <summary>
/// Mobile grading API (GradingApiController, jury + criteria since 11/2026): JSON shape, grading rules via the service,
/// rowVersion, roles; a teacher sees only competitions they judge.
/// </summary>
public class GradingApiTests
{
    private static readonly DateOnly Today = DateTimeHelper.Today;

    private long _staff1;
    private long _teacher;
    private long _student;

    /// <summary>The only criterion (100 %) of each competition.</summary>
    private readonly Dictionary<long, long> _criterion = new();

    private AppDbContext CreateDb()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var staff = new StaffProfile
        {
            StaffCode = "NV0003", JoinedDate = new DateOnly(2024, 1, 1),
            Account = new Account { Username = "staff01", FullName = "Le Minh Hoang", Email = "s1@ifa.local", Role = Role.STAFF, PasswordHash = "x" },
        };
        var student = new StudentProfile
        {
            StudentCode = "HV0001", EnrollmentDate = new DateOnly(2025, 9, 1), CourseName = "Painting",
            Account = new Account { Username = "student01", FullName = "Nguyen Van An", Email = "st@ifa.local", Role = Role.STUDENT, PasswordHash = "x" },
        };
        var teacher = new StaffProfile
        {
            StaffCode = "NV0005", JoinedDate = new DateOnly(2024, 1, 1),
            Account = new Account { Username = "teacher01", FullName = "Pham Thu Ha", Email = "t1@ifa.local", Role = Role.TEACHER, PasswordHash = "x" },
        };
        db.StaffProfiles.AddRange(staff, teacher);
        db.StudentProfiles.Add(student);
        db.SaveChanges();
        _staff1 = staff.Id;
        _teacher = teacher.Id;
        _student = student.Id;
        return db;
    }

    /// <summary>The API as a teacher (grades) or, with <paramref name="role"/> = STAFF, as staff (published results, read only).</summary>
    private GradingApiController Api(AppDbContext db, string role = RoleNames.Teacher)
    {
        var controller = new GradingApiController(db, new EvaluationService(db, NullLogger<EvaluationService>.Instance));
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "1"),
            new Claim(ClaimsPrincipalExtensions.ProfileIdClaim, (role == RoleNames.Teacher ? _teacher : _staff1).ToString()),
            new Claim(ClaimTypes.Role, role),
        ], "test");
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) } };
        return controller;
    }

    /// <summary>A competition judged by the teacher (unless <paramref name="judged"/> is false) with one criterion.</summary>
    private Competition AddCompetition(AppDbContext db, CompetitionStatus status = CompetitionStatus.CLOSED, bool judged = true)
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
            Status = status == CompetitionStatus.GRADED ? CompetitionStatus.GRADED : CompetitionStatus.UPCOMING, CreatedBy = _staff1,
        };
        db.Competitions.Add(c);
        var criterion = new GradingCriterion { Name = "Overall " + Guid.NewGuid().ToString("N")[..6] };
        db.GradingCriteria.Add(criterion);
        db.SaveChanges();
        var cc = new CompetitionCriterion { CompetitionId = c.Id, CriterionId = criterion.Id, WeightPercent = 100m };
        db.CompetitionCriteria.Add(cc);
        if (judged)
        {
            db.CompetitionJudges.Add(new CompetitionJudge { CompetitionId = c.Id, JudgeId = _teacher, IsChair = true });
        }
        db.SaveChanges();
        _criterion[c.Id] = cc.Id;
        return c;
    }

    private Submission AddEntry(AppDbContext db, Competition c, string title = "Neon Street", int hour = 10)
    {
        var s = new Submission
        {
            CompetitionId = c.Id, StudentId = _student, Title = title, ImagePath = "p.jpg", OriginalFileName = "p.jpg",
            FileSize = 10, TextContent = "A poem", Description = "Why", SubmittedAt = c.StartDate.ToDateTime(new TimeOnly(hour, 0)),
        };
        db.Submissions.Add(s);
        db.SaveChanges();
        return s;
    }

    /// <summary>A grade of the entry: <paramref name="score"/> on its only criterion (92 → BEST, 85 → BETTER, 70 → GOOD).</summary>
    private GradingEvaluationRequest Request(Submission e, decimal? score = 92m, string? positive = "Strong", string? rowVersion = null)
        => new()
        {
            Scores = [new GradingScoreDto { CriterionId = _criterion[e.CompetitionId], Score = score }],
            PositivePoints = positive, RowVersion = rowVersion,
        };

    private static T Value<T>(ActionResult<T> result) => Assert.IsType<T>(result.Value);

    /// <summary>Teachers and staff may read; only teachers may save or delete an evaluation (staff do not grade).</summary>
    [Fact]
    public void Controller_TeachersGrade_StaffRead()
    {
        var auth = typeof(GradingApiController).GetCustomAttributes<AuthorizeAttribute>(false).Single();
        Assert.Equal(RoleNames.Teacher + "," + RoleNames.Staff, auth!.Roles);
        foreach (var name in new[] { nameof(GradingApiController.Save), nameof(GradingApiController.DeleteEvaluation) })
        {
            var write = typeof(GradingApiController).GetMethod(name)!.GetCustomAttributes<AuthorizeAttribute>(false).Single();
            Assert.Equal(RoleNames.Teacher, write.Roles);
        }
    }

    /// <summary>
    /// Staff see published (GRADED) competitions only, read only: a CLOSED one (being graded) is not listed and its
    /// entries answer 404; a GRADED entry is shown without the grading buttons.
    /// </summary>
    [Fact]
    public async Task Staff_OnlyPublishedResults_ReadOnly()
    {
        using var db = CreateDb();
        var closed = AddCompetition(db);
        var closedEntry = AddEntry(db, closed);
        var graded = AddCompetition(db, CompetitionStatus.GRADED);
        var gradedEntry = AddEntry(db, graded, "Published");

        var list = Value(await Api(db, RoleNames.Staff).Competitions());
        Assert.Equal(graded.Id, Assert.Single(list).Id);
        await Assert.ThrowsAsync<NotFoundException>(() => Api(db, RoleNames.Staff).Detail(closedEntry.Id));
        await Assert.ThrowsAsync<NotFoundException>(() => Api(db, RoleNames.Staff).Submissions(closed.Id, null, null, null));

        var detail = Value(await Api(db, RoleNames.Staff).Detail(gradedEntry.Id));
        Assert.False(detail.CanEvaluate);
        Assert.False(detail.CanDeleteEvaluation);
    }

    /// <summary>A teacher never sees an entry of a competition that still takes entries (404 through the API too).</summary>
    [Fact]
    public async Task Teacher_OpenCompetition_NotFound()
    {
        using var db = CreateDb();
        var open = AddCompetition(db, CompetitionStatus.ONGOING);
        var entry = AddEntry(db, open);

        Assert.DoesNotContain(Value(await Api(db).Competitions()), c => c.Id == open.Id);
        await Assert.ThrowsAsync<NotFoundException>(() => Api(db).Detail(entry.Id));
        await Assert.ThrowsAsync<NotFoundException>(() => Api(db).Submissions(open.Id, null, null, null));
    }

    [Fact]
    public async Task Competitions_ListsGradableWithProgress()
    {
        using var db = CreateDb();
        var closed = AddCompetition(db);
        var e1 = AddEntry(db, closed);
        AddEntry(db, closed, "Second", 11);
        AddCompetition(db, CompetitionStatus.UPCOMING); // no entries, not gradable → hidden
        AddEntry(db, AddCompetition(db, judged: false), "Not mine"); // another jury → hidden
        await Api(db).Save(e1.Id, Request(e1));

        var list = Value(await Api(db).Competitions());

        var item = Assert.Single(list);
        Assert.Equal(closed.Id, item.Id);
        Assert.Equal(CompetitionStatus.CLOSED, item.Status);
        Assert.Equal(2, item.SubmissionCount);
        Assert.Equal(1, item.EvaluatedCount);
    }

    [Fact]
    public async Task List_FiltersButKeepsOverallProgress()
    {
        using var db = CreateDb();
        var c = AddCompetition(db);
        var e1 = AddEntry(db, c, "Neon Street", 9);
        AddEntry(db, c, "Harbour", 10);
        await Api(db).Save(e1.Id, Request(e1, 70m));

        var pending = Value(await Api(db).Submissions(c.Id, false, null, null));
        Assert.Equal(2, pending.Total);
        Assert.Equal(1, pending.Evaluated);
        Assert.Equal("Harbour", Assert.Single(pending.Items).Title);
        Assert.Null(pending.Items[0].Rating);

        var good = Value(await Api(db).Submissions(c.Id, null, Rating.GOOD, null));
        Assert.Equal(Rating.GOOD, Assert.Single(good.Items).Rating);

        var search = Value(await Api(db).Submissions(c.Id, null, null, "HV0001"));
        Assert.Equal(2, search.Items.Count);
        Assert.Equal("Neon Street", search.Items[0].Title); // oldest first
    }

    [Fact]
    public async Task List_UnknownCompetition_NotFound()
    {
        using var db = CreateDb();
        await Assert.ThrowsAsync<NotFoundException>(() => Api(db).Submissions(999, null, null, null));
    }

    /// <summary>An entry of a competition the teacher does not judge → 404.</summary>
    [Fact]
    public async Task Detail_NotOnJury_NotFound()
    {
        using var db = CreateDb();
        var e = AddEntry(db, AddCompetition(db, judged: false));
        await Assert.ThrowsAsync<NotFoundException>(() => Api(db).Detail(e.Id));
        await Assert.ThrowsAsync<NotFoundException>(() => Api(db).Save(e.Id, Request(e)));
    }

    [Fact]
    public async Task Detail_Unevaluated_HasNextAndCanEvaluate()
    {
        using var db = CreateDb();
        var c = AddCompetition(db);
        var e1 = AddEntry(db, c, "A", 9);
        var e2 = AddEntry(db, c, "B", 10);

        var d = Value(await Api(db).Detail(e1.Id));

        Assert.True(d.CanEvaluate);
        Assert.False(d.CanDeleteEvaluation);
        Assert.Null(d.LockReason);
        Assert.Null(d.Evaluation);
        Assert.Equal(e2.Id, d.NextUnevaluatedId);
        Assert.Equal(2, d.Total);
        Assert.Equal(0, d.Evaluated);
        Assert.Equal("HV0001", d.StudentCode);
        Assert.Equal(_criterion[c.Id], Assert.Single(d.Criteria).Id);
        Assert.True(Assert.Single(d.Jury).IsMe);
    }

    [Fact]
    public async Task Detail_Graded_LockedWithReason()
    {
        // An open competition is not shown at all (Teacher_OpenCompetition_NotFound); a published one is read only
        using var db = CreateDb();
        var c = AddCompetition(db, CompetitionStatus.GRADED);
        var e = AddEntry(db, c);

        var d = Value(await Api(db).Detail(e.Id));

        Assert.False(d.CanEvaluate);
        Assert.Equal(EvaluationService.GradedReadOnlyMessage, d.LockReason);
    }

    [Fact]
    public async Task Detail_Unknown_NotFound()
    {
        using var db = CreateDb();
        await Assert.ThrowsAsync<NotFoundException>(() => Api(db).Detail(404));
    }

    [Fact]
    public async Task Save_Creates_ThenReturnsDetailWithEvaluationAndNext()
    {
        using var db = CreateDb();
        var c = AddCompetition(db);
        var e1 = AddEntry(db, c, "A", 9);
        var e2 = AddEntry(db, c, "B", 10);

        var d = Value(await Api(db).Save(e1.Id, Request(e1, 85m, "  Good light ")));

        Assert.NotNull(d.Evaluation);
        Assert.Equal(85m, d.Evaluation!.TotalScore);
        Assert.Equal(85m, Assert.Single(d.Evaluation.Scores).Score);
        Assert.Equal(Rating.BETTER, d.ResultRating);
        Assert.Equal("<p>Good light</p>", d.Evaluation.PositivePoints); // rich text (BR-50)
        Assert.Equal("Pham Thu Ha", d.Evaluation.EvaluatedByName);
        Assert.False(string.IsNullOrEmpty(d.Evaluation.RowVersion));
        Assert.True(d.CanDeleteEvaluation);
        Assert.Equal(e2.Id, d.NextUnevaluatedId);
        Assert.Equal(1, d.Evaluated);
        Assert.Equal(_teacher, (await db.Evaluations.SingleAsync()).EvaluatedBy);
    }

    [Fact]
    public async Task Save_Existing_WithCurrentRowVersion_Updates()
    {
        using var db = CreateDb();
        var c = AddCompetition(db);
        var e = AddEntry(db, c);
        var first = Value(await Api(db).Save(e.Id, Request(e, 70m)));

        var second = Value(await Api(db).Save(e.Id, Request(e, 92m, "Better", first.Evaluation!.RowVersion)));

        Assert.Equal(Rating.BEST, second.ResultRating);
        Assert.Single(db.JudgeEvaluations);
        Assert.Single(db.Evaluations);
    }

    [Fact]
    public async Task Save_Existing_StaleOrMissingRowVersion_Conflict()
    {
        using var db = CreateDb();
        var c = AddCompetition(db);
        var e = AddEntry(db, c);
        await Api(db).Save(e.Id, Request(e, 70m));

        var stale = await Api(db).Save(e.Id, Request(e, 92m, "x", "123"));
        var missing = await Api(db).Save(e.Id, Request(e, 92m, "x", null));

        Assert.Equal(409, Assert.IsType<ObjectResult>(stale.Result).StatusCode);
        Assert.Equal(409, Assert.IsType<ObjectResult>(missing.Result).StatusCode);
        Assert.Equal(Rating.GOOD, (await db.Evaluations.SingleAsync()).Rating);
    }

    [Fact]
    public async Task Save_BusinessRules_Throw()
    {
        using var db = CreateDb();
        var open = AddEntry(db, AddCompetition(db, CompetitionStatus.ONGOING));
        var closed = AddEntry(db, AddCompetition(db));

        // Open competition: the entry is not visible to the jury yet, so saving answers 404 like an unknown entry
        await Assert.ThrowsAsync<NotFoundException>(() => Api(db).Save(open.Id, Request(open)));
        await Assert.ThrowsAsync<BusinessException>(() => Api(db).Save(closed.Id, Request(closed, 70m, "  ")));
        await Assert.ThrowsAsync<BusinessException>(() => Api(db).Save(closed.Id, Request(closed, null)));
        await Assert.ThrowsAsync<BusinessException>(() => Api(db).Save(closed.Id, Request(closed, 101m)));
        await Assert.ThrowsAsync<NotFoundException>(() => Api(db).Save(404, Request(closed)));
    }

    [Fact]
    public async Task Delete_CurrentRowVersion_NoContent_StaleConflict()
    {
        using var db = CreateDb();
        var c = AddCompetition(db);
        var e = AddEntry(db, c);
        var d = Value(await Api(db).Save(e.Id, Request(e)));

        var stale = await Api(db).DeleteEvaluation(e.Id, "1");
        Assert.Equal(409, Assert.IsType<ObjectResult>(stale).StatusCode);
        Assert.Single(db.Evaluations);

        var ok = await Api(db).DeleteEvaluation(e.Id, d.Evaluation!.RowVersion);
        Assert.IsType<NoContentResult>(ok);
        Assert.Empty(db.Evaluations);
    }

    [Fact]
    public async Task Delete_Unknown_NotFound_NoEvaluation_Business()
    {
        using var db = CreateDb();
        var e = AddEntry(db, AddCompetition(db));

        await Assert.ThrowsAsync<NotFoundException>(() => Api(db).DeleteEvaluation(404, null));
        await Assert.ThrowsAsync<BusinessException>(() => Api(db).DeleteEvaluation(e.Id, null));
    }

    [Fact]
    public async Task Json_HasContractKeys_AndEnumsAsText()
    {
        using var db = CreateDb();
        var c = AddCompetition(db);
        var e = AddEntry(db, c);
        var d = Value(await Api(db).Save(e.Id, Request(e)));

        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(d, options));
        var root = json.RootElement;

        foreach (var key in new[]
        {
            "id", "competitionId", "competitionTitle", "competitionStatus", "title", "description", "textType", "textContent",
            "studentName", "studentCode", "submittedAt", "updatedAt", "canEvaluate", "canDeleteEvaluation", "lockReason",
            "evaluation", "nextUnevaluatedId", "total", "evaluated", "criteria", "jury", "resultRating", "resultScore",
            "scoreSheet", "imageIds",
        })
        {
            Assert.True(root.TryGetProperty(key, out _), key);
        }
        Assert.Equal("CLOSED", root.GetProperty("competitionStatus").GetString());
        Assert.Equal("BEST", root.GetProperty("resultRating").GetString());
        Assert.Equal(92m, root.GetProperty("evaluation").GetProperty("totalScore").GetDecimal());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("nextUnevaluatedId").ValueKind);

        // The body the app sends
        var body = JsonSerializer.Deserialize<GradingEvaluationRequest>(
            "{\"disqualified\":false,\"scores\":[{\"criterionId\":5,\"score\":88.5,\"comment\":\"ok\"}],\"positivePoints\":\"x\"}", options);
        Assert.Equal(88.5m, Assert.Single(body!.Scores).Score);
    }
}
