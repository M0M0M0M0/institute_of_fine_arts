using InstituteOfFineArts.Web.Data;
using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Helpers;
using InstituteOfFineArts.Web.Infrastructure.FileStorage;
using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.Services.Implementations;
using InstituteOfFineArts.Web.ViewModels.Student.Submissions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using SkiaSharp;

namespace InstituteOfFineArts.Tests.Services;

/// <summary>
/// SubmissionService (tasks BE1-07..BE1-09, M06): submit (BR-13..BR-19), my entries, details with the evaluation
/// hidden until GRADED (BR-24), edit / replace image / delete (BR-18), owner only (BR-20, TC-M06-14).
/// Uses the real FileStorageService + ThumbnailService on a temporary folder, so stored and deleted files are checked on disk.
/// </summary>
public sealed class SubmissionServiceTests : IDisposable
{
    /// <summary>Today in the institute time zone.</summary>
    private static readonly DateOnly Today = DateTimeHelper.Today;

    /// <summary>student_profile.id of the signed-in student.</summary>
    private const long Me = 1;

    /// <summary>student_profile.id of another student.</summary>
    private const long Other = 2;

    /// <summary>Temporary content root, deleted after each test.</summary>
    private readonly string _contentRoot = Path.Combine(Path.GetTempPath(), "ifa-tests-" + Guid.NewGuid().ToString("N"));

    /// <summary>Real file storage below {contentRoot}/uploads.</summary>
    private readonly FileStorageService _files;

    /// <summary>Creates the real file storage with a fake hosting environment.</summary>
    public SubmissionServiceTests()
    {
        var env = new Mock<IWebHostEnvironment>();
        env.SetupGet(e => e.ContentRootPath).Returns(_contentRoot);
        var options = Options.Create(new UploadOptions { RootPath = "uploads", MaxFileSizeBytes = 5 * 1024 * 1024, ThumbnailWidth = 400 });
        _files = new FileStorageService(options, env.Object, new ThumbnailService(), NullLogger<FileStorageService>.Instance);
    }

    /// <summary>Removes the temporary folder.</summary>
    public void Dispose()
    {
        if (Directory.Exists(_contentRoot))
        {
            Directory.Delete(_contentRoot, recursive: true);
        }
    }

    /// <summary>DbContext whose SaveChanges can be made to fail, to simulate a database error after the file was written.</summary>
    private sealed class FailingDbContext : AppDbContext
    {
        /// <summary>Creates the context on the given in-memory database.</summary>
        public FailingDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        /// <summary>When true the next SaveChangesAsync throws DbUpdateException.</summary>
        public bool Fail { get; set; }

        /// <summary>Throws while <see cref="Fail"/> is set, otherwise saves normally.</summary>
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => Fail ? throw new DbUpdateException("Simulated database error") : base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Empty in-memory database.</summary>
    private static FailingDbContext CreateDb()
        => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    /// <summary>Service under test.</summary>
    private SubmissionService Service(AppDbContext db) => new(db, _files, NullLogger<SubmissionService>.Instance);

    /// <summary>Adds a competition with dates relative to today and the given stored status.</summary>
    private static Competition AddCompetition(AppDbContext db, string title, int startIn, int endIn,
        CompetitionStatus stored = CompetitionStatus.ONGOING)
    {
        var c = new Competition
        {
            Code = "X" + Guid.NewGuid().ToString("N")[..8], Title = title,
            StartDate = Today.AddDays(startIn), EndDate = Today.AddDays(endIn), Status = stored, CreatedBy = 1,
        };
        db.Competitions.Add(c);
        db.SaveChanges();
        return c;
    }

    /// <summary>Adds an entry directly (with real files on disk when <paramref name="withFiles"/> is set).</summary>
    private Submission AddSubmission(AppDbContext db, Competition c, long student, string title = "Night Market",
        int daysAgo = 0, bool withFiles = false, Rating? rating = null)
    {
        var s = new Submission
        {
            CompetitionId = c.Id, StudentId = student, Title = title, OriginalFileName = "old.jpg", FileSize = 10,
            ImagePath = $"submissions/{c.Id}/{Guid.NewGuid()}.jpg", TextType = TextType.POEM, TextContent = "Old poem",
            Description = "Old description", SubmittedAt = Today.AddDays(-daysAgo).ToDateTime(new TimeOnly(10, 0)),
        };
        s.ThumbnailPath = s.ImagePath.Replace("submissions/", "thumbnails/");
        if (withFiles)
        {
            foreach (var path in new[] { s.ImagePath, s.ThumbnailPath })
            {
                var full = _files.ResolvePath(path);
                Directory.CreateDirectory(Path.GetDirectoryName(full)!);
                File.WriteAllBytes(full, TestImages.Create(100, 100));
            }
        }
        if (rating is { } r)
        {
            s.Evaluation = new Evaluation { Rating = r, PositivePoints = "Strong colors", EvaluatedBy = 1, EvaluatedAt = DateTimeHelper.Now };
        }
        db.Submissions.Add(s);
        db.SaveChanges();
        return s;
    }

    /// <summary>Valid submit form for a competition with a JPEG of the given size.</summary>
    private static SubmissionFormVm Form(long competitionId, IFormFile? image = null) => new()
    {
        CompetitionId = competitionId, Title = "  Falling Leaves  ", Description = "Autumn in the park", TextType = TextType.POEM,
        TextContent = "Red letters falling from the trees,\nthe autumn writes in every breeze.",
        Image = image ?? TestImages.AsFormFile(TestImages.Create(800, 600), "lá vàng #1.JPEG"),
    };

    /// <summary>All files currently below the upload root.</summary>
    private string[] StoredFiles()
    {
        var root = Path.Combine(_contentRoot, "uploads");
        return Directory.Exists(root) ? Directory.GetFiles(root, "*", SearchOption.AllDirectories) : Array.Empty<string>();
    }

    // ------------------------------------------------------------------ Submit (BE1-07)

    /// <summary>TC-M06-01/02/07: valid entry → row with trimmed texts, Guid file name, thumbnail, original name kept, submitted now.</summary>
    [Fact]
    public async Task Create_Valid_SavesRowAndFiles()
    {
        using var db = CreateDb();
        var c = AddCompetition(db, "Colors of Autumn", -10, 10);

        var result = await Service(db).CreateAsync(Form(c.Id), Me);

        var s = await db.Submissions.SingleAsync();
        Assert.Equal(result.Id, s.Id);
        Assert.Equal("Falling Leaves", s.Title);
        Assert.Equal(Me, s.StudentId);
        Assert.Equal(TextType.POEM, s.TextType);
        Assert.Equal("lá vàng #1.JPEG", s.OriginalFileName);
        Assert.Matches($@"^submissions/{c.Id}/[0-9a-f\-]{{36}}\.jpg$", s.ImagePath);
        Assert.True(File.Exists(_files.ResolvePath(s.ImagePath)));
        Assert.True(File.Exists(_files.ResolvePath(s.ThumbnailPath!)));
        Assert.Equal(DateOnly.FromDateTime(DateTimeHelper.Now), DateOnly.FromDateTime(s.SubmittedAt));
        Assert.Null(result.Warning);
    }

    /// <summary>BR-17: a small image is accepted with a warning.</summary>
    [Fact]
    public async Task Create_SmallImage_Warning()
    {
        using var db = CreateDb();
        var c = AddCompetition(db, "Colors of Autumn", -10, 10);

        var result = await Service(db).CreateAsync(Form(c.Id, TestImages.AsFormFile(TestImages.Create(300, 200))), Me);

        Assert.NotNull(result.Warning);
    }

    /// <summary>TC-M06-09: the last day (end date = today) is still accepted until 23:59:59.</summary>
    [Fact]
    public async Task Create_DeadlineToday_Allowed()
    {
        using var db = CreateDb();
        var c = AddCompetition(db, "Deadline Today", -7, 0);

        await Service(db).CreateAsync(Form(c.Id), Me);

        Assert.Equal(1, await db.Submissions.CountAsync());
    }

    /// <summary>TC-M06-10, TC-M06-11 (BR-13, BR-14): closed, graded and upcoming competitions refuse entries – even with a stale stored status.</summary>
    [Theory]
    [InlineData(-25, -1, CompetitionStatus.ONGOING, SubmissionService.DeadlinePassedMessage)]
    [InlineData(-5, 5, CompetitionStatus.GRADED, SubmissionService.DeadlinePassedMessage)]
    [InlineData(5, 30, CompetitionStatus.ONGOING, SubmissionService.NotStartedMessage)]
    public async Task Create_NotOngoing_Refused(int startIn, int endIn, CompetitionStatus stored, string message)
    {
        using var db = CreateDb();
        var c = AddCompetition(db, "Any", startIn, endIn, stored);

        var form = await Assert.ThrowsAsync<BusinessException>(() => Service(db).GetNewFormAsync(c.Id, Me));
        var post = await Assert.ThrowsAsync<BusinessException>(() => Service(db).CreateAsync(Form(c.Id), Me));

        Assert.Equal(message, form.Message);
        Assert.Equal(message, post.Message);
        Assert.Empty(db.Submissions);
        Assert.Empty(StoredFiles());
    }

    /// <summary>BR-15: a second entry in the same competition is refused; another student can still submit.</summary>
    [Fact]
    public async Task Create_SecondEntry_Refused()
    {
        using var db = CreateDb();
        var c = AddCompetition(db, "Colors of Autumn", -10, 10);
        AddSubmission(db, c, Me);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => Service(db).CreateAsync(Form(c.Id), Me));
        await Assert.ThrowsAsync<BusinessException>(() => Service(db).GetNewFormAsync(c.Id, Me));
        await Service(db).CreateAsync(Form(c.Id), Other);

        Assert.Equal(SubmissionService.AlreadySubmittedMessage, ex.Message);
        Assert.Equal(2, await db.Submissions.CountAsync());
    }

    /// <summary>TC-M06-03/04/06: PNG, fake JPEG and empty file → BusinessException on Image; no row, no file.</summary>
    [Theory]
    [InlineData("png")]
    [InlineData("fake")]
    [InlineData("empty")]
    public async Task Create_InvalidImage_NothingSaved(string kind)
    {
        using var db = CreateDb();
        var c = AddCompetition(db, "Colors of Autumn", -10, 10);
        var file = kind switch
        {
            "png" => TestImages.AsFormFile(TestImages.Create(800, 600, SKEncodedImageFormat.Png), "image.png", "image/png"),
            "fake" => TestImages.AsFormFile("this is not an image"u8.ToArray(), "fake_jpg.jpg"),
            _ => TestImages.AsFormFile(Array.Empty<byte>(), "empty.jpg"),
        };

        var ex = await Assert.ThrowsAsync<BusinessException>(() => Service(db).CreateAsync(Form(c.Id, file), Me));

        Assert.Equal(nameof(SubmissionFormVm.Image), ex.Field);
        Assert.Empty(db.Submissions);
        Assert.Empty(StoredFiles());
    }

    /// <summary>BR-19 re-checked on the server: a blank title is refused even when the form validation was skipped.</summary>
    [Fact]
    public async Task Create_BlankTitle_Refused()
    {
        using var db = CreateDb();
        var c = AddCompetition(db, "Colors of Autumn", -10, 10);
        var form = Form(c.Id);
        form.Title = "   ";

        var ex = await Assert.ThrowsAsync<BusinessException>(() => Service(db).CreateAsync(form, Me));

        Assert.Equal(nameof(SubmissionFormVm.Title), ex.Field);
        Assert.Empty(StoredFiles());
    }

    /// <summary>UC-03 7a: the database fails after the file was written → the stored image and thumbnail are deleted.</summary>
    [Fact]
    public async Task Create_DbFailure_DeletesFiles()
    {
        using var db = CreateDb();
        var c = AddCompetition(db, "Colors of Autumn", -10, 10);
        db.Fail = true;

        await Assert.ThrowsAsync<DbUpdateException>(() => Service(db).CreateAsync(Form(c.Id), Me));

        Assert.Empty(StoredFiles());
    }

    /// <summary>Unknown competition → NotFoundException.</summary>
    [Fact]
    public async Task Create_UnknownCompetition_NotFound()
    {
        using var db = CreateDb();
        await Assert.ThrowsAsync<NotFoundException>(() => Service(db).GetNewFormAsync(42, Me));
        await Assert.ThrowsAsync<NotFoundException>(() => Service(db).CreateAsync(Form(42), Me));
    }

    // ------------------------------------------------------------------ My entries + details (BE1-08)

    /// <summary>F-M06-05: only own rows, newest first, status calculated, can-edit only while ongoing.</summary>
    [Fact]
    public async Task SearchMine_OnlyOwnRows()
    {
        using var db = CreateDb();
        var closed = AddCompetition(db, "City at Night", -25, -1);   // stale stored status
        var autumn = AddCompetition(db, "Colors of Autumn", -10, 10);
        AddSubmission(db, closed, Me, "Night Market", daysAgo: 20);
        AddSubmission(db, autumn, Me, "Falling Leaves", daysAgo: 2);
        AddSubmission(db, autumn, Other, "Harvest Moon", daysAgo: 1);

        var page = await Service(db).SearchMineAsync(Me, new MySubmissionSearchVm());

        Assert.Equal(new[] { "Falling Leaves", "Night Market" }, page.Items.Select(i => i.Title));
        Assert.Equal(CompetitionStatus.ONGOING, page.Items[0].CompetitionStatus);
        Assert.True(page.Items[0].CanEdit);
        Assert.Equal(CompetitionStatus.CLOSED, page.Items[1].CompetitionStatus);
        Assert.False(page.Items[1].CanEdit);
        Assert.Equal(new[] { "Colors of Autumn", "City at Night" },
            (await Service(db).GetMyCompetitionsAsync(Me)).Select(o => o.Title));
    }

    /// <summary>Filters: competition, title, submitted date range (To includes the whole day), rating only of GRADED results.</summary>
    [Fact]
    public async Task SearchMine_Filters()
    {
        using var db = CreateDb();
        var graded = AddCompetition(db, "Hometown", -60, -30, CompetitionStatus.GRADED);
        var closed = AddCompetition(db, "City at Night", -25, -1, CompetitionStatus.CLOSED);
        var autumn = AddCompetition(db, "Colors of Autumn", -10, 10);
        AddSubmission(db, graded, Me, "River", daysAgo: 50, rating: Rating.BEST);
        AddSubmission(db, closed, Me, "Night Market", daysAgo: 20, rating: Rating.BEST);   // not published yet
        AddSubmission(db, autumn, Me, "Falling Leaves", daysAgo: 2);
        var service = Service(db);

        async Task<string[]> Titles(MySubmissionSearchVm s) => (await service.SearchMineAsync(Me, s)).Items.Select(i => i.Title).ToArray();

        Assert.Equal(new[] { "Falling Leaves" }, await Titles(new MySubmissionSearchVm { CompetitionId = autumn.Id }));
        Assert.Equal(new[] { "Night Market" }, await Titles(new MySubmissionSearchVm { Title = "Market" }));
        Assert.Equal(new[] { "Night Market" }, await Titles(new MySubmissionSearchVm { From = Today.AddDays(-30), To = Today.AddDays(-20) }));
        Assert.Equal(new[] { "River" }, await Titles(new MySubmissionSearchVm { Rating = Rating.BEST }));

        // Rating column only for the GRADED competition (BR-24)
        var all = (await service.SearchMineAsync(Me, new MySubmissionSearchVm())).Items;
        Assert.Equal(Rating.BEST, all.Single(i => i.Title == "River").Rating);
        Assert.Null(all.Single(i => i.Title == "Night Market").Rating);
    }

    /// <summary>Paging: 10 per page.</summary>
    [Fact]
    public async Task SearchMine_Paging()
    {
        using var db = CreateDb();
        for (var i = 0; i < 12; i++)
        {
            AddSubmission(db, AddCompetition(db, $"C{i}", -10, 10), Me, $"Entry {i}", daysAgo: i);
        }

        var page2 = await Service(db).SearchMineAsync(Me, new MySubmissionSearchVm { Page = 2 });

        Assert.Equal(12, page2.TotalCount);
        Assert.Equal(new[] { "Entry 10", "Entry 11" }, page2.Items.Select(i => i.Title));
    }

    /// <summary>Details of an own entry in an ongoing competition: texts, deadline ISO, can edit, results not published.</summary>
    [Fact]
    public async Task Details_Own_Ongoing()
    {
        using var db = CreateDb();
        var c = AddCompetition(db, "Colors of Autumn", -10, 10);
        var s = AddSubmission(db, c, Me, rating: Rating.BEST);

        var vm = await Service(db).GetDetailsAsync(s.Id, Me);

        Assert.Equal("Night Market", vm.Title);
        Assert.Equal("Colors of Autumn", vm.CompetitionTitle);
        Assert.True(vm.CanEdit);
        Assert.Equal(Today.AddDays(10).ToString("yyyy-MM-dd") + "T23:59:59", vm.DeadlineIso);
        Assert.Null(vm.Evaluation);
    }

    /// <summary>BR-24: the evaluation is hidden while CLOSED (graded but not published) and shown once GRADED.</summary>
    [Theory]
    [InlineData(CompetitionStatus.CLOSED, false)]
    [InlineData(CompetitionStatus.GRADED, true)]
    public async Task Details_EvaluationOnlyWhenGraded(CompetitionStatus stored, bool visible)
    {
        using var db = CreateDb();
        var c = AddCompetition(db, "City at Night", -25, -1, stored);
        var s = AddSubmission(db, c, Me, rating: Rating.BETTER);

        var vm = await Service(db).GetDetailsAsync(s.Id, Me);

        Assert.False(vm.CanEdit);
        Assert.Equal(visible, vm.Evaluation != null);
        if (visible)
        {
            Assert.Equal(Rating.BETTER, vm.Evaluation!.Rating);
            Assert.Equal("Strong colors", vm.Evaluation.PositivePoints);
        }
    }

    /// <summary>TC-M06-14 (IDOR): another student's entry and an unknown id both throw NotFoundException everywhere.</summary>
    [Fact]
    public async Task OtherStudentsEntry_NotFound()
    {
        using var db = CreateDb();
        var c = AddCompetition(db, "Colors of Autumn", -10, 10);
        var theirs = AddSubmission(db, c, Other, withFiles: true);
        var service = Service(db);
        var edit = new SubmissionEditVm { Title = "Hacked", Description = "d", TextType = TextType.OTHER, TextContent = "t" };

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetDetailsAsync(theirs.Id, Me));
        await Assert.ThrowsAsync<NotFoundException>(() => service.GetForEditAsync(theirs.Id, Me));
        await Assert.ThrowsAsync<NotFoundException>(() => service.UpdateAsync(theirs.Id, edit, Me));
        await Assert.ThrowsAsync<NotFoundException>(() => service.DeleteAsync(theirs.Id, Me));
        await Assert.ThrowsAsync<NotFoundException>(() => service.GetDetailsAsync(999, Me));

        var unchanged = await db.Submissions.AsNoTracking().SingleAsync();
        Assert.Equal("Night Market", unchanged.Title);
        Assert.True(File.Exists(_files.ResolvePath(unchanged.ImagePath)));
    }

    // ------------------------------------------------------------------ Edit / replace / delete (BE1-09)

    /// <summary>F-M06-02: texts changed, image kept when no new file is chosen.</summary>
    [Fact]
    public async Task Update_TextsOnly_KeepsImage()
    {
        using var db = CreateDb();
        var c = AddCompetition(db, "Colors of Autumn", -10, 10);
        var s = AddSubmission(db, c, Me, withFiles: true);
        var oldImage = s.ImagePath;
        var service = Service(db);
        var form = await service.GetForEditAsync(s.Id, Me);
        form.Title = "Night Market (v2)";
        form.TextType = TextType.STORY;
        form.TextContent = "New story";

        await service.UpdateAsync(s.Id, form, Me);

        var saved = await db.Submissions.AsNoTracking().SingleAsync();
        Assert.Equal("Night Market (v2)", saved.Title);
        Assert.Equal(TextType.STORY, saved.TextType);
        Assert.Equal("New story", saved.TextContent);
        Assert.Equal(oldImage, saved.ImagePath);
        Assert.True(File.Exists(_files.ResolvePath(oldImage)));
    }

    /// <summary>TC-M06-12 (F-M06-03, BR-18): a new image replaces the old one; the old image and thumbnail are deleted.</summary>
    [Fact]
    public async Task Update_NewImage_OldFilesDeleted()
    {
        using var db = CreateDb();
        var c = AddCompetition(db, "Colors of Autumn", -10, 10);
        var s = AddSubmission(db, c, Me, withFiles: true);
        var (oldImage, oldThumb) = (s.ImagePath, s.ThumbnailPath!);
        var service = Service(db);
        var form = await service.GetForEditAsync(s.Id, Me);
        form.NewImage = TestImages.AsFormFile(TestImages.Create(900, 700), "new.jpg");

        await service.UpdateAsync(s.Id, form, Me);

        var saved = await db.Submissions.AsNoTracking().SingleAsync();
        Assert.NotEqual(oldImage, saved.ImagePath);
        Assert.Equal("new.jpg", saved.OriginalFileName);
        Assert.True(File.Exists(_files.ResolvePath(saved.ImagePath)));
        Assert.True(File.Exists(_files.ResolvePath(saved.ThumbnailPath!)));
        Assert.False(File.Exists(_files.ResolvePath(oldImage)));
        Assert.False(File.Exists(_files.ResolvePath(oldThumb)));
    }

    /// <summary>An invalid replacement image is refused on NewImage; the row and the old files stay as they were.</summary>
    [Fact]
    public async Task Update_InvalidNewImage_NothingChanged()
    {
        using var db = CreateDb();
        var c = AddCompetition(db, "Colors of Autumn", -10, 10);
        var s = AddSubmission(db, c, Me, withFiles: true);
        var service = Service(db);
        var form = await service.GetForEditAsync(s.Id, Me);
        form.NewImage = TestImages.AsFormFile(TestImages.Create(800, 600, SKEncodedImageFormat.Png), "image.png", "image/png");

        var ex = await Assert.ThrowsAsync<BusinessException>(() => service.UpdateAsync(s.Id, form, Me));

        Assert.Equal(nameof(SubmissionEditVm.NewImage), ex.Field);
        Assert.Equal(2, StoredFiles().Length);
        Assert.True(File.Exists(_files.ResolvePath(s.ImagePath)));
    }

    /// <summary>The database fails while replacing the image → the new files are deleted, the old ones kept.</summary>
    [Fact]
    public async Task Update_DbFailure_DeletesNewFiles()
    {
        using var db = CreateDb();
        var c = AddCompetition(db, "Colors of Autumn", -10, 10);
        var s = AddSubmission(db, c, Me, withFiles: true);
        var (oldImage, oldThumb) = (s.ImagePath, s.ThumbnailPath!);
        var service = Service(db);
        var form = await service.GetForEditAsync(s.Id, Me);
        form.NewImage = TestImages.AsFormFile(TestImages.Create(900, 700), "new.jpg");
        db.Fail = true;

        await Assert.ThrowsAsync<DbUpdateException>(() => service.UpdateAsync(s.Id, form, Me));

        Assert.Equal(2, StoredFiles().Length);
        Assert.True(File.Exists(_files.ResolvePath(oldImage)));
        Assert.True(File.Exists(_files.ResolvePath(oldThumb)));
    }

    /// <summary>F-M06-04 (BR-18): deleting an own entry removes the row, the image and the thumbnail.</summary>
    [Fact]
    public async Task Delete_Own_RowAndFilesRemoved()
    {
        using var db = CreateDb();
        var c = AddCompetition(db, "Deadline Today", -7, 0);
        var s = AddSubmission(db, c, Me, withFiles: true);

        await Service(db).DeleteAsync(s.Id, Me);

        Assert.Empty(db.Submissions);
        Assert.Empty(StoredFiles());
    }

    /// <summary>TC-M06-13 (BR-13, BR-14): after the deadline (or once GRADED) edit and delete are refused; nothing changes.</summary>
    [Theory]
    [InlineData(CompetitionStatus.ONGOING)]   // stale stored status: really CLOSED
    [InlineData(CompetitionStatus.GRADED)]
    public async Task EditDelete_AfterDeadline_Refused(CompetitionStatus stored)
    {
        using var db = CreateDb();
        var c = AddCompetition(db, "City at Night", -25, -1, stored);
        var s = AddSubmission(db, c, Me, withFiles: true);
        var service = Service(db);
        var edit = new SubmissionEditVm
        {
            Title = "Changed", Description = "d", TextType = TextType.OTHER, TextContent = "t",
            NewImage = TestImages.AsFormFile(TestImages.Create(800, 600)),
        };

        var get = await Assert.ThrowsAsync<BusinessException>(() => service.GetForEditAsync(s.Id, Me));
        var update = await Assert.ThrowsAsync<BusinessException>(() => service.UpdateAsync(s.Id, edit, Me));
        var delete = await Assert.ThrowsAsync<BusinessException>(() => service.DeleteAsync(s.Id, Me));

        Assert.All(new[] { get, update, delete }, ex => Assert.Equal(SubmissionService.DeadlinePassedMessage, ex.Message));
        var unchanged = await db.Submissions.AsNoTracking().SingleAsync();
        Assert.Equal("Night Market", unchanged.Title);
        Assert.Equal(2, StoredFiles().Length);
    }
}
