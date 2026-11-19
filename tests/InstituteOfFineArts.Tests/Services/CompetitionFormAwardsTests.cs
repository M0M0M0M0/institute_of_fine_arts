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
/// Awards entered on the competition create / edit form (CompetitionFormVm.Awards): saved with the competition, empty
/// rows ignored, the rules of the award form (name, rank, prize, quantity, unique name) applied per row with the error
/// on that row, and on edit: add / change / remove, an award already given cannot be removed or go below its count.
/// </summary>
public class CompetitionFormAwardsTests
{
    private static readonly DateOnly Today = DateTimeHelper.Today;
    private long _staffId;

    private AppDbContext CreateDb()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var staff = new StaffProfile
        {
            StaffCode = "NV0003", JoinedDate = new DateOnly(2024, 1, 1),
            Account = new Account { Username = "staff01", FullName = "Le Minh Hoang", Email = "s@ifa.local", Role = Role.STAFF, PasswordHash = "x" },
        };
        db.StaffProfiles.Add(staff);
        db.SaveChanges();
        _staffId = staff.Id;
        return db;
    }

    private static CompetitionService Service(AppDbContext db)
        => new(db, new Mock<IFileStorageService>().Object, NullLogger<CompetitionService>.Instance, new Mock<INotificationService>().Object);

    private static CompetitionAwardRowVm Row(string? name, decimal? prize, int rank = 1, int quantity = 1, long id = 0, bool remove = false)
        => new() { Id = id, AwardName = name, PrizeValue = prize, Rank = rank, Quantity = quantity, Remove = remove };

    private static CompetitionFormVm Form(params CompetitionAwardRowVm[] awards) => new()
    {
        Title = "Lunar New Year 2027", Description = "Paint the festival",
        StartDate = Today.AddDays(3), EndDate = Today.AddDays(20), Awards = awards.ToList(),
    };

    // ------------------------------------------------------------------ Create

    [Fact]
    public async Task Create_SavesAwardRows_IgnoresBlankRows()
    {
        using var db = CreateDb();

        var id = await Service(db).CreateAsync(Form(
            Row("First Prize", 500m, rank: 1),
            Row(null, null, rank: 2),                       // blank new row → skipped
            Row(" Second Prize ", 250.5m, rank: 2, quantity: 2)), _staffId);

        var awards = await db.CompetitionAwards.Where(a => a.CompetitionId == id).OrderBy(a => a.Rank).ToListAsync();
        Assert.Equal(new[] { "First Prize", "Second Prize" }, awards.Select(a => a.AwardName));
        Assert.Equal(250.5m, awards[1].PrizeValue);
        Assert.Equal(2, awards[1].Quantity);
    }

    [Fact]
    public async Task Create_InvalidRow_ErrorOnThatRow_NothingSaved()
    {
        using var db = CreateDb();

        var ex = await Assert.ThrowsAsync<BusinessException>(() => Service(db).CreateAsync(Form(
            Row("First Prize", 500m),
            Row("Second Prize", null, rank: 2)), _staffId));   // name typed, no prize

        Assert.Equal("Awards[1].PrizeValue", ex.Field);
        Assert.Empty(db.Competitions);
        Assert.Empty(db.CompetitionAwards);
    }

    [Fact]
    public async Task Create_DuplicateNames_Refused()
    {
        using var db = CreateDb();

        var ex = await Assert.ThrowsAsync<BusinessException>(() => Service(db).CreateAsync(Form(
            Row("First Prize", 500m), Row("first prize", 100m, rank: 2)), _staffId));

        Assert.Equal("Awards[1].AwardName", ex.Field);
        Assert.Equal(CompetitionAwardService.DuplicateNameMessage, ex.Message);
    }

    [Fact]
    public async Task Create_NoAwards_StillCreated()
    {
        using var db = CreateDb();

        var id = await Service(db).CreateAsync(Form(Row(null, null)), _staffId);

        Assert.True(await db.Competitions.AnyAsync(c => c.Id == id));
        Assert.Empty(db.CompetitionAwards);
    }

    // ------------------------------------------------------------------ Edit

    /// <summary>An upcoming competition with First (given once) and Second prize, plus a Third prize left off the form.</summary>
    private async Task<(Competition C, CompetitionAward First, CompetitionAward Second, CompetitionAward Third)> SeedAsync(AppDbContext db)
    {
        var c = new Competition
        {
            Code = "CT2027-01", Title = "Lunar New Year 2027", StartDate = Today.AddDays(3), EndDate = Today.AddDays(20),
            Status = CompetitionStatus.UPCOMING, CreatedBy = _staffId,
        };
        var first = new CompetitionAward { AwardName = "First Prize", Rank = 1, PrizeValue = 500m, Quantity = 1 };
        var second = new CompetitionAward { AwardName = "Second Prize", Rank = 2, PrizeValue = 250m, Quantity = 2 };
        var third = new CompetitionAward { AwardName = "Third Prize", Rank = 3, PrizeValue = 100m, Quantity = 3 };
        c.Awards.Add(first);
        c.Awards.Add(second);
        c.Awards.Add(third);
        db.Competitions.Add(c);
        await db.SaveChangesAsync();
        var submission = new Submission { CompetitionId = c.Id, StudentId = 1, Title = "Entry", ImagePath = "p.jpg", OriginalFileName = "p.jpg", TextContent = "t", Description = "d" };
        db.Submissions.Add(submission);
        await db.SaveChangesAsync();
        db.AwardsIssued.Add(new AwardIssued { CompetitionAwardId = first.Id, SubmissionId = submission.Id, IssuedBy = _staffId, IssuedDate = Today });
        await db.SaveChangesAsync();
        return (c, first, second, third);
    }

    [Fact]
    public async Task Edit_AwardRowsCarryTheGivenCount()
    {
        using var db = CreateDb();
        var (c, _, _, _) = await SeedAsync(db);

        var form = await Service(db).GetForEditAsync(c.Id);

        Assert.Equal(new[] { "First Prize", "Second Prize", "Third Prize" }, form.Awards.Select(a => a.AwardName));
        Assert.Equal(new[] { 1, 0, 0 }, form.Awards.Select(a => a.IssuedCount));
    }

    [Fact]
    public async Task Update_AddsChangesRemoves_LeavesAwardsNotOnTheForm()
    {
        using var db = CreateDb();
        var (c, first, second, third) = await SeedAsync(db);
        // The competition has an entry (BR-10): keep its title and start date
        var form = Form(
            Row("First Prize", 600m, rank: 1, id: first.Id),          // changed prize
            Row("Second Prize", 250m, rank: 2, id: second.Id, remove: true),
            Row("Honourable Mention", 0m, rank: 4, quantity: 5));     // new
        form.Title = c.Title;
        form.StartDate = c.StartDate;

        await Service(db).UpdateAsync(c.Id, form);

        var awards = await db.CompetitionAwards.AsNoTracking().Where(a => a.CompetitionId == c.Id).OrderBy(a => a.Rank).ToListAsync();
        Assert.Equal(new[] { "First Prize", "Third Prize", "Honourable Mention" }, awards.Select(a => a.AwardName));
        Assert.Equal(600m, awards[0].PrizeValue);
        Assert.Equal(third.Id, awards[1].Id); // not on the form → untouched
    }

    [Fact]
    public async Task Update_RemoveGivenAward_Refused()
    {
        using var db = CreateDb();
        var (c, first, _, _) = await SeedAsync(db);
        var form = Form(Row("First Prize", 500m, id: first.Id, remove: true));
        form.Title = c.Title;
        form.StartDate = c.StartDate;

        var ex = await Assert.ThrowsAsync<BusinessException>(() => Service(db).UpdateAsync(c.Id, form));

        Assert.Equal(CompetitionAwardService.IssuedDeleteMessage, ex.Message);
        Assert.Equal("Awards[0].AwardName", ex.Field);
    }

    [Fact]
    public async Task Update_InvalidQuantity_ErrorOnThatRow()
    {
        using var db = CreateDb();
        var (c, first, _, _) = await SeedAsync(db);
        var form = Form(Row("First Prize", 500m, quantity: 0, id: first.Id));
        form.Title = c.Title;
        form.StartDate = c.StartDate;

        // 0 is refused by the award rules first (quantity ≥ 1)
        var ex = await Assert.ThrowsAsync<BusinessException>(() => Service(db).UpdateAsync(c.Id, form));
        Assert.Equal("Awards[0].Quantity", ex.Field);
    }

    [Fact]
    public async Task Update_DuplicateWithAwardNotOnTheForm_Refused()
    {
        using var db = CreateDb();
        var (c, _, _, _) = await SeedAsync(db);
        var form = Form(Row("Third Prize", 50m, rank: 5));
        form.Title = c.Title;
        form.StartDate = c.StartDate;

        var ex = await Assert.ThrowsAsync<BusinessException>(() => Service(db).UpdateAsync(c.Id, form));

        Assert.Equal(CompetitionAwardService.DuplicateNameMessage, ex.Message);
    }

    [Fact]
    public async Task Update_AwardOfAnotherCompetition_Refused()
    {
        using var db = CreateDb();
        var (c, _, _, _) = await SeedAsync(db);
        var other = new Competition { Code = "CT2027-02", Title = "Other", StartDate = Today.AddDays(3), EndDate = Today.AddDays(9), CreatedBy = _staffId };
        var foreign = new CompetitionAward { AwardName = "Foreign", Rank = 1, PrizeValue = 1m, Quantity = 1 };
        other.Awards.Add(foreign);
        db.Competitions.Add(other);
        await db.SaveChangesAsync();
        var form = Form(Row("Foreign", 1m, id: foreign.Id, remove: true));
        form.Title = c.Title;
        form.StartDate = c.StartDate;

        await Assert.ThrowsAsync<BusinessException>(() => Service(db).UpdateAsync(c.Id, form));
        Assert.True(await db.CompetitionAwards.AnyAsync(a => a.Id == foreign.Id));
    }
}
