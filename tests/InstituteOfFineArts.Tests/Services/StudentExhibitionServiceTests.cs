using InstituteOfFineArts.Web.Data;
using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Helpers;
using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.Services.Implementations;
using InstituteOfFineArts.Web.ViewModels.Staff.Exhibitions;
using Microsoft.EntityFrameworkCore;

namespace InstituteOfFineArts.Tests.Services;

/// <summary>
/// StudentExhibitionService (task BE2-09; F-M09-02, F-M09-03, SCR-48): upcoming first, own entries only,
/// "Propose an artwork" only while UPCOMING (BR-42). Dates are relative to today.
/// </summary>
public class StudentExhibitionServiceTests
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

    /// <summary>Adds an exhibition directly, with a stale stored status on purpose.</summary>
    private static Exhibition Add(AppDbContext db, string name, int startIn, int endIn)
    {
        var e = new Exhibition
        {
            Name = name, Venue = "Venue", City = "Hanoi", StartDate = Today.AddDays(startIn), EndDate = Today.AddDays(endIn),
            Status = ExhibitionStatus.UPCOMING, CreatedBy = 1,
        };
        db.Exhibitions.Add(e);
        db.SaveChanges();
        return e;
    }

    /// <summary>Adds an entry of <paramref name="studentId"/>'s artwork to the exhibition.</summary>
    private static void AddEntry(AppDbContext db, Exhibition e, long studentId, string title, EntryStatus status = EntryStatus.PENDING)
    {
        db.ExhibitionEntries.Add(new ExhibitionEntry
        {
            ExhibitionId = e.Id, AskingPrice = 200m, Status = status, ProposedBy = 1,
            Submission = new Submission { CompetitionId = 1, StudentId = studentId, Title = title, ImagePath = "p.jpg", OriginalFileName = "p.jpg", TextContent = "t", Description = "d" },
        });
        db.SaveChanges();
    }

    /// <summary>SCR-48: UPCOMING first (soonest first), then ONGOING, then FINISHED; own entries counted; status calculated.</summary>
    [Fact]
    public async Task Search_UpcomingFirst_CountsOwnEntries()
    {
        using var db = CreateDb();
        var finished = Add(db, "Finished", -30, -20);
        Add(db, "Ongoing", -1, 3);
        Add(db, "Later", 40, 45);
        var soon = Add(db, "Soon", 5, 10);
        AddEntry(db, soon, Me, "Mine");
        AddEntry(db, soon, Other, "Theirs");
        AddEntry(db, finished, Me, "Old", EntryStatus.UNSOLD);

        var page = await new StudentExhibitionService(db).SearchAsync(new ExhibitionSearchVm(), Me);

        Assert.Equal(new[] { "Soon", "Later", "Ongoing", "Finished" }, page.Items.Select(x => x.Name));
        Assert.Equal(ExhibitionStatus.ONGOING, page.Items[2].Status);
        Assert.Equal(ExhibitionStatus.FINISHED, page.Items[3].Status);
        Assert.Equal(1, page.Items[0].MyEntryCount);
        Assert.Equal(1, page.Items[3].MyEntryCount);

        // The shared filters work for students too
        var upcoming = await new StudentExhibitionService(db).SearchAsync(new ExhibitionSearchVm { Status = ExhibitionStatus.UPCOMING }, Me);
        Assert.Equal(2, upcoming.TotalCount);
    }

    /// <summary>F-M09-03: details show only the student's own entries; an UPCOMING exhibition can receive proposals.</summary>
    [Fact]
    public async Task GetDetails_Upcoming_OwnEntriesAndCanPropose()
    {
        using var db = CreateDb();
        var e = Add(db, "Soon", 5, 10);
        AddEntry(db, e, Me, "Mine");
        AddEntry(db, e, Other, "Theirs");

        var vm = await new StudentExhibitionService(db).GetDetailsAsync(e.Id, Me);

        Assert.Equal(ExhibitionStatus.UPCOMING, vm.Status);
        Assert.True(vm.CanPropose);
        Assert.Equal("Mine", Assert.Single(vm.MyEntries).ArtworkTitle);
    }

    /// <summary>BR-42: once the exhibition has started no artwork can be proposed.</summary>
    [Fact]
    public async Task GetDetails_Ongoing_CannotPropose()
    {
        using var db = CreateDb();
        var e = Add(db, "Running", -1, 3);
        var vm = await new StudentExhibitionService(db).GetDetailsAsync(e.Id, Me);
        Assert.Equal(ExhibitionStatus.ONGOING, vm.Status);
        Assert.False(vm.CanPropose);
    }

    /// <summary>Unknown id → NotFoundException (404).</summary>
    [Fact]
    public async Task GetDetails_Unknown_NotFound()
    {
        using var db = CreateDb();
        await Assert.ThrowsAsync<NotFoundException>(() => new StudentExhibitionService(db).GetDetailsAsync(999, Me));
    }
}
