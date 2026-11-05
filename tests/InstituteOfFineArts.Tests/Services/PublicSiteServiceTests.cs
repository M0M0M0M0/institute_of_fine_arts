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
/// PublicSiteService (task BE2-11; F-M02-01..07, US-02, TC-M02-01..05): home page blocks with statuses calculated
/// for today, winners of the most recent GRADED competition only (US-02 AC3/AC4), upcoming exhibitions, public
/// competition search / details and the gallery of awarded paintings.
/// </summary>
public class PublicSiteServiceTests
{
    /// <summary>Today in the institute time zone.</summary>
    private static readonly DateOnly Today = DateTimeHelper.Today;

    /// <summary>staff_profile.id in the test database.</summary>
    private long _staffId;

    /// <summary>student_profile.id of "Nguyen Van An".</summary>
    private long _studentId;

    /// <summary>Empty in-memory database with one staff member and one student.</summary>
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

    /// <summary>Service under test.</summary>
    private static PublicSiteService Service(AppDbContext db) => new(db);

    /// <summary>
    /// Adds a competition with the given dates (relative to today) and a "First Prize" award. The stored status is
    /// deliberately stale (UPCOMING) unless <paramref name="graded"/>, so the tests prove it is resolved.
    /// </summary>
    private Competition AddCompetition(AppDbContext db, string title, int startIn, int endIn, bool graded = false, int publishedDaysAgo = 1)
    {
        var c = new Competition
        {
            Code = "X" + Guid.NewGuid().ToString("N")[..8], Title = title, Description = "About " + title, Rules = "One painting",
            StartDate = Today.AddDays(startIn), EndDate = Today.AddDays(endIn),
            Status = graded ? CompetitionStatus.GRADED : CompetitionStatus.UPCOMING,
            PublishedAt = graded ? DateTime.Now.AddDays(-publishedDaysAgo) : null,
            ResultRemarks = "Great season", CreatedBy = _staffId,
        };
        c.Awards.Add(new CompetitionAward { AwardName = "First Prize", Rank = 1, PrizeValue = 500m, Quantity = 1 });
        db.Competitions.Add(c);
        db.SaveChanges();
        return c;
    }

    /// <summary>Adds an entry of the student; with <paramref name="award"/> it gets "First Prize".</summary>
    private Submission AddEntry(AppDbContext db, Competition c, string title, bool award = false)
    {
        var s = new Submission
        {
            CompetitionId = c.Id, StudentId = _studentId, Title = title, ImagePath = "p.jpg", ThumbnailPath = "t.jpg",
            OriginalFileName = "p.jpg", FileSize = 10, TextContent = "A poem", Description = "Why I painted it",
            SubmittedAt = c.StartDate.ToDateTime(new TimeOnly(10, 0)),
        };
        db.Submissions.Add(s);
        db.SaveChanges();
        if (award)
        {
            db.AwardsIssued.Add(new AwardIssued
            {
                CompetitionAwardId = c.Awards.Single().Id, SubmissionId = s.Id, IssuedBy = _staffId, IssuedDate = Today.AddDays(-1),
            });
            db.SaveChanges();
        }
        return s;
    }

    /// <summary>US-02 AC1/AC2, TC-M02-02: upcoming and ongoing split by today's dates; entry count and days left.</summary>
    [Fact]
    public async Task Home_UpcomingAndOngoing_WithCountsAndDaysLeft()
    {
        using var db = CreateDb();
        AddCompetition(db, "Future", 3, 20);
        var ongoing = AddCompetition(db, "Autumn", -5, 4);
        AddCompetition(db, "Last day", -10, 0);
        AddCompetition(db, "Closed", -30, -1);
        AddEntry(db, ongoing, "Leaf");
        AddEntry(db, ongoing, "Tree");

        var home = await Service(db).GetHomeAsync();

        Assert.Equal("Future", Assert.Single(home.Upcoming).Title);
        Assert.Equal(new[] { "Last day", "Autumn" }, home.Ongoing.Select(c => c.Title));
        var autumn = home.Ongoing.Single(c => c.Title == "Autumn");
        Assert.Equal(2, autumn.SubmissionCount);
        Assert.Equal(4, autumn.DaysLeft);
        Assert.Equal(0, home.Ongoing.Single(c => c.Title == "Last day").DaysLeft);
        Assert.All(home.Ongoing, c => Assert.Equal(CompetitionStatus.ONGOING, c.Status));
    }

    /// <summary>US-02 AC3/AC4, TC-M02-04: winners of the most recently published GRADED competition; a CLOSED one is never shown.</summary>
    [Fact]
    public async Task Home_LatestGradedWinners_NotClosed()
    {
        using var db = CreateDb();
        var older = AddCompetition(db, "Hometown", -60, -40, graded: true, publishedDaysAgo: 30);
        var newer = AddCompetition(db, "Wildlife", -50, -45, graded: true, publishedDaysAgo: 2);
        var closed = AddCompetition(db, "Closed", -20, -2);
        AddEntry(db, older, "River", award: true);
        AddEntry(db, newer, "Fox", award: true);
        AddEntry(db, closed, "Unpublished", award: true);

        var home = await Service(db).GetHomeAsync();

        Assert.NotNull(home.LatestResults);
        Assert.Equal(newer.Id, home.LatestResults!.CompetitionId);
        var w = Assert.Single(home.LatestResults.Winners);
        Assert.Equal("Nguyen Van An", w.StudentName);
        Assert.Equal("First Prize", w.AwardName);
        Assert.Equal("Fox", w.ArtworkTitle);
        Assert.True(w.HasThumbnail);
    }

    /// <summary>US-02 AC4: nothing GRADED yet → no results block.</summary>
    [Fact]
    public async Task Home_NoGraded_NoResults()
    {
        using var db = CreateDb();
        var closed = AddCompetition(db, "Closed", -20, -2);
        AddEntry(db, closed, "Unpublished", award: true);

        var home = await Service(db).GetHomeAsync();

        Assert.Null(home.LatestResults);
    }

    /// <summary>F-M02-06, BR-42: only exhibitions that start after today, soonest first.</summary>
    [Fact]
    public async Task Home_UpcomingExhibitionsOnly()
    {
        using var db = CreateDb();
        db.Exhibitions.AddRange(
            new Exhibition { Name = "Later", Venue = "Museum", City = "Hanoi", StartDate = Today.AddDays(30), EndDate = Today.AddDays(35), CreatedBy = _staffId },
            new Exhibition { Name = "Soon", Venue = "Gallery", City = "Da Nang", StartDate = Today.AddDays(1), EndDate = Today.AddDays(5), CreatedBy = _staffId },
            new Exhibition { Name = "Running", Venue = "Hall", City = "Hue", StartDate = Today, EndDate = Today.AddDays(5), CreatedBy = _staffId },
            new Exhibition { Name = "Past", Venue = "Hall", City = "Hue", StartDate = Today.AddDays(-20), EndDate = Today.AddDays(-5), CreatedBy = _staffId });
        db.SaveChanges();

        var home = await Service(db).GetHomeAsync();

        Assert.Equal(new[] { "Soon", "Later" }, home.UpcomingExhibitions.Select(e => e.Name));
        Assert.Equal("Da Nang", home.UpcomingExhibitions[0].City);
        Assert.Equal("Gallery", home.UpcomingExhibitions[0].Venue);
    }

    /// <summary>TC-M02-05: search by part of the title and by the status calculated for today.</summary>
    [Fact]
    public async Task Search_ByKeywordAndStatus()
    {
        using var db = CreateDb();
        AddCompetition(db, "Colors of Autumn", -5, 5);
        AddCompetition(db, "Autumn Leaves", 5, 15);
        AddCompetition(db, "Hometown", -60, -40, graded: true);
        AddCompetition(db, "Wildlife", -30, -2);
        var service = Service(db);

        var autumn = await service.SearchCompetitionsAsync(new PublicCompetitionSearchVm { Keyword = " Autumn " });
        var ongoing = await service.SearchCompetitionsAsync(new PublicCompetitionSearchVm { Keyword = "Autumn", Status = CompetitionStatus.ONGOING });
        var closed = await service.SearchCompetitionsAsync(new PublicCompetitionSearchVm { Status = CompetitionStatus.CLOSED });
        var graded = await service.SearchCompetitionsAsync(new PublicCompetitionSearchVm { Status = CompetitionStatus.GRADED });
        var upcoming = await service.SearchCompetitionsAsync(new PublicCompetitionSearchVm { Status = CompetitionStatus.UPCOMING });

        Assert.Equal(2, autumn.TotalCount);
        var item = Assert.Single(ongoing.Items);
        Assert.Equal("Colors of Autumn", item.Title);
        Assert.Equal(CompetitionStatus.ONGOING, item.Status);
        Assert.Equal(1, item.AwardCount);
        Assert.Equal(500m, item.TopPrize);
        Assert.Equal("Wildlife", Assert.Single(closed.Items).Title);
        Assert.Equal("Hometown", Assert.Single(graded.Items).Title);
        Assert.Equal("Autumn Leaves", Assert.Single(upcoming.Items).Title);
    }

    /// <summary>F-M02-05: GRADED details have awards, remarks and winners.</summary>
    [Fact]
    public async Task Details_Graded_HasWinners()
    {
        using var db = CreateDb();
        var c = AddCompetition(db, "Hometown", -60, -40, graded: true);
        AddEntry(db, c, "River", award: true);
        AddEntry(db, c, "Hill");

        var d = await Service(db).GetCompetitionDetailsAsync(c.Id);

        Assert.Equal(CompetitionStatus.GRADED, d.Status);
        Assert.Equal("About Hometown", d.Description);
        Assert.Equal("One painting", d.Rules);
        Assert.Equal(2, d.SubmissionCount);
        Assert.Equal("First Prize", Assert.Single(d.Awards).Name);
        Assert.Equal("Great season", d.ResultRemarks);
        Assert.Equal("River", Assert.Single(d.Winners).ArtworkTitle);
    }

    /// <summary>US-02 AC4: a CLOSED competition with awards already recorded shows no winners and no remarks.</summary>
    [Fact]
    public async Task Details_ClosedNotPublished_NoWinners()
    {
        using var db = CreateDb();
        var c = AddCompetition(db, "Wildlife", -30, -2);
        AddEntry(db, c, "Fox", award: true);

        var d = await Service(db).GetCompetitionDetailsAsync(c.Id);

        Assert.Equal(CompetitionStatus.CLOSED, d.Status);
        Assert.Empty(d.Winners);
        Assert.Null(d.ResultRemarks);
    }

    /// <summary>Unknown id → NotFoundException (controller returns 404).</summary>
    [Fact]
    public async Task Details_Unknown_NotFound()
    {
        using var db = CreateDb();

        await Assert.ThrowsAsync<NotFoundException>(() => Service(db).GetCompetitionDetailsAsync(999));
    }

    /// <summary>F-M02-07, BR-20: the gallery lists awarded paintings of GRADED competitions only.</summary>
    [Fact]
    public async Task Gallery_OnlyAwardedOfGraded()
    {
        using var db = CreateDb();
        var graded = AddCompetition(db, "Hometown", -60, -40, graded: true);
        var closed = AddCompetition(db, "Wildlife", -30, -2);
        var river = AddEntry(db, graded, "River", award: true);
        AddEntry(db, graded, "Hill");
        AddEntry(db, closed, "Fox", award: true);

        var page = await Service(db).GetGalleryAsync(1);

        var g = Assert.Single(page.Items);
        Assert.Equal(river.Id, g.SubmissionId);
        Assert.Equal("Nguyen Van An", g.StudentName);
        Assert.Equal("First Prize", g.AwardName);
        Assert.Equal("Hometown", g.CompetitionTitle);
    }
}
