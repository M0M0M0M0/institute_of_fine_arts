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

/// <summary>Mobile grading API (GradingApiController): JSON shape, evaluation rules via the service, rowVersion, role.</summary>
public class GradingApiTests
{
    private static readonly DateOnly Today = DateTimeHelper.Today;

    private long _staff1;
    private long _student;

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
        db.StaffProfiles.Add(staff);
        db.StudentProfiles.Add(student);
        db.SaveChanges();
        _staff1 = staff.Id;
        _student = student.Id;
        return db;
    }

    private GradingApiController Api(AppDbContext db)
    {
        var controller = new GradingApiController(db, new EvaluationService(db, NullLogger<EvaluationService>.Instance));
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "1"),
            new Claim(ClaimsPrincipalExtensions.ProfileIdClaim, _staff1.ToString()),
        ], "test");
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) } };
        return controller;
    }

    private Competition AddCompetition(AppDbContext db, CompetitionStatus status = CompetitionStatus.CLOSED)
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
        db.SaveChanges();
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

    private static GradingEvaluationRequest Request(Rating? rating = Rating.BEST, string? positive = "Strong", string? rowVersion = null)
        => new() { Rating = rating, PositivePoints = positive, RowVersion = rowVersion };

    private static T Value<T>(ActionResult<T> result) => Assert.IsType<T>(result.Value);

    [Fact]
    public void Controller_IsStaffOnly()
    {
        var auth = typeof(GradingApiController).GetCustomAttributes<AuthorizeAttribute>(false).Single();
        Assert.Equal(RoleNames.Staff, auth!.Roles);
    }

    [Fact]
    public async Task Competitions_ListsGradableWithProgress()
    {
        using var db = CreateDb();
        var closed = AddCompetition(db);
        var e1 = AddEntry(db, closed);
        AddEntry(db, closed, "Second", 11);
        AddCompetition(db, CompetitionStatus.UPCOMING); // no entries, not gradable → hidden
        await Api(db).Save(e1.Id, Request());

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
        await Api(db).Save(e1.Id, Request(Rating.GOOD));

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
    }

    [Fact]
    public async Task Detail_NotClosed_LockedWithReason()
    {
        using var db = CreateDb();
        var c = AddCompetition(db, CompetitionStatus.ONGOING);
        var e = AddEntry(db, c);

        var d = Value(await Api(db).Detail(e.Id));

        Assert.False(d.CanEvaluate);
        Assert.Equal(EvaluationService.NotClosedMessage, d.LockReason);
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

        var d = Value(await Api(db).Save(e1.Id, Request(Rating.BETTER, "  Good light ")));

        Assert.NotNull(d.Evaluation);
        Assert.Equal(Rating.BETTER, d.Evaluation!.Rating);
        Assert.Equal("Good light", d.Evaluation.PositivePoints);
        Assert.Equal("Le Minh Hoang", d.Evaluation.EvaluatedByName);
        Assert.False(string.IsNullOrEmpty(d.Evaluation.RowVersion));
        Assert.True(d.CanDeleteEvaluation);
        Assert.Equal(e2.Id, d.NextUnevaluatedId);
        Assert.Equal(1, d.Evaluated);
        Assert.Equal(_staff1, (await db.Evaluations.SingleAsync()).EvaluatedBy);
    }

    [Fact]
    public async Task Save_Existing_WithCurrentRowVersion_Updates()
    {
        using var db = CreateDb();
        var c = AddCompetition(db);
        var e = AddEntry(db, c);
        var first = Value(await Api(db).Save(e.Id, Request(Rating.GOOD)));

        var second = Value(await Api(db).Save(e.Id, Request(Rating.BEST, "Better", first.Evaluation!.RowVersion)));

        Assert.Equal(Rating.BEST, second.Evaluation!.Rating);
        Assert.Single(db.Evaluations);
    }

    [Fact]
    public async Task Save_Existing_StaleOrMissingRowVersion_Conflict()
    {
        using var db = CreateDb();
        var c = AddCompetition(db);
        var e = AddEntry(db, c);
        await Api(db).Save(e.Id, Request(Rating.GOOD));

        var stale = await Api(db).Save(e.Id, Request(Rating.BEST, "x", "123"));
        var missing = await Api(db).Save(e.Id, Request(Rating.BEST, "x", null));

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

        await Assert.ThrowsAsync<BusinessException>(() => Api(db).Save(open.Id, Request()));
        await Assert.ThrowsAsync<BusinessException>(() => Api(db).Save(closed.Id, Request(Rating.GOOD, "  ")));
        await Assert.ThrowsAsync<BusinessException>(() => Api(db).Save(closed.Id, Request(null)));
        await Assert.ThrowsAsync<NotFoundException>(() => Api(db).Save(404, Request()));
    }

    [Fact]
    public async Task Delete_CurrentRowVersion_NoContent_StaleConflict()
    {
        using var db = CreateDb();
        var c = AddCompetition(db);
        var e = AddEntry(db, c);
        var d = Value(await Api(db).Save(e.Id, Request()));

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
        var d = Value(await Api(db).Save(e.Id, Request(Rating.BEST)));

        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(d, options));
        var root = json.RootElement;

        foreach (var key in new[]
        {
            "id", "competitionId", "competitionTitle", "competitionStatus", "title", "description", "textType", "textContent",
            "studentName", "studentCode", "submittedAt", "updatedAt", "canEvaluate", "canDeleteEvaluation", "lockReason",
            "evaluation", "nextUnevaluatedId", "total", "evaluated",
        })
        {
            Assert.True(root.TryGetProperty(key, out _), key);
        }
        Assert.Equal("CLOSED", root.GetProperty("competitionStatus").GetString());
        Assert.Equal("BEST", root.GetProperty("evaluation").GetProperty("rating").GetString());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("nextUnevaluatedId").ValueKind);

        // The app sends "Best": the case-insensitive converter must read it
        var body = JsonSerializer.Deserialize<GradingEvaluationRequest>("{\"rating\":\"Best\",\"positivePoints\":\"x\"}", options);
        Assert.Equal(Rating.BEST, body!.Rating);
    }
}
