using InstituteOfFineArts.Web.Data;
using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.Services.Implementations;
using InstituteOfFineArts.Web.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace InstituteOfFineArts.Tests.Services;

/// <summary>
/// StatusSyncService (task BE2-05, CN37, BR-07, BR-42): stored statuses follow the dates, GRADED is kept,
/// accepted entries of finished exhibitions become UNSOLD, and a second run changes nothing.
/// </summary>
public class StatusSyncServiceTests
{
    /// <summary>Fixed "today" used by every test.</summary>
    private static readonly DateOnly Today = new(2026, 6, 15);

    /// <summary>Empty in-memory database.</summary>
    private static AppDbContext CreateDb()
        => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    /// <summary>Service under test.</summary>
    private static StatusSyncService Service(AppDbContext db) => new(db, NullLogger<StatusSyncService>.Instance);

    /// <summary>Competition with the given dates (relative to <see cref="Today"/>) and stored status.</summary>
    private static Competition Competition(string code, int startOffset, int endOffset, CompetitionStatus stored) => new()
    {
        Code = code, Title = code, StartDate = Today.AddDays(startOffset), EndDate = Today.AddDays(endOffset), Status = stored, CreatedBy = 1,
    };

    /// <summary>Exhibition with the given dates (relative to <see cref="Today"/>) and stored status.</summary>
    private static Exhibition Exhibition(string name, int startOffset, int endOffset, ExhibitionStatus stored) => new()
    {
        Name = name, Venue = "Hall A", City = "Ho Chi Minh City", StartDate = Today.AddDays(startOffset), EndDate = Today.AddDays(endOffset),
        Status = stored, CreatedBy = 1,
    };

    /// <summary>Entry of an exhibition with the given status.</summary>
    private static ExhibitionEntry Entry(Exhibition exhibition, long submissionId, EntryStatus status) => new()
    {
        Exhibition = exhibition, SubmissionId = submissionId, AskingPrice = 100m, Status = status, ProposedBy = 1,
    };

    /// <summary>BR-07: stale UPCOMING competitions become ONGOING or CLOSED; correct ones are not counted.</summary>
    [Fact]
    public async Task Sync_Competitions_FollowDates()
    {
        using var db = CreateDb();
        db.Competitions.AddRange(
            Competition("C-ONGOING", -5, 5, CompetitionStatus.UPCOMING),
            Competition("C-CLOSED", -20, -1, CompetitionStatus.UPCOMING),
            Competition("C-ENDS-TODAY", -10, 0, CompetitionStatus.ONGOING),
            Competition("C-FUTURE", 1, 10, CompetitionStatus.CLOSED),
            Competition("C-OK", 3, 10, CompetitionStatus.UPCOMING));
        db.SaveChanges();

        var result = await Service(db).SyncAsync(Today);

        Assert.Equal(3, result.Competitions);
        var status = db.Competitions.ToDictionary(c => c.Code, c => c.Status);
        Assert.Equal(CompetitionStatus.ONGOING, status["C-ONGOING"]);
        Assert.Equal(CompetitionStatus.CLOSED, status["C-CLOSED"]);
        Assert.Equal(CompetitionStatus.ONGOING, status["C-ENDS-TODAY"]);
        Assert.Equal(CompetitionStatus.UPCOMING, status["C-FUTURE"]);
        Assert.Equal(CompetitionStatus.UPCOMING, status["C-OK"]);
    }

    /// <summary>BR-07: GRADED competitions are never changed, whatever their dates.</summary>
    [Fact]
    public async Task Sync_Graded_Untouched()
    {
        using var db = CreateDb();
        db.Competitions.AddRange(
            Competition("C-GRADED-PAST", -30, -10, CompetitionStatus.GRADED),
            Competition("C-GRADED-FUTURE", 5, 10, CompetitionStatus.GRADED));
        db.SaveChanges();

        var result = await Service(db).SyncAsync(Today);

        Assert.Equal(0, result.Competitions);
        Assert.All(db.Competitions, c => Assert.Equal(CompetitionStatus.GRADED, c.Status));
    }

    /// <summary>BR-42: exhibition statuses follow the dates (UPCOMING, ONGOING, FINISHED).</summary>
    [Fact]
    public async Task Sync_Exhibitions_FollowDates()
    {
        using var db = CreateDb();
        db.Exhibitions.AddRange(
            Exhibition("X-UPCOMING", 2, 9, ExhibitionStatus.ONGOING),
            Exhibition("X-ONGOING", -2, 2, ExhibitionStatus.UPCOMING),
            Exhibition("X-FINISHED", -9, -2, ExhibitionStatus.ONGOING),
            Exhibition("X-OK", -9, -2, ExhibitionStatus.FINISHED));
        db.SaveChanges();

        var result = await Service(db).SyncAsync(Today);

        Assert.Equal(3, result.Exhibitions);
        var status = db.Exhibitions.ToDictionary(x => x.Name, x => x.Status);
        Assert.Equal(ExhibitionStatus.UPCOMING, status["X-UPCOMING"]);
        Assert.Equal(ExhibitionStatus.ONGOING, status["X-ONGOING"]);
        Assert.Equal(ExhibitionStatus.FINISHED, status["X-FINISHED"]);
        Assert.Equal(ExhibitionStatus.FINISHED, status["X-OK"]);
    }

    /// <summary>
    /// BR-42: ACCEPTED entries become UNSOLD only when the exhibition is FINISHED;
    /// PENDING, REJECTED, SOLD and UNSOLD entries keep their status, and running exhibitions are not touched.
    /// </summary>
    [Fact]
    public async Task Sync_FinishedExhibition_AcceptedBecomeUnsold()
    {
        using var db = CreateDb();
        var finished = Exhibition("X-FINISHED", -9, -1, ExhibitionStatus.ONGOING);
        var ongoing = Exhibition("X-ONGOING", -1, 5, ExhibitionStatus.ONGOING);
        db.ExhibitionEntries.AddRange(
            Entry(finished, 1, EntryStatus.ACCEPTED),
            Entry(finished, 2, EntryStatus.ACCEPTED),
            Entry(finished, 3, EntryStatus.PENDING),
            Entry(finished, 4, EntryStatus.REJECTED),
            Entry(finished, 5, EntryStatus.SOLD),
            Entry(finished, 6, EntryStatus.UNSOLD),
            Entry(ongoing, 7, EntryStatus.ACCEPTED));
        db.SaveChanges();

        var result = await Service(db).SyncAsync(Today);

        Assert.Equal(2, result.EntriesUnsold);
        var status = db.ExhibitionEntries.ToDictionary(e => e.SubmissionId, e => e.Status);
        Assert.Equal(EntryStatus.UNSOLD, status[1]);
        Assert.Equal(EntryStatus.UNSOLD, status[2]);
        Assert.Equal(EntryStatus.PENDING, status[3]);
        Assert.Equal(EntryStatus.REJECTED, status[4]);
        Assert.Equal(EntryStatus.SOLD, status[5]);
        Assert.Equal(EntryStatus.UNSOLD, status[6]);
        Assert.Equal(EntryStatus.ACCEPTED, status[7]);
    }

    /// <summary>CN37: the job is idempotent – a second run on the same day changes nothing.</summary>
    [Fact]
    public async Task Sync_RunTwice_SecondRunChangesNothing()
    {
        using var db = CreateDb();
        db.Competitions.Add(Competition("C-STALE", -5, -1, CompetitionStatus.UPCOMING));
        var finished = Exhibition("X-STALE", -9, -1, ExhibitionStatus.UPCOMING);
        db.ExhibitionEntries.Add(Entry(finished, 1, EntryStatus.ACCEPTED));
        db.SaveChanges();
        var service = Service(db);

        var first = await service.SyncAsync(Today);
        var second = await service.SyncAsync(Today);

        Assert.Equal(new StatusSyncResult(1, 1, 1), first);
        Assert.Equal(new StatusSyncResult(0, 0, 0), second);
        Assert.Equal(0, second.Total);
    }
}
