using InstituteOfFineArts.Web.Data;
using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Helpers;
using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.Services.Implementations;
using InstituteOfFineArts.Web.ViewModels.Staff.Competitions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace InstituteOfFineArts.Tests.Services;

/// <summary>
/// CompetitionAwardService (task BE1-06, F-M05-06): add / edit / delete awards, BR-09 values, unique name per
/// competition, GRADED read only, issued awards protected (BR-31), award of another competition → not found.
/// </summary>
public class CompetitionAwardServiceTests
{
    /// <summary>Today in the institute time zone.</summary>
    private static readonly DateOnly Today = DateTimeHelper.Today;

    /// <summary>staff_profile.id in the test database.</summary>
    private long _staffId;

    /// <summary>Empty in-memory database with one staff member.</summary>
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

    /// <summary>Service under test.</summary>
    private static CompetitionAwardService Service(AppDbContext db) => new(db, NullLogger<CompetitionAwardService>.Instance);

    /// <summary>Adds an upcoming competition (or one with the given stored status) with a "First Prize" award.</summary>
    private Competition AddCompetition(AppDbContext db, CompetitionStatus status = CompetitionStatus.UPCOMING)
    {
        var c = new Competition
        {
            Code = "X" + Guid.NewGuid().ToString("N")[..8], Title = "Lunar New Year 2027",
            StartDate = Today.AddDays(5), EndDate = Today.AddDays(30), Status = status, CreatedBy = _staffId,
        };
        c.Awards.Add(new CompetitionAward { AwardName = "First Prize", Rank = 1, PrizeValue = 300m, Quantity = 1 });
        db.Competitions.Add(c);
        db.SaveChanges();
        return c;
    }

    /// <summary>Gives the award of <paramref name="award"/> to a new submission.</summary>
    private void Issue(AppDbContext db, Competition c, CompetitionAward award)
    {
        var s = new Submission { CompetitionId = c.Id, StudentId = 1, Title = "Entry", ImagePath = "p.jpg", OriginalFileName = "p.jpg", TextContent = "t", Description = "d" };
        db.Submissions.Add(s);
        db.SaveChanges();
        db.AwardsIssued.Add(new AwardIssued { CompetitionAwardId = award.Id, SubmissionId = s.Id, IssuedBy = _staffId, IssuedDate = Today });
        db.SaveChanges();
    }

    /// <summary>Valid form.</summary>
    private static CompetitionAwardFormVm Form(string name = "Second Prize", int? rank = 2, decimal? prize = 200m, int? quantity = 1) => new()
    {
        AwardName = name, Rank = rank, PrizeValue = prize, Quantity = quantity, PrizeDescription = "  Certificate  ",
    };

    /// <summary>Valid award → saved with trimmed text; the add form suggests the next rank.</summary>
    [Fact]
    public async Task Create_SavesAward()
    {
        using var db = CreateDb();
        var c = AddCompetition(db);
        var service = Service(db);

        var blank = await service.GetForCreateAsync(c.Id);
        Assert.Equal(2, blank.Rank);
        Assert.Equal(c.Title, blank.CompetitionTitle);

        var id = await service.CreateAsync(c.Id, Form(name: "  Second Prize "));
        var saved = await db.CompetitionAwards.SingleAsync(a => a.Id == id);
        Assert.Equal(c.Id, saved.CompetitionId);
        Assert.Equal("Second Prize", saved.AwardName);
        Assert.Equal(200m, saved.PrizeValue);
        Assert.Equal("Certificate", saved.PrizeDescription);
    }

    /// <summary>Same name in the same competition (any case) → refused on the name field; another competition is fine.</summary>
    [Fact]
    public async Task Create_DuplicateName_Refused()
    {
        using var db = CreateDb();
        var c = AddCompetition(db);
        var other = AddCompetition(db);
        var service = Service(db);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => service.CreateAsync(c.Id, Form(name: "first PRIZE")));
        Assert.Equal(nameof(CompetitionAwardFormVm.AwardName), ex.Field);
        Assert.Equal(CompetitionAwardService.DuplicateNameMessage, ex.Message);

        // The name is only unique inside one competition
        await service.CreateAsync(other.Id, Form(name: "Honorable Mention"));
        Assert.Equal(2, await db.CompetitionAwards.CountAsync(a => a.CompetitionId == other.Id));
    }

    /// <summary>Rank &lt; 1, negative prize, more than 2 decimals, quantity &lt; 1, blank name → refused with the field.</summary>
    [Theory]
    [InlineData("Second Prize", 0, 200, 1, nameof(CompetitionAwardFormVm.Rank))]
    [InlineData("Second Prize", 2, -1, 1, nameof(CompetitionAwardFormVm.PrizeValue))]
    [InlineData("Second Prize", 2, 10.555, 1, nameof(CompetitionAwardFormVm.PrizeValue))]
    [InlineData("Second Prize", 2, 200, 0, nameof(CompetitionAwardFormVm.Quantity))]
    [InlineData("   ", 2, 200, 1, nameof(CompetitionAwardFormVm.AwardName))]
    public async Task Create_InvalidValues_Refused(string name, int rank, double prize, int quantity, string field)
    {
        using var db = CreateDb();
        var c = AddCompetition(db);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => Service(db).CreateAsync(c.Id, Form(name, rank, (decimal)prize, quantity)));
        Assert.Equal(field, ex.Field);
        Assert.Equal(1, await db.CompetitionAwards.CountAsync());
    }

    /// <summary>Prize 0 is allowed (certificate only).</summary>
    [Fact]
    public async Task Create_ZeroPrize_Allowed()
    {
        using var db = CreateDb();
        var c = AddCompetition(db);
        var id = await Service(db).CreateAsync(c.Id, Form(prize: 0m));
        Assert.Equal(0m, (await db.CompetitionAwards.FindAsync(id))!.PrizeValue);
    }

    /// <summary>GRADED competition → add, edit form, save and delete are all refused.</summary>
    [Fact]
    public async Task Graded_IsReadOnly()
    {
        using var db = CreateDb();
        var c = AddCompetition(db, CompetitionStatus.GRADED);
        var awardId = c.Awards.Single().Id;
        var service = Service(db);

        await Assert.ThrowsAsync<BusinessException>(() => service.GetForCreateAsync(c.Id));
        await Assert.ThrowsAsync<BusinessException>(() => service.CreateAsync(c.Id, Form()));
        await Assert.ThrowsAsync<BusinessException>(() => service.GetForEditAsync(c.Id, awardId));
        await Assert.ThrowsAsync<BusinessException>(() => service.UpdateAsync(c.Id, awardId, Form(name: "First Prize", rank: 1)));
        var ex = await Assert.ThrowsAsync<BusinessException>(() => service.DeleteAsync(c.Id, awardId));
        Assert.Equal(CompetitionAwardService.GradedReadOnlyMessage, ex.Message);
        Assert.Equal(1, await db.CompetitionAwards.CountAsync());
    }

    /// <summary>Edit saves the values; keeping its own name is not a duplicate; another award's name is.</summary>
    [Fact]
    public async Task Update_SavesAndChecksName()
    {
        using var db = CreateDb();
        var c = AddCompetition(db);
        var service = Service(db);
        var firstId = c.Awards.Single().Id;
        var secondId = await service.CreateAsync(c.Id, Form());

        // Same name (other case) on the same award → allowed
        await service.UpdateAsync(c.Id, firstId, Form(name: "FIRST PRIZE", rank: 1, prize: 350.50m, quantity: 2));
        var first = await db.CompetitionAwards.AsNoTracking().SingleAsync(a => a.Id == firstId);
        Assert.Equal("FIRST PRIZE", first.AwardName);
        Assert.Equal(350.50m, first.PrizeValue);
        Assert.Equal(2, first.Quantity);

        // Name of the other award → refused
        var ex = await Assert.ThrowsAsync<BusinessException>(() => service.UpdateAsync(c.Id, secondId, Form(name: "first prize")));
        Assert.Equal(nameof(CompetitionAwardFormVm.AwardName), ex.Field);
    }

    /// <summary>Issued award: cannot be deleted, quantity cannot go below the issued count, other edits allowed.</summary>
    [Fact]
    public async Task IssuedAward_IsProtected()
    {
        using var db = CreateDb();
        var c = AddCompetition(db);
        var award = c.Awards.Single();
        award.Quantity = 2;
        db.SaveChanges();
        Issue(db, c, award);
        Issue(db, c, award);
        var service = Service(db);

        var del = await Assert.ThrowsAsync<BusinessException>(() => service.DeleteAsync(c.Id, award.Id));
        Assert.Equal(CompetitionAwardService.IssuedDeleteMessage, del.Message);

        var qty = await Assert.ThrowsAsync<BusinessException>(() => service.UpdateAsync(c.Id, award.Id, Form(name: "First Prize", rank: 1, quantity: 1)));
        Assert.Equal(nameof(CompetitionAwardFormVm.Quantity), qty.Field);

        // Same quantity as issued, new description → fine
        await service.UpdateAsync(c.Id, award.Id, Form(name: "First Prize", rank: 1, quantity: 2));
        Assert.Equal(2, (await service.GetForEditAsync(c.Id, award.Id)).IssuedCount);
        Assert.Equal(1, await db.CompetitionAwards.CountAsync());
    }

    /// <summary>Award never issued → deleted.</summary>
    [Fact]
    public async Task Delete_RemovesAward()
    {
        using var db = CreateDb();
        var c = AddCompetition(db);
        await Service(db).DeleteAsync(c.Id, c.Awards.Single().Id);
        Assert.Equal(0, await db.CompetitionAwards.CountAsync());
    }

    /// <summary>An award id of another competition, an unknown award or an unknown competition → NotFoundException.</summary>
    [Fact]
    public async Task AwardOfAnotherCompetition_NotFound()
    {
        using var db = CreateDb();
        var c = AddCompetition(db);
        var other = AddCompetition(db);
        var otherAwardId = other.Awards.Single().Id;
        var service = Service(db);

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetForEditAsync(c.Id, otherAwardId));
        await Assert.ThrowsAsync<NotFoundException>(() => service.UpdateAsync(c.Id, otherAwardId, Form()));
        await Assert.ThrowsAsync<NotFoundException>(() => service.DeleteAsync(c.Id, otherAwardId));
        await Assert.ThrowsAsync<NotFoundException>(() => service.DeleteAsync(c.Id, 999_999));
        await Assert.ThrowsAsync<NotFoundException>(() => service.CreateAsync(999_999, Form()));
        Assert.Equal(2, await db.CompetitionAwards.CountAsync());
    }
}
