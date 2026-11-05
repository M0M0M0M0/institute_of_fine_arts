using InstituteOfFineArts.Web.Data;
using InstituteOfFineArts.Web.Helpers;
using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.Services.Implementations;
using InstituteOfFineArts.Web.ViewModels.Student.Results;
using Microsoft.EntityFrameworkCore;

namespace InstituteOfFineArts.Tests.Services;

/// <summary>
/// StudentResultService (task BE2-08; F-M06-07..09, F-M08-04 pub): own results with evaluation and award only once
/// GRADED (BR-24, US-10 AC2/AC3), BEST works of every student in GRADED competitions (US-10 AC4, A-13), awards of
/// GRADED competitions with search (US-11 AC4, TC-M08-05).
/// </summary>
public class StudentResultServiceTests
{
    /// <summary>Today in the institute time zone.</summary>
    private static readonly DateOnly Today = DateTimeHelper.Today;

    /// <summary>staff_profile.id of the evaluator in the test database.</summary>
    private long _staffId;

    /// <summary>student_profile.id of "Nguyen Van An" (the signed-in student).</summary>
    private long _student1;

    /// <summary>student_profile.id of "Tran Thi Binh" (another student).</summary>
    private long _student2;

    /// <summary>Empty in-memory database with one staff member and two students.</summary>
    private AppDbContext CreateDb()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var staff = new StaffProfile
        {
            StaffCode = "NV0003", JoinedDate = new DateOnly(2024, 1, 1),
            Account = new Account { Username = "staff01", FullName = "Le Minh Hoang", Email = "s1@ifa.local", Role = Role.STAFF, PasswordHash = "x" },
        };
        var student1 = new StudentProfile
        {
            StudentCode = "HV0001", EnrollmentDate = new DateOnly(2025, 9, 1), CourseName = "Painting",
            Account = new Account { Username = "student01", FullName = "Nguyen Van An", Email = "st1@ifa.local", Role = Role.STUDENT, PasswordHash = "x" },
        };
        var student2 = new StudentProfile
        {
            StudentCode = "HV0002", EnrollmentDate = new DateOnly(2025, 9, 1), CourseName = "Painting",
            Account = new Account { Username = "student02", FullName = "Tran Thi Binh", Email = "st2@ifa.local", Role = Role.STUDENT, PasswordHash = "x" },
        };
        db.StaffProfiles.Add(staff);
        db.StudentProfiles.AddRange(student1, student2);
        db.SaveChanges();
        _staffId = staff.Id;
        _student1 = student1.Id;
        _student2 = student2.Id;
        return db;
    }

    /// <summary>Service under test.</summary>
    private static StudentResultService Service(AppDbContext db) => new(db);

    /// <summary>Adds a competition that resolves to <paramref name="status"/> (GRADED is stored) with a "First Prize" award.</summary>
    private Competition AddCompetition(AppDbContext db, CompetitionStatus status, string title = "City at Night", int publishedDaysAgo = 1)
    {
        // Dates that make CompetitionStatusResolver return the wanted status
        var (start, end) = status switch
        {
            CompetitionStatus.UPCOMING => (Today.AddDays(5), Today.AddDays(30)),
            CompetitionStatus.ONGOING => (Today.AddDays(-5), Today.AddDays(5)),
            _ => (Today.AddDays(-30), Today.AddDays(-5)),
        };
        var c = new Competition
        {
            Code = "X" + Guid.NewGuid().ToString("N")[..8], Title = title, StartDate = start, EndDate = end,
            Status = status == CompetitionStatus.GRADED ? CompetitionStatus.GRADED : CompetitionStatus.UPCOMING,
            PublishedAt = status == CompetitionStatus.GRADED ? DateTime.Now.AddDays(-publishedDaysAgo) : null,
            CreatedBy = _staffId,
        };
        c.Awards.Add(new CompetitionAward { AwardName = "First Prize", Rank = 1, PrizeValue = 500m, Quantity = 1 });
        db.Competitions.Add(c);
        db.SaveChanges();
        return c;
    }

    /// <summary>Adds an entry, optionally evaluated and awarded "First Prize".</summary>
    private Submission AddEntry(AppDbContext db, Competition c, long studentId, string title, Rating? rating = null, bool award = false)
    {
        var s = new Submission
        {
            CompetitionId = c.Id, StudentId = studentId, Title = title, ImagePath = "p.jpg", ThumbnailPath = "t.jpg",
            OriginalFileName = "p.jpg", FileSize = 10, TextContent = "A poem", Description = "Why I painted it",
            SubmittedAt = c.StartDate.ToDateTime(new TimeOnly(10, 0)),
        };
        db.Submissions.Add(s);
        db.SaveChanges();
        if (rating is { } r)
        {
            db.Evaluations.Add(new Evaluation
            {
                SubmissionId = s.Id, EvaluatedBy = _staffId, Rating = r, PositivePoints = "Strong composition",
                NegativePoints = "Dark corners", ImprovementAreas = "Perspective", EvaluatedAt = DateTime.Now.AddDays(-2),
            });
        }
        if (award)
        {
            db.AwardsIssued.Add(new AwardIssued
            {
                CompetitionAwardId = c.Awards.Single().Id, SubmissionId = s.Id, IssuedBy = _staffId,
                IssuedDate = Today.AddDays(-1), Remarks = "Outstanding work",
            });
        }
        db.SaveChanges();
        return s;
    }

    /// <summary>US-10 AC3: GRADED → rating, strengths, weaknesses, areas of improvement and the award are shown.</summary>
    [Fact]
    public async Task MyResults_Graded_ShowsEvaluationAndAward()
    {
        using var db = CreateDb();
        var c = AddCompetition(db, CompetitionStatus.GRADED);
        AddEntry(db, c, _student1, "Neon Street", Rating.BEST, award: true);

        var page = await Service(db).SearchMyResultsAsync(_student1, new MyResultSearchVm());

        var item = Assert.Single(page.Items);
        Assert.True(item.IsPublished);
        Assert.Equal(Rating.BEST, item.Rating);
        Assert.Equal("Strong composition", item.PositivePoints);
        Assert.Equal("Dark corners", item.NegativePoints);
        Assert.Equal("Perspective", item.ImprovementAreas);
        Assert.Equal("First Prize", item.AwardName);
        Assert.Equal(1, item.AwardRank);
        Assert.Equal(500m, item.PrizeValue);
    }

    /// <summary>BR-24, US-10 AC2: CLOSED and evaluated but not published → no rating, no comments, no award ("Awaiting results").</summary>
    [Fact]
    public async Task MyResults_ClosedNotPublished_EvaluationHidden()
    {
        using var db = CreateDb();
        var c = AddCompetition(db, CompetitionStatus.CLOSED);
        AddEntry(db, c, _student1, "Neon Street", Rating.BEST, award: true);

        var page = await Service(db).SearchMyResultsAsync(_student1, new MyResultSearchVm());

        var item = Assert.Single(page.Items);
        Assert.Equal(CompetitionStatus.CLOSED, item.CompetitionStatus);
        Assert.False(item.IsPublished);
        Assert.Null(item.Rating);
        Assert.Null(item.PositivePoints);
        Assert.Null(item.NegativePoints);
        Assert.Null(item.ImprovementAreas);
        Assert.Null(item.AwardName);
        Assert.Equal("Awaiting results", StudentResultService.AwaitingResultsMessage);
    }

    /// <summary>BR-20: only the signed-in student's entries; competition filter works.</summary>
    [Fact]
    public async Task MyResults_OnlyOwnEntries_FilterByCompetition()
    {
        using var db = CreateDb();
        var c1 = AddCompetition(db, CompetitionStatus.GRADED, "Hometown");
        var c2 = AddCompetition(db, CompetitionStatus.ONGOING, "Autumn");
        AddEntry(db, c1, _student1, "Mine 1", Rating.GOOD);
        AddEntry(db, c2, _student1, "Mine 2");
        AddEntry(db, c1, _student2, "Not mine", Rating.BEST);

        var all = await Service(db).SearchMyResultsAsync(_student1, new MyResultSearchVm());
        var filtered = await Service(db).SearchMyResultsAsync(_student1, new MyResultSearchVm { CompetitionId = c2.Id });

        Assert.Equal(new[] { "Mine 1", "Mine 2" }, all.Items.Select(i => i.Title).OrderBy(t => t));
        Assert.Equal("Mine 2", Assert.Single(filtered.Items).Title);
    }

    /// <summary>BR-24: the rating filter never matches an evaluation that is not published yet.</summary>
    [Fact]
    public async Task MyResults_RatingFilter_IgnoresUnpublished()
    {
        using var db = CreateDb();
        var graded = AddCompetition(db, CompetitionStatus.GRADED, "Hometown");
        var closed = AddCompetition(db, CompetitionStatus.CLOSED, "Wildlife");
        AddEntry(db, graded, _student1, "Published BEST", Rating.BEST);
        AddEntry(db, closed, _student1, "Unpublished BEST", Rating.BEST);

        var page = await Service(db).SearchMyResultsAsync(_student1, new MyResultSearchVm { Rating = Rating.BEST });

        Assert.Equal("Published BEST", Assert.Single(page.Items).Title);
    }

    /// <summary>US-10 AC4, A-13: BEST entries of every student, only of GRADED competitions, with staff comments.</summary>
    [Fact]
    public async Task Best_OnlyBestOfGraded_WithComments()
    {
        using var db = CreateDb();
        var graded = AddCompetition(db, CompetitionStatus.GRADED, "Hometown");
        var closed = AddCompetition(db, CompetitionStatus.CLOSED, "Wildlife");
        AddEntry(db, graded, _student1, "Best of An", Rating.BEST, award: true);
        AddEntry(db, graded, _student2, "Best of Binh", Rating.BEST);
        AddEntry(db, graded, _student2, "Good of Binh", Rating.GOOD);
        AddEntry(db, closed, _student1, "Unpublished best", Rating.BEST);

        var page = await Service(db).SearchBestWorksAsync(new BestWorkSearchVm());

        Assert.Equal(new[] { "Best of An", "Best of Binh" }, page.Items.Select(i => i.Title));
        var first = page.Items[0];
        Assert.Equal("Nguyen Van An", first.StudentName);
        Assert.Equal("Hometown", first.CompetitionTitle);
        Assert.Equal("Strong composition", first.PositivePoints);
        Assert.Equal("First Prize", first.AwardName);
        Assert.Null(page.Items[1].AwardName);
    }

    /// <summary>F-M06-08: filter by competition.</summary>
    [Fact]
    public async Task Best_FilterByCompetition()
    {
        using var db = CreateDb();
        var c1 = AddCompetition(db, CompetitionStatus.GRADED, "Hometown");
        var c2 = AddCompetition(db, CompetitionStatus.GRADED, "Wildlife");
        AddEntry(db, c1, _student1, "River", Rating.BEST);
        AddEntry(db, c2, _student1, "Fox", Rating.BEST);

        var page = await Service(db).SearchBestWorksAsync(new BestWorkSearchVm { CompetitionId = c2.Id });

        Assert.Equal("Fox", Assert.Single(page.Items).Title);
    }

    /// <summary>US-11 AC4, TC-M08-05: awards of GRADED competitions only, with student, award, artwork and remarks.</summary>
    [Fact]
    public async Task Awards_OnlyGraded()
    {
        using var db = CreateDb();
        var graded = AddCompetition(db, CompetitionStatus.GRADED, "Hometown");
        var closed = AddCompetition(db, CompetitionStatus.CLOSED, "Wildlife");
        var s = AddEntry(db, graded, _student1, "River", Rating.BEST, award: true);
        AddEntry(db, closed, _student2, "Fox", Rating.BEST, award: true);

        var page = await Service(db).SearchAwardsAsync(new IssuedAwardSearchVm());

        var a = Assert.Single(page.Items);
        Assert.Equal("Hometown", a.CompetitionTitle);
        Assert.Equal("First Prize", a.AwardName);
        Assert.Equal(1, a.Rank);
        Assert.Equal(500m, a.PrizeValue);
        Assert.Equal("Nguyen Van An", a.StudentName);
        Assert.Equal(s.Id, a.SubmissionId);
        Assert.Equal("River", a.ArtworkTitle);
        Assert.Equal(Today.AddDays(-1), a.IssuedDate);
        Assert.Equal("Outstanding work", a.Remarks);
    }

    /// <summary>F-M08-04: search by competition, part of the student name, part of the award name and issued date.</summary>
    [Fact]
    public async Task Awards_Search_Filters()
    {
        using var db = CreateDb();
        var c1 = AddCompetition(db, CompetitionStatus.GRADED, "Hometown");
        var c2 = AddCompetition(db, CompetitionStatus.GRADED, "Wildlife");
        AddEntry(db, c1, _student1, "River", Rating.BEST, award: true);
        AddEntry(db, c2, _student2, "Fox", Rating.BEST, award: true);
        var service = Service(db);

        Assert.Equal("Fox", Assert.Single((await service.SearchAwardsAsync(new IssuedAwardSearchVm { CompetitionId = c2.Id })).Items).ArtworkTitle);
        Assert.Equal("River", Assert.Single((await service.SearchAwardsAsync(new IssuedAwardSearchVm { StudentName = " Van An " })).Items).ArtworkTitle);
        Assert.Equal(2, (await service.SearchAwardsAsync(new IssuedAwardSearchVm { Award = "First" })).TotalCount);
        Assert.Equal(0, (await service.SearchAwardsAsync(new IssuedAwardSearchVm { Award = "Honorable" })).TotalCount);
        Assert.Equal(0, (await service.SearchAwardsAsync(new IssuedAwardSearchVm { From = Today })).TotalCount);
        Assert.Equal(2, (await service.SearchAwardsAsync(new IssuedAwardSearchVm { From = Today.AddDays(-1), To = Today.AddDays(-1) })).TotalCount);
    }

    /// <summary>Filter options: only GRADED competitions, most recently published first.</summary>
    [Fact]
    public async Task GradedCompetitions_OnlyGraded_LatestFirst()
    {
        using var db = CreateDb();
        AddCompetition(db, CompetitionStatus.GRADED, "Older", publishedDaysAgo: 30);
        AddCompetition(db, CompetitionStatus.GRADED, "Newer", publishedDaysAgo: 1);
        AddCompetition(db, CompetitionStatus.CLOSED, "Closed");

        var options = await Service(db).GetGradedCompetitionsAsync();

        Assert.Equal(new[] { "Newer", "Older" }, options.Select(o => o.Title));
    }
}
