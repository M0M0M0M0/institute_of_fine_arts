using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using InstituteOfFineArts.Web.Controllers.Api;
using InstituteOfFineArts.Web.Controllers.Api.Dto;
using InstituteOfFineArts.Web.Data;
using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Helpers;
using InstituteOfFineArts.Web.Infrastructure.FileStorage;
using InstituteOfFineArts.Web.Infrastructure.Security;
using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.Services.Implementations;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace InstituteOfFineArts.Tests.Api;

/// <summary>
/// Mobile API, public data + student submissions: JSON shape the Flutter app reads, ownership, deadline, duplicate (409 + submissionId)
/// and the image rules shared with FilesController.
/// </summary>
public sealed class PublicAndSubmissionsApiTests : IDisposable
{
    private static readonly DateOnly Today = DateTimeHelper.Today;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _contentRoot = Path.Combine(Path.GetTempPath(), "ifa-api-tests-" + Guid.NewGuid().ToString("N"));
    private readonly FileStorageService _files;
    private readonly AppDbContext _db = new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    public PublicAndSubmissionsApiTests()
    {
        var env = new Mock<IWebHostEnvironment>();
        env.SetupGet(e => e.ContentRootPath).Returns(_contentRoot);
        var options = Options.Create(new UploadOptions { RootPath = "uploads", MaxFileSizeBytes = 5 * 1024 * 1024, ThumbnailWidth = 400 });
        _files = new FileStorageService(options, env.Object, new ThumbnailService(), NullLogger<FileStorageService>.Instance);
    }

    public void Dispose()
    {
        _db.Dispose();
        if (Directory.Exists(_contentRoot)) Directory.Delete(_contentRoot, recursive: true);
    }

    // ------------------------------------------------------------------ helpers

    private static ClaimsPrincipal UserOf(string role, long profileId) => new(new ClaimsIdentity(new[]
    {
        new Claim(ClaimTypes.Role, role), new Claim(ClaimsPrincipalExtensions.ProfileIdClaim, profileId.ToString()),
    }, "Test"));

    private static T WithUser<T>(T controller, ClaimsPrincipal user) where T : ControllerBase
    {
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = user } };
        return controller;
    }

    private HomeAndCompetitionsApiController PublicApi()
        => WithUser(new HomeAndCompetitionsApiController(new PublicSiteService(_db), _db), new ClaimsPrincipal(new ClaimsIdentity()));

    private StudentSubmissionsApiController StudentApi(long profileId)
        => WithUser(new StudentSubmissionsApiController(new SubmissionService(_db, _files, NullLogger<SubmissionService>.Instance), _db),
            UserOf(RoleNames.Student, profileId));

    private SubmissionImagesApiController ImageApi(ClaimsPrincipal user)
        => WithUser(new SubmissionImagesApiController(new ImageAccessService(_db), _files,
            new WatermarkService(NullLogger<WatermarkService>.Instance)), user);

    private static JsonElement ToJson(object? value) => JsonSerializer.SerializeToElement(value, value?.GetType() ?? typeof(object), Json);

    /// <summary>Payload of an ActionResult&lt;T&gt; / ObjectResult / IActionResult.</summary>
    private static object? Payload<T>(ActionResult<T> r) => r.Value ?? (r.Result as ObjectResult)?.Value;

    private Competition AddCompetition(string title, int startIn, int endIn, CompetitionStatus stored = CompetitionStatus.ONGOING)
    {
        var c = new Competition
        {
            Code = "C" + Guid.NewGuid().ToString("N")[..8], Title = title, Description = "About", Rules = "Rules text",
            StartDate = Today.AddDays(startIn), EndDate = Today.AddDays(endIn), Status = stored, CreatedBy = 1,
        };
        _db.Competitions.Add(c);
        _db.SaveChanges();
        return c;
    }

    private StudentProfile AddStudent(string fullName)
    {
        var account = new Account { Username = "u" + Guid.NewGuid().ToString("N")[..6], FullName = fullName, Email = "x@x.vn", Role = Role.STUDENT };
        var profile = new StudentProfile { Account = account, StudentCode = "S" + Guid.NewGuid().ToString("N")[..6], CourseName = "Painting" };
        _db.StudentProfiles.Add(profile);
        _db.SaveChanges();
        return profile;
    }

    private Submission AddSubmission(Competition c, long student, string title = "Sunset", bool withFile = false, Rating? rating = null)
    {
        var s = new Submission
        {
            CompetitionId = c.Id, StudentId = student, Title = title, OriginalFileName = "sunset.jpg", FileSize = 10,
            ImagePath = $"submissions/{c.Id}/{Guid.NewGuid()}.jpg", TextType = TextType.POEM, TextContent = "Poem",
            Description = "Desc", SubmittedAt = DateTimeHelper.Now.AddHours(-1),
        };
        if (withFile)
        {
            var full = _files.ResolvePath(s.ImagePath);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllBytes(full, TestImages.Create(200, 150));
        }
        if (rating is { } r)
        {
            s.Evaluation = new Evaluation { Rating = r, PositivePoints = "Good colours", EvaluatedBy = 1, EvaluatedAt = DateTimeHelper.Now };
        }
        _db.Submissions.Add(s);
        _db.SaveChanges();
        return s;
    }

    private static SubmissionApiForm Form(string? title = "Falling leaves", string? textType = "Poem", bool image = true) => new()
    {
        Title = title, Description = "Autumn", TextType = textType, TextContent = "Red leaves", PublicConsent = "true",
        Image = image ? TestImages.AsFormFile(TestImages.Create(800, 600)) : null,
    };

    // ------------------------------------------------------------------ home + competitions

    [Fact]
    public async Task Home_ListsOpenCompetitionsAndLatestWinners()
    {
        var ongoing = AddCompetition("Autumn colours", -2, 5);
        AddCompetition("Winter", 3, 10, CompetitionStatus.UPCOMING);
        var graded = AddCompetition("Summer", -40, -30, CompetitionStatus.GRADED);
        graded.PublishedAt = DateTimeHelper.Now.AddDays(-20);
        var student = AddStudent("Nguyen An");
        var entry = AddSubmission(graded, student.Id, "Calm sea");
        var award = new CompetitionAward { CompetitionId = graded.Id, AwardName = "First prize", Rank = 1, PrizeValue = 2000000m, Quantity = 1 };
        _db.CompetitionAwards.Add(award);
        _db.SaveChanges();
        _db.AwardsIssued.Add(new AwardIssued { CompetitionAwardId = award.Id, SubmissionId = entry.Id, IssuedBy = 1, IssuedDate = Today });
        _db.SaveChanges();

        var result = (await PublicApi().Home()).Value;
        var json = ToJson(result);

        Assert.Equal("Summer", json.GetProperty("latestGradedCompetition").GetString());
        var winner = json.GetProperty("latestWinners")[0];
        Assert.Equal("Nguyen An", winner.GetProperty("studentName").GetString());
        Assert.Equal("First prize", winner.GetProperty("awardName").GetString());
        Assert.Equal(1, winner.GetProperty("rank").GetInt32());
        Assert.Equal("Calm sea", winner.GetProperty("paintingTitle").GetString());
        var on = json.GetProperty("ongoing")[0];
        Assert.Equal(ongoing.Id, on.GetProperty("id").GetInt64());
        Assert.Equal("ONGOING", on.GetProperty("status").GetString());
        Assert.Equal(5, on.GetProperty("daysLeft").GetInt32());
        Assert.Equal(0, on.GetProperty("submissionCount").GetInt32());
        Assert.Equal(JsonValueKind.Null, on.GetProperty("topic").ValueKind);
        Assert.Equal(3, json.GetProperty("upcoming")[0].GetProperty("daysUntilStart").GetInt32());
        Assert.Equal(Today.AddDays(5).ToString("yyyy-MM-dd"), on.GetProperty("endDate").GetString());
    }

    [Fact]
    public async Task Home_Empty_HasNullTitleAndEmptyLists()
    {
        var json = ToJson((await PublicApi().Home()).Value);
        Assert.Equal(JsonValueKind.Null, json.GetProperty("latestGradedCompetition").ValueKind);
        Assert.Empty(json.GetProperty("latestWinners").EnumerateArray());
    }

    [Fact]
    public async Task Competitions_ListAndDetail()
    {
        var c = AddCompetition("Autumn colours", -1, 4);
        _db.CompetitionAwards.Add(new CompetitionAward { CompetitionId = c.Id, AwardName = "Second", Rank = 2, PrizeValue = 500m, Quantity = 2, PrizeDescription = "Voucher" });
        _db.CompetitionAwards.Add(new CompetitionAward { CompetitionId = c.Id, AwardName = "First", Rank = 1, PrizeValue = 900m, Quantity = 1 });
        _db.SaveChanges();
        AddSubmission(c, 7);

        var list = ToJson((await PublicApi().List()).Value);
        Assert.Equal(1, list.GetArrayLength());
        Assert.Equal(1, list[0].GetProperty("submissionCount").GetInt32());
        Assert.Equal(4, list[0].GetProperty("daysLeft").GetInt32());

        var detail = ToJson((await PublicApi().Details(c.Id)).Value);
        Assert.Equal("About", detail.GetProperty("description").GetString());
        Assert.Equal("Rules text", detail.GetProperty("conditions").GetString());
        Assert.Equal(Today.AddDays(4).ToString("yyyy-MM-dd") + "T23:59:59", detail.GetProperty("deadline").GetString());
        var awards = detail.GetProperty("awards");
        Assert.Equal("First", awards[0].GetProperty("name").GetString());
        Assert.Equal(900m, awards[0].GetProperty("prizeValue").GetDecimal());
        Assert.Equal(2, awards[1].GetProperty("quantity").GetInt32());
        Assert.Equal("Voucher", awards[1].GetProperty("description").GetString());
    }

    [Fact]
    public async Task CompetitionDetail_Unknown_Throws404()
        => await Assert.ThrowsAsync<NotFoundException>(() => PublicApi().Details(999));

    [Fact]
    public async Task Awards_OnlyWhenGraded()
    {
        var open = AddCompetition("Open", -1, 4);
        Assert.Empty((await PublicApi().Awards(open.Id)).Value!);

        var graded = AddCompetition("Done", -20, -10, CompetitionStatus.GRADED);
        var student = AddStudent("Tran Binh");
        var entry = AddSubmission(graded, student.Id, "Mountains");
        var award = new CompetitionAward { CompetitionId = graded.Id, AwardName = "First", Rank = 1, PrizeValue = 2000000.5m, Quantity = 1 };
        _db.CompetitionAwards.Add(award);
        _db.SaveChanges();
        _db.AwardsIssued.Add(new AwardIssued { CompetitionAwardId = award.Id, SubmissionId = entry.Id, IssuedBy = 1, IssuedDate = Today });
        _db.SaveChanges();

        var json = ToJson((await PublicApi().Awards(graded.Id)).Value);
        Assert.Equal("Tran Binh", json[0].GetProperty("studentName").GetString());
        Assert.Equal("Mountains", json[0].GetProperty("paintingTitle").GetString());
        Assert.Equal(2000000.5m, json[0].GetProperty("prizeValue").GetDecimal());
        await Assert.ThrowsAsync<NotFoundException>(() => PublicApi().Awards(999));
    }

    [Fact]
    public async Task ResultAdjustment_AlwaysNull()
    {
        var c = AddCompetition("X", -1, 4);
        var ok = Assert.IsType<OkObjectResult>(await PublicApi().ResultAdjustment(c.Id));
        Assert.Equal(JsonValueKind.Null, ToJson(ok.Value).GetProperty("adjustedAt").ValueKind);
        await Assert.ThrowsAsync<NotFoundException>(() => PublicApi().ResultAdjustment(999));
    }

    // ------------------------------------------------------------------ submit

    [Fact]
    public async Task Create_Returns201WithDetail_AndMySubmissionFindsIt()
    {
        var c = AddCompetition("Autumn colours", -1, 4);
        var api = StudentApi(1);

        var created = Assert.IsType<CreatedResult>(await api.Create(c.Id, Form()));
        Assert.Equal(201, created.StatusCode);
        var json = ToJson(created.Value);
        Assert.Equal("Falling leaves", json.GetProperty("title").GetString());
        Assert.Equal("ONGOING", json.GetProperty("competitionStatus").GetString());
        Assert.Equal("POEM", json.GetProperty("textType").GetString());
        Assert.True(json.GetProperty("publicConsent").GetBoolean());
        Assert.True(json.GetProperty("canEdit").GetBoolean());
        Assert.Equal(JsonValueKind.Null, json.GetProperty("evaluation").ValueKind);
        Assert.Empty(json.GetProperty("awards").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, json.GetProperty("updatedAt").ValueKind);

        var id = json.GetProperty("id").GetInt64();
        var mine = ToJson(Assert.IsType<OkObjectResult>(await api.MySubmission(c.Id)).Value);
        Assert.Equal(id, mine.GetProperty("submissionId").GetInt64());
    }

    [Fact]
    public async Task MySubmission_NullWhenNone_404WhenCompetitionUnknown()
    {
        var c = AddCompetition("X", -1, 4);
        var json = ToJson(Assert.IsType<OkObjectResult>(await StudentApi(1).MySubmission(c.Id)).Value);
        Assert.Equal(JsonValueKind.Null, json.GetProperty("submissionId").ValueKind);
        await Assert.ThrowsAsync<NotFoundException>(() => StudentApi(1).MySubmission(999));
    }

    [Fact]
    public async Task Create_Duplicate_Returns409WithExistingId()
    {
        var c = AddCompetition("X", -1, 4);
        var existing = AddSubmission(c, 1);

        var result = Assert.IsType<ObjectResult>(await StudentApi(1).Create(c.Id, Form()));
        Assert.Equal(409, result.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(result.Value);
        Assert.Equal(SubmissionService.AlreadySubmittedMessage, problem.Title);
        Assert.Equal(existing.Id, Convert.ToInt64(problem.Extensions["submissionId"]));
    }

    [Fact]
    public async Task Create_AfterDeadline_ThrowsServiceMessage()
    {
        var c = AddCompetition("Old", -10, -2, CompetitionStatus.CLOSED);
        var ex = await Assert.ThrowsAsync<BusinessException>(() => StudentApi(1).Create(c.Id, Form()));
        Assert.Equal(SubmissionService.DeadlinePassedMessage, ex.Message);
    }

    [Fact]
    public async Task Create_MissingTitleOrBadTextTypeOrImage_ThrowsServiceMessage()
    {
        var c = AddCompetition("X", -1, 4);
        var noTitle = await Assert.ThrowsAsync<BusinessException>(() => StudentApi(1).Create(c.Id, Form(title: null)));
        Assert.Equal("Please enter a title", noTitle.Message);
        var badType = await Assert.ThrowsAsync<BusinessException>(() => StudentApi(1).Create(c.Id, Form(textType: "song")));
        Assert.Equal("Please choose the text type", badType.Message);
        await Assert.ThrowsAsync<BusinessException>(() => StudentApi(1).Create(c.Id, Form(image: false)));
        Assert.Empty(_db.Submissions);
    }

    [Fact]
    public async Task Create_UnknownCompetition_Throws404()
        => await Assert.ThrowsAsync<NotFoundException>(() => StudentApi(1).Create(999, Form()));

    // ------------------------------------------------------------------ read / update / delete

    [Fact]
    public async Task Details_OnlyForOwner()
    {
        var c = AddCompetition("X", -1, 4);
        var s = AddSubmission(c, 1);
        Assert.Equal(s.Id, ToJson((await StudentApi(1).Details(s.Id)).Value).GetProperty("id").GetInt64());
        await Assert.ThrowsAsync<NotFoundException>(() => StudentApi(2).Details(s.Id));
    }

    [Fact]
    public async Task Details_Graded_ShowsEvaluationAndAward_NotBeforePublished()
    {
        var graded = AddCompetition("Done", -20, -10, CompetitionStatus.GRADED);
        var s = AddSubmission(graded, 1, rating: Rating.BEST);
        var award = new CompetitionAward { CompetitionId = graded.Id, AwardName = "First", Rank = 1, PrizeValue = 1m, Quantity = 1 };
        _db.CompetitionAwards.Add(award);
        _db.SaveChanges();
        _db.AwardsIssued.Add(new AwardIssued { CompetitionAwardId = award.Id, SubmissionId = s.Id, IssuedBy = 1, IssuedDate = Today });
        _db.SaveChanges();

        var json = ToJson((await StudentApi(1).Details(s.Id)).Value);
        Assert.False(json.GetProperty("canEdit").GetBoolean());
        Assert.Equal("BEST", json.GetProperty("evaluation").GetProperty("rating").GetString());
        Assert.Equal("Good colours", json.GetProperty("evaluation").GetProperty("positivePoints").GetString());
        Assert.Equal("First", json.GetProperty("awards")[0].GetProperty("awardName").GetString());

        var closed = AddCompetition("Grading", -10, -2, CompetitionStatus.CLOSED);
        var hidden = AddSubmission(closed, 1, rating: Rating.BEST);
        var hiddenJson = ToJson((await StudentApi(1).Details(hidden.Id)).Value);
        Assert.Equal("CLOSED", hiddenJson.GetProperty("competitionStatus").GetString());
        Assert.Equal(JsonValueKind.Null, hiddenJson.GetProperty("evaluation").ValueKind);
    }

    [Fact]
    public async Task Update_ChangesTexts_WithoutNewImage()
    {
        var c = AddCompetition("X", -1, 4);
        var s = AddSubmission(c, 1);
        var form = Form(title: "New title", textType: "story", image: false);

        var json = ToJson((await StudentApi(1).Update(s.Id, form)).Value);

        Assert.Equal("New title", json.GetProperty("title").GetString());
        Assert.Equal("STORY", json.GetProperty("textType").GetString());
        Assert.Equal("sunset.jpg", json.GetProperty("originalFileName").GetString());
        await Assert.ThrowsAsync<NotFoundException>(() => StudentApi(2).Update(s.Id, Form(image: false)));
    }

    [Fact]
    public async Task Update_AfterDeadline_Throws()
    {
        var c = AddCompetition("Old", -10, -2, CompetitionStatus.CLOSED);
        var s = AddSubmission(c, 1);
        await Assert.ThrowsAsync<BusinessException>(() => StudentApi(1).Update(s.Id, Form(image: false)));
        await Assert.ThrowsAsync<BusinessException>(() => StudentApi(1).Delete(s.Id));
    }

    [Fact]
    public async Task Delete_Own_Returns204_OtherIs404()
    {
        var c = AddCompetition("X", -1, 4);
        var s = AddSubmission(c, 1);
        await Assert.ThrowsAsync<NotFoundException>(() => StudentApi(2).Delete(s.Id));
        Assert.IsType<NoContentResult>(await StudentApi(1).Delete(s.Id));
        Assert.Empty(_db.Submissions);
    }

    [Fact]
    public async Task Mine_FiltersAndRatingOnlyWhenGraded()
    {
        var open = AddCompetition("Open", -1, 4);
        var graded = AddCompetition("Done", -20, -10, CompetitionStatus.GRADED);
        var closed = AddCompetition("Grading", -10, -2, CompetitionStatus.CLOSED);
        AddSubmission(open, 1, "Sunset");
        AddSubmission(graded, 1, "Sea", rating: Rating.BEST);
        AddSubmission(closed, 1, "Hidden best", rating: Rating.BEST);
        AddSubmission(open, 2, "Not mine");
        var api = StudentApi(1);

        var all = ToJson((await api.Mine(null, null, null, null, null)).Value);
        Assert.Equal(3, all.GetArrayLength());
        var seaRow = all.EnumerateArray().Single(e => e.GetProperty("title").GetString() == "Sea");
        Assert.Equal("BEST", seaRow.GetProperty("rating").GetString());
        Assert.False(seaRow.GetProperty("canEdit").GetBoolean());
        Assert.Equal("GRADED", seaRow.GetProperty("competitionStatus").GetString());
        var openRow = all.EnumerateArray().Single(e => e.GetProperty("title").GetString() == "Sunset");
        Assert.True(openRow.GetProperty("canEdit").GetBoolean());
        Assert.Equal(JsonValueKind.Null, openRow.GetProperty("rating").ValueKind);
        var hiddenRow = all.EnumerateArray().Single(e => e.GetProperty("title").GetString() == "Hidden best");
        Assert.Equal(JsonValueKind.Null, hiddenRow.GetProperty("rating").ValueKind);

        Assert.Single((await api.Mine(open.Id, null, null, null, null)).Value!);
        Assert.Single((await api.Mine(null, "Sun", null, null, null)).Value!);
        Assert.Single((await api.Mine(null, null, null, null, Rating.BEST)).Value!);
        Assert.Equal(3, (await api.Mine(null, null, Today.AddDays(-1), Today, null)).Value!.Count);
        Assert.Empty((await api.Mine(null, null, Today.AddDays(-9), Today.AddDays(-5), null)).Value!);
        await Assert.ThrowsAsync<BusinessException>(() => api.Mine(null, null, Today, Today.AddDays(-3), null));
    }

    // ------------------------------------------------------------------ image

    [Fact]
    public async Task Image_OwnerGetsOriginal_OtherStudentGets404_StaffGetsDownload()
    {
        var c = AddCompetition("X", -1, 4);
        var s = AddSubmission(c, 1, withFile: true);

        var own = Assert.IsType<FileStreamResult>(await ImageApi(UserOf(RoleNames.Student, 1)).Image(s.Id));
        Assert.Equal("image/jpeg", own.ContentType);
        own.FileStream.Dispose();

        var other = Assert.IsType<ObjectResult>(await ImageApi(UserOf(RoleNames.Student, 2)).Image(s.Id));
        Assert.Equal(404, other.StatusCode);

        var staff = Assert.IsType<FileStreamResult>(await ImageApi(UserOf(RoleNames.Staff, 5)).Image(s.Id, download: true));
        Assert.Equal("sunset.jpg", staff.FileDownloadName);
        staff.FileStream.Dispose();

        var missing = Assert.IsType<ObjectResult>(await ImageApi(UserOf(RoleNames.Staff, 5)).Image(999));
        Assert.Equal(404, missing.StatusCode);
    }

    [Fact]
    public async Task Image_PublicBestOfOtherStudent_IsWatermarkedCopy_NoDownload()
    {
        var graded = AddCompetition("Done", -20, -10, CompetitionStatus.GRADED);
        var s = AddSubmission(graded, 1, withFile: true, rating: Rating.BEST);

        var viewed = Assert.IsType<FileContentResult>(await ImageApi(UserOf(RoleNames.Student, 2)).Image(s.Id));
        Assert.Equal("image/jpeg", viewed.ContentType);
        Assert.NotEmpty(viewed.FileContents);

        var download = Assert.IsType<ObjectResult>(await ImageApi(UserOf(RoleNames.Student, 2)).Image(s.Id, download: true));
        Assert.Equal(404, download.StatusCode);
    }
}
