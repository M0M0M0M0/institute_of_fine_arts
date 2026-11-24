using InstituteOfFineArts.Web.Data;
using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Helpers;
using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.Services.Implementations;
using InstituteOfFineArts.Web.Services.Interfaces;
using InstituteOfFineArts.Web.ViewModels.Staff.Competitions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace InstituteOfFineArts.Tests.Services;

/// <summary>
/// Timeline of a competition (step 7, BR-29): the system dates merged with the staff's milestones in date order, the
/// current line, and the milestone rows of the competition form (checked, added, changed, removed, logged).
/// </summary>
public class CompetitionTimelineTests
{
    private static readonly DateOnly Today = DateTimeHelper.Today;

    private static CompetitionMilestone M(int days, string title, int order = 0) => new() { Date = Today.AddDays(days), Title = title, SortOrder = order };

    // ------------------------------------------------------------------ builder

    /// <summary>Open, close and the milestones in date order; the current line is the last one on or before today.</summary>
    [Fact]
    public void Build_OrdersAndMarksCurrent()
    {
        var items = CompetitionTimeline.Build(Today.AddDays(-5), Today.AddDays(5), null,
            [M(12, "Award ceremony"), M(-20, "Competition announced"), M(0, "Half way")], Today);

        Assert.Equal(["Competition announced", CompetitionTimeline.OpenTitle, "Half way", CompetitionTimeline.CloseTitle, "Award ceremony"],
            items.Select(i => i.Title));
        Assert.Equal([MilestoneState.Past, MilestoneState.Past, MilestoneState.Current, MilestoneState.Upcoming, MilestoneState.Upcoming],
            items.Select(i => i.State));
        Assert.True(items[1].IsSystem);
        Assert.False(items[0].IsSystem);
    }

    /// <summary>A milestone on the last day comes before "Submissions close" (the deadline is the end of the day).</summary>
    [Fact]
    public void Build_SameDayAsDeadline_BeforeClose()
    {
        var items = CompetitionTimeline.Build(Today.AddDays(-10), Today.AddDays(-1), null, [M(-1, "Last workshop")], Today);
        Assert.Equal(["Last workshop", CompetitionTimeline.CloseTitle], items.Skip(1).Select(i => i.Title));
        Assert.Equal(MilestoneState.Current, items[^1].State);
    }

    /// <summary>Published results appear on their day; nothing is current before the first date.</summary>
    [Fact]
    public void Build_PublishedAndNotStarted()
    {
        var published = CompetitionTimeline.Build(Today.AddDays(-30), Today.AddDays(-10), Today.AddDays(-3).ToDateTime(new TimeOnly(9, 0)), [], Today);
        Assert.Equal(CompetitionTimeline.PublishedTitle, published[^1].Title);
        Assert.Equal(MilestoneState.Current, published[^1].State);

        var upcoming = CompetitionTimeline.Build(Today.AddDays(3), Today.AddDays(20), null, [], Today);
        Assert.All(upcoming, i => Assert.Equal(MilestoneState.Upcoming, i.State));
    }

    // ------------------------------------------------------------------ form rows

    private long _staffId;

    private AppDbContext CreateDb()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var staff = new StaffProfile
        {
            StaffCode = "NV0001", JoinedDate = new DateOnly(2024, 1, 1),
            Account = new Account { Username = "staff01", FullName = "Le Minh Hoang", Email = "s@ifa.local", Role = Role.STAFF, PasswordHash = "x" },
        };
        // A teacher too: saving the form reads the teachers' names (jury), which must work on a fresh context
        var teacher = new StaffProfile
        {
            StaffCode = "NV0003", JoinedDate = new DateOnly(2024, 1, 1),
            Account = new Account { Username = "teacher01", FullName = "Pham Thu Ha", Email = "t@ifa.local", Role = Role.TEACHER, PasswordHash = "x" },
        };
        db.StaffProfiles.AddRange(staff, teacher);
        db.SaveChanges();
        db.ChangeTracker.Clear();
        _staffId = staff.Id;
        return db;
    }

    private static CompetitionService Service(AppDbContext db)
        => new(db, new Mock<IFileStorageService>().Object, NullLogger<CompetitionService>.Instance, new Mock<INotificationService>().Object);

    private static CompetitionFormVm Form(params CompetitionMilestoneRowVm[] milestones) => new()
    {
        Title = "Lunar New Year 2027", Description = "Paint the festival", Rules = "One entry per student",
        StartDate = Today.AddDays(3), EndDate = Today.AddDays(20), Milestones = milestones.ToList(),
    };

    private static CompetitionMilestoneRowVm Row(int days, string? title, string? content = null, long id = 0)
        => new() { Id = id, Date = Today.AddDays(days), Title = title, Content = content };

    /// <summary>Create: filled rows are saved (trimmed), blank rows skipped; the timeline shows them.</summary>
    [Fact]
    public async Task Create_SavesMilestones()
    {
        using var db = CreateDb();
        var id = await Service(db).CreateAsync(Form(Row(-7, "  Competition announced  ", " On the website "), new CompetitionMilestoneRowVm(), Row(27, "Award ceremony")), _staffId);

        var saved = await db.CompetitionMilestones.Where(m => m.CompetitionId == id).OrderBy(m => m.Date).ToListAsync();
        Assert.Equal(["Competition announced", "Award ceremony"], saved.Select(m => m.Title));
        Assert.Equal("On the website", saved[0].Content);
        var c = await db.Competitions.Include(x => x.Milestones).SingleAsync();
        Assert.Equal(4, CompetitionTimeline.Of(c, Today).Count);
    }

    /// <summary>A row needs a date and a title, and the date must be within a year of the competition.</summary>
    [Theory]
    [InlineData(null, "Ceremony", CompetitionService.MilestoneDateRequiredMessage, "Date")]
    [InlineData(5, "  ", CompetitionService.MilestoneTitleRequiredMessage, "Title")]
    [InlineData(500, "Far away", CompetitionService.MilestoneDateRangeMessage, "Date")]
    public async Task Create_BadRow_Refused(int? days, string title, string message, string field)
    {
        using var db = CreateDb();
        var row = new CompetitionMilestoneRowVm { Date = days is { } d ? Today.AddDays(d) : null, Title = title };

        var ex = await Assert.ThrowsAsync<BusinessException>(() => Service(db).CreateAsync(Form(row), _staffId));

        Assert.Equal(message, ex.Message);
        Assert.Equal($"Milestones[0].{field}", ex.Field);
        Assert.Empty(db.Competitions);
    }

    /// <summary>At most 12 milestones.</summary>
    [Fact]
    public async Task Create_TooMany_Refused()
    {
        using var db = CreateDb();
        var rows = Enumerable.Range(0, CompetitionService.MaxMilestones + 1).Select(i => Row(i, $"Day {i}")).ToArray();
        var ex = await Assert.ThrowsAsync<BusinessException>(() => Service(db).CreateAsync(Form(rows), _staffId));
        Assert.Equal(CompetitionService.TooManyMilestonesMessage, ex.Message);
    }

    /// <summary>Edit: rows are loaded in date order; a change, an addition and a removal are saved and listed for the history.</summary>
    [Fact]
    public async Task Update_AddsChangesRemoves()
    {
        using var db = CreateDb();
        var service = Service(db);
        var id = await service.CreateAsync(Form(Row(27, "Award ceremony"), Row(-7, "Competition announced")), _staffId);
        // Like a new request: nothing already loaded (the teachers' accounts are not in memory)
        db.ChangeTracker.Clear();

        var form = await service.GetForEditAsync(id);
        Assert.Equal(["Competition announced", "Award ceremony"], form.Milestones.Select(m => m.Title));
        form.Milestones[0].Remove = true;
        form.Milestones[1].Date = Today.AddDays(30);
        form.Milestones.Add(Row(25, "Results expected"));
        var changes = await service.UpdateAsync(id, form);
        await db.SaveChangesAsync();

        Assert.Equal(["Results expected", "Award ceremony"],
            await db.CompetitionMilestones.OrderBy(m => m.Date).Select(m => m.Title).ToListAsync());
        Assert.Contains(changes, c => c.StartsWith("Milestone removed:") && c.Contains("Competition announced"));
        Assert.Contains(changes, c => c.StartsWith("Milestone changed:") && c.Contains("Award ceremony"));
        Assert.Contains(changes, c => c.StartsWith("Milestone added:") && c.Contains("Results expected"));
    }

    /// <summary>A row with the id of another competition's milestone is refused.</summary>
    [Fact]
    public async Task Update_ForeignMilestone_Refused()
    {
        using var db = CreateDb();
        var service = Service(db);
        var other = await service.CreateAsync(Form(Row(5, "Other")), _staffId);
        var id = await service.CreateAsync(Form(), _staffId);
        var foreign = await db.CompetitionMilestones.SingleAsync(m => m.CompetitionId == other);

        var form = await service.GetForEditAsync(id);
        form.Milestones.Add(Row(5, "Stolen", id: foreign.Id));
        await Assert.ThrowsAsync<BusinessException>(() => service.UpdateAsync(id, form));
    }
}
