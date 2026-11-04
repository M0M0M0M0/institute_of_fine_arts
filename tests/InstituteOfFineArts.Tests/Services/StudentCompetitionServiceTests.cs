using InstituteOfFineArts.Web.Data;
using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Helpers;
using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.Services.Implementations;
using Microsoft.EntityFrameworkCore;

namespace InstituteOfFineArts.Tests.Services;

/// <summary>
/// StudentCompetitionService (task BE1-06, F-M05-09): only upcoming / ongoing competitions are listed (status
/// calculated, not stored), the "already submitted" flag, details with awards and the submit permission.
/// </summary>
public class StudentCompetitionServiceTests
{
    /// <summary>Today in the institute time zone.</summary>
    private static readonly DateOnly Today = DateTimeHelper.Today;

    /// <summary>student_profile.id of the signed-in student.</summary>
    private const long Me = 1;

    /// <summary>student_profile.id of another student.</summary>
    private const long Other = 2;

    /// <summary>Empty in-memory database.</summary>
    private static AppDbContext CreateDb()
        => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    /// <summary>Adds a competition with dates relative to today, the given stored status, awards and entries.</summary>
    private static Competition Add(AppDbContext db, string title, int startIn, int endIn, CompetitionStatus stored, params long[] entriesBy)
    {
        var c = new Competition
        {
            Code = "X" + Guid.NewGuid().ToString("N")[..8], Title = title, Rules = "One painting per student",
            StartDate = Today.AddDays(startIn), EndDate = Today.AddDays(endIn), Status = stored, CreatedBy = 1,
        };
        c.Awards.Add(new CompetitionAward { AwardName = "Second Prize", Rank = 2, PrizeValue = 200m, Quantity = 1 });
        c.Awards.Add(new CompetitionAward { AwardName = "First Prize", Rank = 1, PrizeValue = 300m, Quantity = 1 });
        foreach (var student in entriesBy)
        {
            c.Submissions.Add(new Submission { StudentId = student, Title = $"Entry {student}", ImagePath = "p.jpg", OriginalFileName = "p.jpg", TextContent = "t", Description = "d", SubmittedAt = Today.ToDateTime(new TimeOnly(9, 0)) });
        }
        db.Competitions.Add(c);
        db.SaveChanges();
        return c;
    }

    /// <summary>List: upcoming and ongoing only (even with a stale stored status); ongoing first; award summary and submitted flag.</summary>
    [Fact]
    public async Task List_OnlyUpcomingAndOngoing()
    {
        using var db = CreateDb();
        Add(db, "Hometown Landscapes", -60, -30, CompetitionStatus.GRADED, Me);
        Add(db, "City at Night", -25, -1, CompetitionStatus.ONGOING);            // stored status stale: really CLOSED
        Add(db, "Graded but dates open", -5, 5, CompetitionStatus.GRADED);         // GRADED is never listed
        Add(db, "Colors of Autumn", -10, 10, CompetitionStatus.ONGOING, Other);
        Add(db, "Deadline Today", -7, 0, CompetitionStatus.ONGOING, Me);
        Add(db, "Lunar New Year 2027", 5, 30, CompetitionStatus.ONGOING);         // stored status stale: really UPCOMING

        var list = await new StudentCompetitionService(db).GetOpenCompetitionsAsync(Me);

        Assert.Equal(new[] { "Deadline Today", "Colors of Autumn", "Lunar New Year 2027" }, list.Select(c => c.Title));
        Assert.Equal(CompetitionStatus.ONGOING, list[0].Status);
        Assert.Equal(CompetitionStatus.UPCOMING, list[2].Status);
        Assert.True(list[0].HasSubmitted);
        Assert.False(list[1].HasSubmitted);   // only another student submitted there
        Assert.Equal(2, list[1].AwardCount);
        Assert.Equal(300m, list[1].TopPrize);
    }

    /// <summary>Details of an ongoing competition: awards by rank, rules, can submit when no own entry.</summary>
    [Fact]
    public async Task Details_Ongoing_NotSubmitted_CanSubmit()
    {
        using var db = CreateDb();
        var c = Add(db, "Colors of Autumn", -10, 10, CompetitionStatus.UPCOMING, Other);

        var vm = await new StudentCompetitionService(db).GetDetailsAsync(c.Id, Me);

        Assert.Equal(CompetitionStatus.ONGOING, vm.Status);
        Assert.Equal("One painting per student", vm.Rules);
        Assert.Equal(new[] { "First Prize", "Second Prize" }, vm.Awards.Select(a => a.Name));
        Assert.Null(vm.MySubmission);
        Assert.True(vm.CanSubmit);
    }

    /// <summary>Details after submitting: own entry shown, no submit link.</summary>
    [Fact]
    public async Task Details_AlreadySubmitted_CannotSubmit()
    {
        using var db = CreateDb();
        var c = Add(db, "Colors of Autumn", -10, 10, CompetitionStatus.ONGOING, Other);

        var vm = await new StudentCompetitionService(db).GetDetailsAsync(c.Id, Other);

        Assert.NotNull(vm.MySubmission);
        Assert.Equal("Entry 2", vm.MySubmission!.Title);
        Assert.False(vm.CanSubmit);
    }

    /// <summary>Upcoming, closed and graded competitions can be opened but never allow submitting.</summary>
    [Theory]
    [InlineData(5, 30, CompetitionStatus.UPCOMING, CompetitionStatus.UPCOMING)]
    [InlineData(-25, -1, CompetitionStatus.ONGOING, CompetitionStatus.CLOSED)]
    [InlineData(-60, -30, CompetitionStatus.GRADED, CompetitionStatus.GRADED)]
    public async Task Details_NotOngoing_CannotSubmit(int startIn, int endIn, CompetitionStatus stored, CompetitionStatus expected)
    {
        using var db = CreateDb();
        var c = Add(db, "Any", startIn, endIn, stored);

        var vm = await new StudentCompetitionService(db).GetDetailsAsync(c.Id, Me);

        Assert.Equal(expected, vm.Status);
        Assert.False(vm.CanSubmit);
    }

    /// <summary>Unknown competition → NotFoundException.</summary>
    [Fact]
    public async Task Details_Unknown_NotFound()
    {
        using var db = CreateDb();
        await Assert.ThrowsAsync<NotFoundException>(() => new StudentCompetitionService(db).GetDetailsAsync(42, Me));
    }
}
