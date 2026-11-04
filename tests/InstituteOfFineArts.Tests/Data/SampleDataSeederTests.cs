using InstituteOfFineArts.Web.Data;
using InstituteOfFineArts.Web.Data.Seed;
using InstituteOfFineArts.Web.Infrastructure.FileStorage;
using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.Services.Implementations;
using InstituteOfFineArts.Web.Services.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SkiaSharp;

namespace InstituteOfFineArts.Tests.Data;

/// <summary>
/// Runs the whole sample data seeder (accounts + BE2-01) on an in-memory database and a temporary upload folder,
/// then checks the numbers of docs/plan/04 section 4.2–4.3 and the business rules the data must respect.
/// </summary>
public sealed class SampleDataSeederTests : IAsyncLifetime
{
    /// <summary>Temporary content root (uploads below it), deleted afterwards.</summary>
    private readonly string _contentRoot = Path.Combine(Path.GetTempPath(), "ifa-seed-" + Guid.NewGuid().ToString("N"));

    /// <summary>Service provider shared by the seeder and the checks.</summary>
    private ServiceProvider _services = null!;

    /// <summary>Database filled by the seeder.</summary>
    private AppDbContext Db => _services.GetRequiredService<AppDbContext>();

    /// <summary>Seeds once for all tests of this class instance.</summary>
    public async Task InitializeAsync()
    {
        var env = new Mock<IWebHostEnvironment>();
        env.SetupGet(e => e.ContentRootPath).Returns(_contentRoot);
        var dbName = Guid.NewGuid().ToString();
        _services = new ServiceCollection()
            .AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(dbName), ServiceLifetime.Singleton)
            .AddSingleton(env.Object)
            .AddSingleton(Microsoft.Extensions.Options.Options.Create(new UploadOptions()))
            .AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance)
            .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>))
            .AddSingleton<IThumbnailService, ThumbnailService>()
            .AddSingleton<IFileStorageService, FileStorageService>()
            .BuildServiceProvider();

        await DbSeeder.SeedAccountsAsync(Db);
        await DbSeeder.SeedSampleDataAsync(Db, _services);
    }

    /// <summary>Removes the temporary folder.</summary>
    public async Task DisposeAsync()
    {
        await _services.DisposeAsync();
        if (Directory.Exists(_contentRoot))
        {
            Directory.Delete(_contentRoot, recursive: true);
        }
    }

    /// <summary>7 competitions with the planned statuses; codes CTyyyy-nn are unique.</summary>
    [Fact]
    public async Task Competitions_StatusesAndCodes()
    {
        var all = await Db.Competitions.ToListAsync();
        Assert.Equal(7, all.Count);
        string Status(string title) => all.Single(c => c.Title == title).Status.ToString();
        Assert.Equal("GRADED", Status("Hometown Landscapes"));
        Assert.Equal("GRADED", Status("Wildlife"));
        Assert.Equal("CLOSED", Status("City at Night"));
        Assert.Equal("ONGOING", Status("Colors of Autumn"));
        Assert.Equal("ONGOING", Status("Deadline Today"));
        Assert.Equal("UPCOMING", Status("Lunar New Year 2027"));
        Assert.Equal("UPCOMING", Status("Family Portrait"));
        Assert.All(all, c => Assert.Matches(@"^CT\d{4}-\d{2}$", c.Code));
        Assert.Equal(7, all.Select(c => c.Code).Distinct().Count());
        Assert.All(all, c => Assert.True(Db.CompetitionAwards.Any(a => a.CompetitionId == c.Id)));   // BR-09
        Assert.Equal(6, all.Count(c => c.CoverImagePath != null));
    }

    /// <summary>27 entries (8, 6, 5, 6, 2); one per student per competition; student07 and student10 have none.</summary>
    [Fact]
    public async Task Submissions_CountsAndOwners()
    {
        var subs = await Db.Submissions.Include(s => s.Competition).Include(s => s.Student).ThenInclude(s => s.Account).ToListAsync();
        Assert.Equal(27, subs.Count);
        int Count(string title) => subs.Count(s => s.Competition.Title == title);
        Assert.Equal(new[] { 8, 6, 5, 6, 2, 0, 0 },
            new[] { "Hometown Landscapes", "Wildlife", "City at Night", "Colors of Autumn", "Deadline Today", "Lunar New Year 2027", "Family Portrait" }.Select(Count));
        Assert.Equal(subs.Count, subs.Select(s => (s.CompetitionId, s.StudentId)).Distinct().Count());   // BR-15
        Assert.DoesNotContain(subs, s => s.Student.Account.Username is "student07" or "student10");
        Assert.DoesNotContain(subs, s => s.Competition.Title == "Colors of Autumn" && s.Student.Account.Username == "student01"); // TC-M06-01
        Assert.Contains(subs, s => s.TextContent.Contains("Hà Nội"));                                    // Vietnamese text stored
    }

    /// <summary>Every image and thumbnail exists, is a JPEG, and the thumbnail is 400 px wide.</summary>
    [Fact]
    public async Task Submissions_ImagesOnDisk()
    {
        var storage = _services.GetRequiredService<IFileStorageService>();
        foreach (var s in await Db.Submissions.ToListAsync())
        {
            var original = storage.ResolvePath(s.ImagePath);
            Assert.True(File.Exists(original), s.ImagePath);
            Assert.Equal(new FileInfo(original).Length, s.FileSize);
            using var thumb = SKCodec.Create(storage.ResolvePath(s.ThumbnailPath!));
            Assert.Equal(SKEncodedImageFormat.Jpeg, thumb.EncodedFormat);
            Assert.Equal(400, thumb.Info.Width);
        }
    }

    /// <summary>14 evaluations in the two GRADED competitions covering all 6 ratings; the late entry is DISQUALIFIED.</summary>
    [Fact]
    public async Task Evaluations_AllRatings()
    {
        var evaluations = await Db.Evaluations.Include(e => e.Submission).ThenInclude(s => s.Competition).ToListAsync();
        Assert.Equal(14, evaluations.Count);
        Assert.All(evaluations, e => Assert.Equal(CompetitionStatus.GRADED, e.Submission.Competition.Status));
        Assert.Equal(Enum.GetValues<Rating>().Length, evaluations.Select(e => e.Rating).Distinct().Count());
        var late = evaluations.Single(e => e.Submission.SubmittedAt > e.Submission.Competition.EndDate.ToDateTime(new TimeOnly(23, 59, 59)));
        Assert.Equal(Rating.DISQUALIFIED, late.Rating);                                                    // TC-M07-04
        Assert.NotNull(late.NegativePoints);
        Assert.Equal(3, evaluations.Count(e => e.Rating == Rating.BEST && e.Submission.Competition.Title == "Hometown Landscapes"));
    }

    /// <summary>4 awards given, only to BEST entries, within quantity; Hometown still has an open BEST + Third Prize (TC-M08-01).</summary>
    [Fact]
    public async Task Awards_OnlyBest()
    {
        var issued = await Db.AwardsIssued.Include(a => a.Submission).ThenInclude(s => s.Evaluation)
            .Include(a => a.CompetitionAward).ThenInclude(a => a.Competition).ToListAsync();
        Assert.Equal(4, issued.Count);
        Assert.All(issued, a => Assert.Equal(Rating.BEST, a.Submission.Evaluation!.Rating));               // BR-30
        Assert.All(issued, a => Assert.Equal(a.CompetitionAward.CompetitionId, a.Submission.CompetitionId));
        Assert.All(issued.GroupBy(a => a.CompetitionAwardId), g => Assert.True(g.Count() <= g.First().CompetitionAward.Quantity)); // BR-31
        Assert.DoesNotContain(issued, a => a.CompetitionAward.AwardName == "Third Prize" && a.CompetitionAward.Competition.Title == "Hometown Landscapes");
    }

    /// <summary>2 exhibitions / 8 entries; only BEST/BETTER of GRADED competitions; 3 sold, 1 of them not paid; 3 customers.</summary>
    [Fact]
    public async Task Exhibitions_EntriesAndSales()
    {
        var entries = await Db.ExhibitionEntries.Include(e => e.Exhibition)
            .Include(e => e.Submission).ThenInclude(s => s.Evaluation)
            .Include(e => e.Submission).ThenInclude(s => s.Competition).ToListAsync();
        Assert.Equal(3, await Db.Customers.CountAsync());
        Assert.Equal(2, await Db.Exhibitions.CountAsync());
        Assert.Equal(8, entries.Count);
        Assert.All(entries, e => Assert.Contains(e.Submission.Evaluation!.Rating, new[] { Rating.BEST, Rating.BETTER })); // BR-40
        Assert.All(entries, e => Assert.Equal(CompetitionStatus.GRADED, e.Submission.Competition.Status));

        var sold = entries.Where(e => e.Status == EntryStatus.SOLD).ToList();
        Assert.Equal(3, sold.Count);
        Assert.All(sold, e => Assert.True(e.SoldPrice > 0 && e.SoldDate != null && e.CustomerId != null));   // BR-44
        Assert.Single(sold, e => !e.PaidToStudent);                                                      // TC-M09-07
        Assert.All(sold.Where(e => e.PaidToStudent), e => Assert.Equal(e.SoldPrice, e.PaidAmount));      // BR-45
        Assert.Equal(new[] { ExhibitionStatus.FINISHED, ExhibitionStatus.UPCOMING },
            (await Db.Exhibitions.OrderBy(x => x.StartDate).ToListAsync()).Select(x => x.Status));

        // BR-43: at most one active (PENDING/ACCEPTED) entry per submission
        Assert.All(entries.Where(e => e.Status is EntryStatus.PENDING or EntryStatus.ACCEPTED).GroupBy(e => e.SubmissionId), g => Assert.Single(g));
    }
}
