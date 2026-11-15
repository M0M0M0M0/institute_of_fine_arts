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
/// Mobile redesign API additions: image flags (hasCover, submissionId, hasThumbnail), ?thumb=true, the public gallery,
/// the student / staff dashboards and the longer /api/auth/me.
/// </summary>
public sealed class MobileRedesignApiTests : IDisposable
{
    private static readonly DateOnly Today = DateTimeHelper.Today;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _contentRoot = Path.Combine(Path.GetTempPath(), "ifa-api-tests-" + Guid.NewGuid().ToString("N"));
    private readonly FileStorageService _files;
    private readonly AppDbContext _db = new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    public MobileRedesignApiTests()
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

    private static ClaimsPrincipal UserOf(string role, long profileId, long accountId = 1) => new(new ClaimsIdentity(new[]
    {
        new Claim(ClaimTypes.Role, role), new Claim(ClaimsPrincipalExtensions.ProfileIdClaim, profileId.ToString()),
        new Claim(ClaimTypes.NameIdentifier, accountId.ToString()),
    }, "Test"));

    private static T WithUser<T>(T controller, ClaimsPrincipal user) where T : ControllerBase
    {
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = user } };
        return controller;
    }

    private static JsonElement ToJson(object? value) => JsonSerializer.SerializeToElement(value, value?.GetType() ?? typeof(object), Json);

    private static object? Payload<T>(ActionResult<T> r) => r.Value ?? (r.Result as ObjectResult)?.Value;

    private HomeAndCompetitionsApiController PublicApi()
        => WithUser(new HomeAndCompetitionsApiController(new PublicSiteService(_db), _db), new ClaimsPrincipal(new ClaimsIdentity()));

    private GalleryApiController GalleryApi()
        => WithUser(new GalleryApiController(new ArtworkCatalogService(_db), _db), new ClaimsPrincipal(new ClaimsIdentity()));

    private DashboardApiController DashboardApi(string role, long profileId)
        => WithUser(new DashboardApiController(_db), UserOf(role, profileId));

    private SubmissionImagesApiController ImageApi(ClaimsPrincipal user)
        => WithUser(new SubmissionImagesApiController(
            new ImageAccessService(_db, Options.Create(new UploadOptions { PublicWatermark = false })), _files,
            new WatermarkService(NullLogger<WatermarkService>.Instance)), user);

    private Competition AddCompetition(string title, int startIn, int endIn, CompetitionStatus stored = CompetitionStatus.ONGOING,
        bool cover = false, DateTime? publishedAt = null)
    {
        var c = new Competition
        {
            Code = "C" + Guid.NewGuid().ToString("N")[..8], Title = title, Description = "About", Rules = "Rules text",
            StartDate = Today.AddDays(startIn), EndDate = Today.AddDays(endIn), Status = stored, CreatedBy = 1,
            CoverImagePath = cover ? "competitions/cover.jpg" : null, PublishedAt = publishedAt,
        };
        _db.Competitions.Add(c);
        _db.SaveChanges();
        return c;
    }

    private StudentProfile AddStudent(string fullName, string? className = "Class A")
    {
        var account = new Account { Username = "u" + Guid.NewGuid().ToString("N")[..6], FullName = fullName, Email = fullName.Replace(' ', '.') + "@x.vn", Role = Role.STUDENT };
        var profile = new StudentProfile { Account = account, StudentCode = "S" + Guid.NewGuid().ToString("N")[..6], CourseName = "Painting", ClassName = className };
        _db.StudentProfiles.Add(profile);
        _db.SaveChanges();
        return profile;
    }

    private Submission AddSubmission(Competition c, long student, string title = "Sunset", bool thumb = false, Rating? rating = null)
    {
        var s = new Submission
        {
            CompetitionId = c.Id, StudentId = student, Title = title, OriginalFileName = "sunset.jpg", FileSize = 10,
            ImagePath = $"submissions/{c.Id}/{Guid.NewGuid()}.jpg", TextType = TextType.POEM, TextContent = "Poem",
            Description = "Desc", SubmittedAt = DateTimeHelper.Now.AddHours(-1),
            ThumbnailPath = thumb ? $"submissions/{c.Id}/{Guid.NewGuid()}_thumb.jpg" : null,
        };
        WriteImage(s.ImagePath, 200, 150);
        if (s.ThumbnailPath != null) WriteImage(s.ThumbnailPath, 40, 30);
        if (rating is { } r)
        {
            s.Evaluation = new Evaluation { Rating = r, PositivePoints = "Good colours", EvaluatedBy = 1, EvaluatedAt = DateTimeHelper.Now };
        }
        _db.Submissions.Add(s);
        _db.SaveChanges();
        return s;
    }

    private void WriteImage(string relativePath, int width, int height)
    {
        var full = _files.ResolvePath(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllBytes(full, TestImages.Create(width, height));
    }

    private AwardIssued Award(Competition c, Submission s, string name = "First prize", int rank = 1)
    {
        var award = new CompetitionAward { CompetitionId = c.Id, AwardName = name, Rank = rank, PrizeValue = 1000m, Quantity = 1 };
        _db.CompetitionAwards.Add(award);
        _db.SaveChanges();
        var issued = new AwardIssued { CompetitionAwardId = award.Id, SubmissionId = s.Id, IssuedBy = 1, IssuedDate = Today };
        _db.AwardsIssued.Add(issued);
        _db.SaveChanges();
        return issued;
    }

    // ------------------------------------------------------------------ image flags

    [Fact]
    public async Task CompetitionDtos_HaveHasCover()
    {
        var withCover = AddCompetition("Autumn", -1, 4, cover: true);
        var plain = AddCompetition("Winter", -1, 6);

        var list = ToJson((await PublicApi().List()).Value);
        var byId = list.EnumerateArray().ToDictionary(e => e.GetProperty("id").GetInt64());
        Assert.True(byId[withCover.Id].GetProperty("hasCover").GetBoolean());
        Assert.False(byId[plain.Id].GetProperty("hasCover").GetBoolean());

        Assert.True(ToJson((await PublicApi().Details(withCover.Id)).Value).GetProperty("hasCover").GetBoolean());
        Assert.False(ToJson((await PublicApi().Details(plain.Id)).Value).GetProperty("hasCover").GetBoolean());

        var home = ToJson((await PublicApi().Home()).Value);
        var ongoing = home.GetProperty("ongoing").EnumerateArray().ToDictionary(e => e.GetProperty("id").GetInt64());
        Assert.True(ongoing[withCover.Id].GetProperty("hasCover").GetBoolean());
        Assert.False(ongoing[plain.Id].GetProperty("hasCover").GetBoolean());
    }

    [Fact]
    public async Task WinnersAndAwardResults_HaveSubmissionIdAndThumbnailFlag()
    {
        var graded = AddCompetition("Summer", -40, -30, CompetitionStatus.GRADED, publishedAt: DateTimeHelper.Now.AddDays(-20));
        var student = AddStudent("Nguyen An");
        var entry = AddSubmission(graded, student.Id, "Calm sea", thumb: true, rating: Rating.BEST);
        Award(graded, entry);

        var winner = ToJson((await PublicApi().Home()).Value).GetProperty("latestWinners")[0];
        Assert.Equal(entry.Id, winner.GetProperty("submissionId").GetInt64());
        Assert.True(winner.GetProperty("hasThumbnail").GetBoolean());

        var result = ToJson(Payload(await PublicApi().Awards(graded.Id)))[0];
        Assert.Equal(entry.Id, result.GetProperty("submissionId").GetInt64());
        Assert.True(result.GetProperty("hasThumbnail").GetBoolean());
    }

    [Fact]
    public async Task StudentSubmissions_HaveSubmissionIdAndThumbnailFlag()
    {
        var c = AddCompetition("Autumn", -1, 4);
        var student = AddStudent("Nguyen An");
        var withThumb = AddSubmission(c, student.Id, "A", thumb: true);
        var api = WithUser(new StudentSubmissionsApiController(new SubmissionService(_db, _files, NullLogger<SubmissionService>.Instance), _db),
            UserOf(RoleNames.Student, student.Id));

        var list = ToJson(Payload(await api.Mine(null, null, null, null, null)))[0];
        Assert.Equal(withThumb.Id, list.GetProperty("submissionId").GetInt64());
        Assert.True(list.GetProperty("hasThumbnail").GetBoolean());

        var detail = ToJson(Payload(await api.Details(withThumb.Id)));
        Assert.Equal(withThumb.Id, detail.GetProperty("submissionId").GetInt64());
        Assert.True(detail.GetProperty("hasThumbnail").GetBoolean());
    }

    [Fact]
    public async Task GradingAndAwarding_ExposeFlags()
    {
        var closed = AddCompetition("Closed", -10, -2, cover: true);
        var student = AddStudent("Nguyen An");
        var entry = AddSubmission(closed, student.Id, "A", thumb: true, rating: Rating.BEST);

        var competitions = await Web_GradingCompetitions();
        Assert.True(competitions.GetProperty("hasCover").GetBoolean());

        var items = ToJson((await Web_GradingList(closed.Id)).Items)[0];
        Assert.Equal(entry.Id, items.GetProperty("submissionId").GetInt64());
        Assert.True(items.GetProperty("hasThumbnail").GetBoolean());

        var awarding = ToJson((await Web_AwardingCompetitions())[0]);
        Assert.True(awarding.GetProperty("hasCover").GetBoolean());

        var issued = ToJson((await Web_AwardingDetail(closed.Id)));
        Assert.True(issued.GetProperty("hasCover").GetBoolean());
    }

    private async Task<JsonElement> Web_GradingCompetitions()
        => ToJson((await GradingQueries.CompetitionsAsync(_db))[0]);

    private Task<GradingListDto> Web_GradingList(long id)
        => GradingQueries.ListAsync(_db, id, null, null, null);

    private Task<List<AwardingCompetitionDto>> Web_AwardingCompetitions()
        => AwardingQueries.CompetitionsAsync(_db);

    private async Task<AwardingDetailDto> Web_AwardingDetail(long id)
        => await AwardingQueries.DetailAsync(_db, id);

    [Fact]
    public async Task ExhibitionEntries_ExposeSubmissionIdAndThumbnailFlag()
    {
        var c = AddCompetition("Graded", -40, -30, CompetitionStatus.GRADED, publishedAt: DateTimeHelper.Now.AddDays(-5));
        var student = AddStudent("Nguyen An");
        var entry = AddSubmission(c, student.Id, "A", thumb: true, rating: Rating.BEST);
        var exhibition = new Exhibition { Name = "Spring", Venue = "Hall", City = "Hue", StartDate = Today.AddDays(5), EndDate = Today.AddDays(9), CreatedBy = 1 };
        _db.Exhibitions.Add(exhibition);
        _db.SaveChanges();

        var eligible = ToJson(await ExhibitionApiQueries.EligibleAsync(_db, student.Id, null))[0];
        Assert.Equal(entry.Id, eligible.GetProperty("submissionId").GetInt64());
        Assert.True(eligible.GetProperty("hasThumbnail").GetBoolean());

        _db.ExhibitionEntries.Add(new ExhibitionEntry { ExhibitionId = exhibition.Id, SubmissionId = entry.Id, AskingPrice = 100m, ProposedBy = 1 });
        _db.SaveChanges();

        var mine = ToJson(await ExhibitionApiQueries.StudentEntriesAsync(_db, student.Id))[0];
        Assert.Equal(entry.Id, mine.GetProperty("submissionId").GetInt64());
        Assert.True(mine.GetProperty("hasThumbnail").GetBoolean());

        var staff = ToJson(await ExhibitionApiQueries.StaffEntriesAsync(_db, null, null, null, null))[0];
        Assert.Equal(entry.Id, staff.GetProperty("submissionId").GetInt64());
        Assert.True(staff.GetProperty("hasThumbnail").GetBoolean());
    }

    // ------------------------------------------------------------------ thumb=true

    [Fact]
    public async Task Image_ThumbTrue_ReturnsThumbnailForOwner_AndOriginalByDefault()
    {
        var c = AddCompetition("Autumn", -1, 4);
        var student = AddStudent("Nguyen An");
        var entry = AddSubmission(c, student.Id, thumb: true);
        var api = ImageApi(UserOf(RoleNames.Student, student.Id));

        var original = Assert.IsType<FileStreamResult>(await api.Image(entry.Id));
        var thumb = Assert.IsType<FileStreamResult>(await api.Image(entry.Id, thumb: true));

        Assert.Equal(File.ReadAllBytes(_files.ResolvePath(entry.ImagePath)).Length, original.FileStream.Length);
        Assert.Equal(File.ReadAllBytes(_files.ResolvePath(entry.ThumbnailPath!)).Length, thumb.FileStream.Length);
        Assert.NotEqual(original.FileStream.Length, thumb.FileStream.Length);
        await original.FileStream.DisposeAsync();
        await thumb.FileStream.DisposeAsync();
    }

    [Fact]
    public async Task Image_ThumbTrue_KeepsPermissionRules()
    {
        var c = AddCompetition("Autumn", -1, 4);
        var owner = AddStudent("Nguyen An");
        var other = AddStudent("Tran Binh");
        var entry = AddSubmission(c, owner.Id, thumb: true);

        var result = await ImageApi(UserOf(RoleNames.Student, other.Id)).Image(entry.Id, thumb: true);

        Assert.Equal(StatusCodes.Status404NotFound, Assert.IsType<ObjectResult>(result).StatusCode);
    }

    // ------------------------------------------------------------------ gallery

    private Submission AddPublicPainting(string title, bool thumb = true)
    {
        var graded = AddCompetition("Graded " + title, -40, -30, CompetitionStatus.GRADED, publishedAt: DateTimeHelper.Now.AddDays(-3));
        var student = AddStudent("Nguyen An");
        var entry = AddSubmission(graded, student.Id, title, thumb, Rating.BEST);
        return entry;
    }

    [Fact]
    public async Task Gallery_ListsPublicPaintingsOnly_WithPaging()
    {
        var animal = AddPublicPainting("A horse in the field");
        AddPublicPainting("Quiet sea");
        var ongoing = AddCompetition("Open", -1, 4);
        AddSubmission(ongoing, AddStudent("Le Cuong").Id, "Private", rating: Rating.BEST);

        var all = ToJson((await GalleryApi().List(null)).Value);
        Assert.Equal(2, all.GetProperty("totalCount").GetInt32());
        Assert.Equal(1, all.GetProperty("page").GetInt32());
        Assert.Equal(12, all.GetProperty("pageSize").GetInt32());
        Assert.Equal(1, all.GetProperty("totalPages").GetInt32());
        var first = all.GetProperty("items").EnumerateArray().First(i => i.GetProperty("submissionId").GetInt64() == animal.Id);
        Assert.Equal("A horse in the field", first.GetProperty("title").GetString());
        Assert.Equal("Nguyen An", first.GetProperty("studentName").GetString());
        Assert.True(first.GetProperty("hasThumbnail").GetBoolean());

        var paged = ToJson((await GalleryApi().List(null, 2, 1)).Value);
        Assert.Equal(2, paged.GetProperty("totalPages").GetInt32());
        Assert.Equal(1, paged.GetProperty("items").GetArrayLength());

        var capped = ToJson((await GalleryApi().List(null, 1, 500)).Value);
        Assert.Equal(48, capped.GetProperty("pageSize").GetInt32());
    }

    [Fact]
    public async Task Gallery_Category_FiltersAndRejectsUnknown()
    {
        var horse = AddPublicPainting("A horse in the field");
        AddPublicPainting("Quiet sea");

        var animals = ToJson((await GalleryApi().List("subject/Animals")).Value);
        Assert.Equal(1, animals.GetProperty("totalCount").GetInt32());
        Assert.Equal(horse.Id, animals.GetProperty("items")[0].GetProperty("submissionId").GetInt64());

        await Assert.ThrowsAsync<NotFoundException>(() => GalleryApi().List("subject/Nope"));
        await Assert.ThrowsAsync<NotFoundException>(() => GalleryApi().List("Animals"));
    }

    [Fact]
    public void Gallery_Categories_HaveGroupKeyLabel()
    {
        var json = ToJson(GalleryApi().Categories().Value);
        var oil = json.EnumerateArray().First(c => c.GetProperty("key").GetString() == "OilPaintings");
        Assert.Equal("medium", oil.GetProperty("group").GetString());
        Assert.Equal("Oil Paintings", oil.GetProperty("label").GetString());
    }

    [Fact]
    public async Task Gallery_Artwork_PublicOkAndPrivateNotFound()
    {
        var entry = AddPublicPainting("Quiet sea");
        var ongoing = AddCompetition("Open", -1, 4);
        var hidden = AddSubmission(ongoing, AddStudent("Le Cuong").Id, "Private", rating: Rating.BEST);

        var json = ToJson((await GalleryApi().Artwork(entry.Id)).Value);
        Assert.Equal(entry.Id, json.GetProperty("submissionId").GetInt64());
        Assert.Equal("Quiet sea", json.GetProperty("title").GetString());
        Assert.True(json.GetProperty("hasThumbnail").GetBoolean());
        Assert.True(json.GetProperty("isBest").GetBoolean());

        await Assert.ThrowsAsync<NotFoundException>(() => GalleryApi().Artwork(hidden.Id));
        await Assert.ThrowsAsync<NotFoundException>(() => GalleryApi().Artwork(99999));
    }

    // ------------------------------------------------------------------ dashboards

    [Fact]
    public async Task StudentDashboard_ComputesTheFiveBlocks()
    {
        var student = AddStudent("Nguyen An");
        var other = AddStudent("Tran Binh");
        var entered = AddCompetition("Entered", -2, 5, cover: true);
        var open = AddCompetition("Open to enter", -1, 3, cover: true);
        AddCompetition("Upcoming", 3, 9, CompetitionStatus.UPCOMING);
        var closed = AddCompetition("Closed", -20, -10);
        var recent = AddCompetition("Recent results", -40, -30, CompetitionStatus.GRADED, publishedAt: DateTimeHelper.Now.AddDays(-5));
        var old = AddCompetition("Old results", -90, -80, CompetitionStatus.GRADED, publishedAt: DateTimeHelper.Now.AddDays(-60));

        var editable = AddSubmission(entered, student.Id, "Editable");
        AddSubmission(closed, student.Id, "In closed");
        var fresh = AddSubmission(recent, student.Id, "Fresh result", rating: Rating.BEST);
        AddSubmission(old, student.Id, "Old result", rating: Rating.BEST);
        AddSubmission(open, other.Id, "Someone else's");

        var exhibition = new Exhibition { Name = "Spring", Venue = "Hall", City = "Hue", StartDate = Today.AddDays(5), EndDate = Today.AddDays(9), CreatedBy = 1 };
        _db.Exhibitions.Add(exhibition);
        _db.SaveChanges();
        _db.ExhibitionEntries.AddRange(
            new ExhibitionEntry { ExhibitionId = exhibition.Id, SubmissionId = fresh.Id, AskingPrice = 1m, ProposedBy = 1, Status = EntryStatus.PENDING },
            new ExhibitionEntry { ExhibitionId = exhibition.Id, SubmissionId = editable.Id, AskingPrice = 1m, ProposedBy = 1, Status = EntryStatus.SOLD, SoldPrice = 1m, PaidToStudent = false },
            new ExhibitionEntry { ExhibitionId = exhibition.Id, SubmissionId = editable.Id, AskingPrice = 1m, ProposedBy = 1, Status = EntryStatus.SOLD, SoldPrice = 1m, PaidToStudent = true });
        _db.SaveChanges();

        var json = ToJson(Payload(await DashboardApi(RoleNames.Student, student.Id).Student()));

        var toEnter = json.GetProperty("openToEnter");
        Assert.Equal(1, toEnter.GetArrayLength());
        Assert.Equal(open.Id, toEnter[0].GetProperty("id").GetInt64());
        Assert.True(toEnter[0].GetProperty("hasCover").GetBoolean());
        Assert.Equal("ONGOING", toEnter[0].GetProperty("status").GetString());

        var edit = json.GetProperty("editable");
        Assert.Equal(1, edit.GetArrayLength());
        Assert.Equal(editable.Id, edit[0].GetProperty("id").GetInt64());
        Assert.True(edit[0].GetProperty("canEdit").GetBoolean());

        var results = json.GetProperty("newResults");
        Assert.Equal(1, results.GetArrayLength());
        Assert.Equal(fresh.Id, results[0].GetProperty("id").GetInt64());

        Assert.Equal(1, json.GetProperty("pendingProposals").GetInt32());
        Assert.Equal(1, json.GetProperty("unpaidSales").GetInt32());
    }

    [Fact]
    public async Task StaffDashboard_ComputesGradingPublishingAndCounts()
    {
        var student = AddStudent("Nguyen An");
        var partly = AddCompetition("Partly graded", -20, -10);
        AddSubmission(partly, student.Id, "A", rating: Rating.BEST);
        AddSubmission(partly, AddStudent("Tran Binh").Id, "B");
        var done = AddCompetition("Fully graded", -30, -15);
        var best = AddSubmission(done, student.Id, "C", rating: Rating.BEST);
        _db.CompetitionAwards.Add(new CompetitionAward { CompetitionId = done.Id, AwardName = "First", Rank = 1, PrizeValue = 1m, Quantity = 3 });
        _db.SaveChanges();
        AddCompetition("Still open", -2, 4);
        var published = AddCompetition("Published", -60, -50, CompetitionStatus.GRADED, publishedAt: DateTimeHelper.Now.AddDays(-30));
        AddSubmission(published, student.Id, "D", rating: Rating.BEST);

        var exhibition = new Exhibition { Name = "Spring", Venue = "Hall", City = "Hue", StartDate = Today.AddDays(5), EndDate = Today.AddDays(9), CreatedBy = 1 };
        _db.Exhibitions.Add(exhibition);
        _db.SaveChanges();
        _db.ExhibitionEntries.AddRange(
            new ExhibitionEntry { ExhibitionId = exhibition.Id, SubmissionId = best.Id, AskingPrice = 1m, ProposedBy = 1, Status = EntryStatus.PENDING },
            new ExhibitionEntry { ExhibitionId = exhibition.Id, SubmissionId = best.Id, AskingPrice = 1m, ProposedBy = 1, Status = EntryStatus.SOLD, SoldPrice = 1m },
            new ExhibitionEntry { ExhibitionId = exhibition.Id, SubmissionId = best.Id, AskingPrice = 1m, ProposedBy = 1, Status = EntryStatus.SOLD, SoldPrice = 1m, PaidToStudent = true });
        _db.SaveChanges();

        var json = ToJson(Payload(await DashboardApi(RoleNames.Staff, 1).Staff()));

        var toGrade = json.GetProperty("toGrade");
        Assert.Equal(1, toGrade.GetArrayLength());
        Assert.Equal(partly.Id, toGrade[0].GetProperty("competitionId").GetInt64());
        Assert.Equal("Partly graded", toGrade[0].GetProperty("title").GetString());
        Assert.Equal(2, toGrade[0].GetProperty("total").GetInt32());
        Assert.Equal(1, toGrade[0].GetProperty("evaluated").GetInt32());
        Assert.Equal(Today.AddDays(-10).ToString("yyyy-MM-dd"), toGrade[0].GetProperty("endDate").GetString());

        var toPublish = json.GetProperty("toPublish");
        Assert.Equal(1, toPublish.GetArrayLength());
        Assert.Equal(done.Id, toPublish[0].GetProperty("competitionId").GetInt64());
        Assert.Equal(3, toPublish[0].GetProperty("emptySlots").GetInt32());

        Assert.Equal(1, json.GetProperty("pendingProposals").GetInt32());
        Assert.Equal(1, json.GetProperty("unpaidSales").GetInt32());
    }

    // ------------------------------------------------------------------ auth/me

    [Fact]
    public async Task Me_ReturnsEmailCodeAndClassOrPosition()
    {
        var student = AddStudent("Nguyen An", "Class B");
        var staffAccount = new Account { Username = "staff1", FullName = "Teacher", Email = "t@x.vn", Role = Role.STAFF };
        var staff = new StaffProfile { Account = staffAccount, StaffCode = "NV01", Position = "Lecturer" };
        _db.StaffProfiles.Add(staff);
        _db.SaveChanges();

        var studentJson = ToJson(Payload(await WithUser(new AuthApiController(null!, null!, _db), UserOf(RoleNames.Student, student.Id, student.AccountId)).Me()));
        Assert.Equal("Nguyen.An@x.vn", studentJson.GetProperty("email").GetString());
        Assert.Equal(student.StudentCode, studentJson.GetProperty("code").GetString());
        Assert.Equal("Class B", studentJson.GetProperty("className").GetString());
        Assert.Equal(JsonValueKind.Null, studentJson.GetProperty("position").ValueKind);
        Assert.Equal("Nguyen An", studentJson.GetProperty("fullName").GetString());

        var staffJson = ToJson(Payload(await WithUser(new AuthApiController(null!, null!, _db), UserOf(RoleNames.Staff, staff.Id, staff.AccountId)).Me()));
        Assert.Equal("t@x.vn", staffJson.GetProperty("email").GetString());
        Assert.Equal("NV01", staffJson.GetProperty("code").GetString());
        Assert.Equal("Lecturer", staffJson.GetProperty("position").GetString());
        Assert.Equal(JsonValueKind.Null, staffJson.GetProperty("className").ValueKind);
    }
}
