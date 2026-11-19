using InstituteOfFineArts.Web.Data;
using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Helpers;
using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.Services.Implementations;
using InstituteOfFineArts.Web.ViewModels.Public;
using Microsoft.EntityFrameworkCore;

namespace InstituteOfFineArts.Tests.Services;

/// <summary>
/// ArtworkCatalogService (FE101 gallery pages with real data): only public paintings (BEST or awarded, GRADED
/// competition – BR-20) are listed; categories match words in the title / description; private paintings → 404.
/// </summary>
public class ArtworkCatalogServiceTests
{
    private static readonly DateOnly Today = DateTimeHelper.Today;

    private long _staffId;
    private long _studentId;

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
        _staffId = staff.Id;
        _studentId = student.Id;
        return db;
    }

    private Competition AddCompetition(AppDbContext db, string title, int startIn, int endIn, bool graded = false, int publishedDaysAgo = 1)
    {
        var c = new Competition
        {
            Code = "X" + Guid.NewGuid().ToString("N")[..8], Title = title, Description = "About " + title,
            StartDate = Today.AddDays(startIn), EndDate = Today.AddDays(endIn),
            Status = graded ? CompetitionStatus.GRADED : CompetitionStatus.UPCOMING,
            PublishedAt = graded ? DateTime.Now.AddDays(-publishedDaysAgo) : null, CreatedBy = _staffId,
        };
        c.Awards.Add(new CompetitionAward { AwardName = "First Prize", Rank = 1, PrizeValue = 300m, Quantity = 5 });
        db.Competitions.Add(c);
        db.SaveChanges();
        return c;
    }

    /// <summary>Entry with an optional rating and award.</summary>
    private Submission AddEntry(AppDbContext db, Competition c, string title, string description, Rating? rating = null, bool award = false)
    {
        var s = new Submission
        {
            CompetitionId = c.Id, StudentId = _studentId, Title = title, ImagePath = "p.jpg", ThumbnailPath = "t.jpg",
            OriginalFileName = "p.jpg", FileSize = 10, TextContent = "A poem", Description = description,
            SubmittedAt = c.StartDate.ToDateTime(new TimeOnly(10, 0)),
        };
        db.Submissions.Add(s);
        db.SaveChanges();
        if (rating != null)
        {
            db.Evaluations.Add(new Evaluation { SubmissionId = s.Id, EvaluatedBy = _staffId, Rating = rating.Value, PositivePoints = "p", EvaluatedAt = DateTime.Now });
        }
        if (award)
        {
            db.AwardsIssued.Add(new AwardIssued { CompetitionAwardId = c.Awards.Single().Id, SubmissionId = s.Id, IssuedBy = _staffId, IssuedDate = Today });
        }
        db.SaveChanges();
        return s;
    }

    /// <summary>Category pages list public paintings whose title or description has a keyword; private ones never.</summary>
    [Fact]
    public async Task Category_MatchesKeywords_OnlyPublic()
    {
        using var db = CreateDb();
        var graded = AddCompetition(db, "Wildlife", -40, -10, graded: true);
        var horse = AddEntry(db, graded, "Running Free", "Oil painting of a horse in a field", Rating.BEST);
        var bird = AddEntry(db, graded, "Kingfisher", "Watercolor of a bird", Rating.BETTER, award: true);
        AddEntry(db, graded, "Old Dog", "Oil painting of a dog", Rating.GOOD);                  // not public
        var ongoing = AddCompetition(db, "Autumn", -5, 5);
        AddEntry(db, ongoing, "Fox", "Oil painting of a fox");                                   // not graded
        var service = new ArtworkCatalogService(db);

        var animals = await service.GetCategoryAsync(service.FindCategory("SUBJECT", "animals")!, 1);
        var oil = await service.GetCategoryAsync(service.FindCategory("medium", "OilPaintings")!, 1);
        var water = await service.GetCategoryAsync(service.FindCategory("medium", "WatercolorPaintings")!, 1);
        var acrylic = await service.GetCategoryAsync(service.FindCategory("medium", "AcrylicPaintings")!, 1);

        Assert.Equal(new[] { bird.Id, horse.Id }, animals.Items.Items.Select(a => a.SubmissionId)); // awarded first
        Assert.Equal(horse.Id, Assert.Single(oil.Items.Items).SubmissionId);
        Assert.Equal(bird.Id, Assert.Single(water.Items.Items).SubmissionId);
        Assert.Equal(0, acrylic.Items.TotalCount);
        Assert.Equal("First Prize", animals.Items.Items[0].AwardName);
        Assert.Equal("Oil Paintings", oil.Category!.Label);
    }

    /// <summary>A keyword matches the start of a word only ("oil" is not found in "toil").</summary>
    [Fact]
    public async Task Category_WholeWordStart()
    {
        using var db = CreateDb();
        var graded = AddCompetition(db, "Work", -40, -10, graded: true);
        AddEntry(db, graded, "Daily Toil", "Farmers at work", Rating.BEST);
        var service = new ArtworkCatalogService(db);

        var oil = await service.GetCategoryAsync(service.FindCategory("medium", "OilPaintings")!, 1);

        Assert.Equal(0, oil.Items.TotalCount);
    }

    /// <summary>Unknown group or category → null (the controller answers 404); the menu has 10 categories.</summary>
    [Fact]
    public void FindCategory_FixedList()
    {
        var service = new ArtworkCatalogService(null!);

        Assert.Null(service.FindCategory("subject", "../Shared/_Layout"));
        Assert.Null(service.FindCategory("colour", "Animals"));
        Assert.Equal(10, service.Categories.Count);
    }

    /// <summary>New arrivals: public paintings of the 3 most recently published competitions only.</summary>
    [Fact]
    public async Task NewArrivals_LatestPublishedCompetitions()
    {
        using var db = CreateDb();
        var ids = new List<long>();
        for (var i = 0; i < 4; i++)
        {
            var c = AddCompetition(db, $"C{i}", -100 + i, -90 + i, graded: true, publishedDaysAgo: 40 - i * 10);
            ids.Add(AddEntry(db, c, $"Painting {i}", "x", Rating.BEST).Id);
        }
        var service = new ArtworkCatalogService(db);

        var page = await service.GetNewArrivalsAsync(1);

        Assert.Equal(new[] { ids[3], ids[2], ids[1] }, page.Items.Items.Select(a => a.SubmissionId));
    }

    /// <summary>Paging: 12 per page; a page past the end shows the last page.</summary>
    [Fact]
    public async Task All_Paged()
    {
        using var db = CreateDb();
        var c = AddCompetition(db, "Big", -40, -10, graded: true);
        for (var i = 0; i < 14; i++) AddEntry(db, c, $"P{i}", "x", Rating.BEST);
        var service = new ArtworkCatalogService(db);

        var first = await service.GetAllAsync(1);
        var beyond = await service.GetAllAsync(9);

        Assert.Equal(14, first.Items.TotalCount);
        Assert.Equal(12, first.Items.Items.Count);
        Assert.Equal(2, beyond.Items.Page);
        Assert.Equal(2, beyond.Items.Items.Count);
    }

    /// <summary>A competition's artworks: empty with a "published later" message until GRADED; unknown → 404.</summary>
    [Fact]
    public async Task CompetitionArtworks_OnlyWhenGraded()
    {
        using var db = CreateDb();
        var closed = AddCompetition(db, "City at Night", -25, -1);
        AddEntry(db, closed, "Bridge", "x", Rating.BEST);
        var service = new ArtworkCatalogService(db);

        var list = await service.GetCompetitionArtworksAsync(closed.Id, 1);

        Assert.Equal(0, list.Items.TotalCount);
        Assert.Contains("once the results are published", list.EmptyMessage);
        await Assert.ThrowsAsync<NotFoundException>(() => service.GetCompetitionArtworksAsync(9999, 1));
    }

    /// <summary>Showcase: ongoing before upcoming, closed ones hidden; latest results newest first (max 4).</summary>
    [Fact]
    public async Task Showcase_OpenThenResults()
    {
        using var db = CreateDb();
        AddCompetition(db, "Upcoming", 5, 30);
        AddCompetition(db, "Ongoing", -5, 10);
        AddCompetition(db, "Closed", -30, -1);
        for (var i = 0; i < 5; i++) AddCompetition(db, $"Graded {i}", -100, -90, graded: true, publishedDaysAgo: 50 - i);
        var service = new ArtworkCatalogService(db);

        var showcase = await service.GetShowcaseAsync();

        Assert.Equal(new[] { "Ongoing", "Upcoming" }, showcase.Open.Select(c => c.Title));
        Assert.Equal(new[] { "Graded 4", "Graded 3", "Graded 2", "Graded 1" }, showcase.Results.Select(c => c.Title));
        Assert.Equal(CompetitionStatus.ONGOING, showcase.Open[0].Status);
    }

    /// <summary>Tabs: Open = ongoing then upcoming; Past = closed then graded (every one, not only 4); All = everything.</summary>
    [Fact]
    public async Task SearchShowcase_Tabs()
    {
        using var db = CreateDb();
        AddCompetition(db, "Upcoming", 5, 30);
        AddCompetition(db, "Ongoing", -5, 10);
        AddCompetition(db, "Closed", -30, -1);
        for (var i = 0; i < 5; i++) AddCompetition(db, $"Graded {i}", -100, -90, graded: true, publishedDaysAgo: 50 - i);
        var service = new ArtworkCatalogService(db);

        var open = await service.SearchShowcaseAsync(new GalleryCompetitionSearchVm { Tab = GalleryCompetitionTab.Open }, pageSize: 20);
        var past = await service.SearchShowcaseAsync(new GalleryCompetitionSearchVm { Tab = GalleryCompetitionTab.Past }, pageSize: 20);
        var all = await service.SearchShowcaseAsync(new GalleryCompetitionSearchVm { Tab = GalleryCompetitionTab.All }, pageSize: 20);

        Assert.Equal(new[] { "Ongoing", "Upcoming" }, open.Items.Select(c => c.Title));
        // The closed one first, then all 5 graded ones, latest results first
        Assert.Equal(new[] { "Closed", "Graded 4", "Graded 3", "Graded 2", "Graded 1", "Graded 0" }, past.Items.Select(c => c.Title));
        Assert.Equal(CompetitionStatus.CLOSED, past.Items[0].Status);
        Assert.Equal(8, all.TotalCount);
    }

    /// <summary>Keyword (title or code) and status filters; a status of another tab switches the tab.</summary>
    [Fact]
    public async Task SearchShowcase_KeywordAndStatus()
    {
        using var db = CreateDb();
        AddCompetition(db, "Spring Landscapes", -5, 10);
        AddCompetition(db, "City at Night", -5, 12);
        var graded = AddCompetition(db, "Winter Landscapes", -100, -90, graded: true);
        var service = new ArtworkCatalogService(db);

        var landscapes = await service.SearchShowcaseAsync(new GalleryCompetitionSearchVm { Tab = GalleryCompetitionTab.All, Keyword = " Landscapes " }); // SQL Server is case-insensitive; the in-memory test database is not
        var byCode = await service.SearchShowcaseAsync(new GalleryCompetitionSearchVm { Tab = GalleryCompetitionTab.All, Keyword = graded.Code });
        var search = new GalleryCompetitionSearchVm { Tab = GalleryCompetitionTab.Open, Status = CompetitionStatus.GRADED };
        var gradedOnly = await service.SearchShowcaseAsync(search);

        Assert.Equal(new[] { "Spring Landscapes", "Winter Landscapes" }, landscapes.Items.Select(c => c.Title));
        Assert.Equal(new[] { "Winter Landscapes" }, byCode.Items.Select(c => c.Title));
        Assert.Equal(new[] { "Winter Landscapes" }, gradedOnly.Items.Select(c => c.Title));
        Assert.Equal(GalleryCompetitionTab.Past, search.Tab);
    }

    /// <summary>Paging: page size respected, a page past the end shows the last page.</summary>
    [Fact]
    public async Task SearchShowcase_Paging()
    {
        using var db = CreateDb();
        for (var i = 0; i < 7; i++) AddCompetition(db, $"Graded {i}", -100, -90, graded: true, publishedDaysAgo: 50 - i);
        var service = new ArtworkCatalogService(db);

        var first = await service.SearchShowcaseAsync(new GalleryCompetitionSearchVm { Tab = GalleryCompetitionTab.Past });
        var beyond = await service.SearchShowcaseAsync(new GalleryCompetitionSearchVm { Tab = GalleryCompetitionTab.Past, Page = 9 });

        Assert.Equal(6, first.Items.Count);
        Assert.Equal(7, first.TotalCount);
        Assert.True(first.HasNext);
        Assert.Equal(2, beyond.Page);
        Assert.Single(beyond.Items);
    }

    /// <summary>No filter at all = the highlights view (open + latest results); any filter = the paged list.</summary>
    [Fact]
    public void SearchVm_IsDefault()
    {
        Assert.True(new GalleryCompetitionSearchVm().IsDefault);
        Assert.False(new GalleryCompetitionSearchVm { Tab = GalleryCompetitionTab.Past }.IsDefault);
        Assert.False(new GalleryCompetitionSearchVm { Keyword = "x" }.IsDefault);
        Assert.False(new GalleryCompetitionSearchVm { Status = CompetitionStatus.ONGOING }.IsDefault);
    }

    /// <summary>Artwork page: public painting with its award; a private one (not BEST, no award) → 404.</summary>
    [Fact]
    public async Task Artwork_PublicOnly()
    {
        using var db = CreateDb();
        var c = AddCompetition(db, "Wildlife", -40, -10, graded: true);
        var winner = AddEntry(db, c, "Fox in the Snow", "x", Rating.BEST, award: true);
        var other = AddEntry(db, c, "Butterflies", "x", Rating.NORMAL);
        var service = new ArtworkCatalogService(db);

        var detail = await service.GetArtworkAsync(winner.Id);

        Assert.Equal("Fox in the Snow", detail.Title);
        Assert.Equal("First Prize", detail.AwardName);
        Assert.True(detail.IsBest);
        Assert.Equal("Nguyen Van An", detail.StudentName);
        await Assert.ThrowsAsync<NotFoundException>(() => service.GetArtworkAsync(other.Id));
    }
}
