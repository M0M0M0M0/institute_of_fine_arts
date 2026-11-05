using System.ComponentModel.DataAnnotations;
using InstituteOfFineArts.Web.Data;
using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Helpers;
using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.Services.Implementations;
using InstituteOfFineArts.Web.ViewModels.Staff.Exhibitions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace InstituteOfFineArts.Tests.Services;

/// <summary>
/// ExhibitionService (task BE2-09; F-M09-01, F-M09-02): create (TC-M09-01), BR-42 dates / status / delete,
/// search, details counts and the date-edit limits once artworks exist. Dates are relative to today.
/// </summary>
public class ExhibitionServiceTests
{
    /// <summary>Today in the institute time zone.</summary>
    private static readonly DateOnly Today = DateTimeHelper.Today;

    /// <summary>staff_profile.id of a STAFF creator in the test database.</summary>
    private long _staffId;

    /// <summary>staff_profile.id of a MANAGER creator (managers also have a staff_profile).</summary>
    private long _managerId;

    /// <summary>Empty in-memory database with one staff member and one manager.</summary>
    private AppDbContext CreateDb()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var staff = new StaffProfile
        {
            StaffCode = "NV0003", JoinedDate = new DateOnly(2024, 1, 1),
            Account = new Account { Username = "staff01", FullName = "Le Minh Hoang", Email = "s@ifa.local", Role = Role.STAFF, PasswordHash = "x" },
        };
        var manager = new StaffProfile
        {
            StaffCode = "NV0002", JoinedDate = new DateOnly(2024, 1, 1),
            Account = new Account { Username = "manager01", FullName = "Tran Bich Ngoc", Email = "m@ifa.local", Role = Role.MANAGER, PasswordHash = "x" },
        };
        db.StaffProfiles.AddRange(staff, manager);
        db.SaveChanges();
        _staffId = staff.Id;
        _managerId = manager.Id;
        return db;
    }

    /// <summary>Service under test.</summary>
    private static ExhibitionService Service(AppDbContext db) => new(db, NullLogger<ExhibitionService>.Instance);

    /// <summary>Valid form starting in <paramref name="startIn"/> days and lasting <paramref name="days"/> days.</summary>
    private static ExhibitionFormVm Form(string name = "Hanoi Spring Exhibition", int startIn = 10, int days = 7) => new()
    {
        Name = name, Venue = "Vietnam Fine Arts Museum", City = "Hanoi", Organizer = "IFA", Description = "Spring works",
        StartDate = Today.AddDays(startIn), EndDate = Today.AddDays(startIn + days),
    };

    /// <summary>Adds an exhibition directly, with a stale stored status on purpose.</summary>
    private Exhibition Add(AppDbContext db, string name, int startIn, int endIn, string city = "Hanoi")
    {
        var e = new Exhibition
        {
            Name = name, Venue = "Venue", City = city, StartDate = Today.AddDays(startIn), EndDate = Today.AddDays(endIn),
            Status = ExhibitionStatus.UPCOMING, CreatedBy = _staffId,
        };
        db.Exhibitions.Add(e);
        db.SaveChanges();
        return e;
    }

    /// <summary>Adds an entry with its submission to the exhibition.</summary>
    private static ExhibitionEntry AddEntry(AppDbContext db, Exhibition e, EntryStatus status, decimal? soldPrice = null, DateOnly? soldDate = null)
    {
        var entry = new ExhibitionEntry
        {
            ExhibitionId = e.Id, AskingPrice = 100m, Status = status, SoldPrice = soldPrice, SoldDate = soldDate, ProposedBy = 1,
            Submission = new Submission { CompetitionId = 1, StudentId = 1, Title = "Artwork", ImagePath = "p.jpg", OriginalFileName = "p.jpg", TextContent = "t", Description = "d" },
        };
        db.ExhibitionEntries.Add(entry);
        db.SaveChanges();
        return entry;
    }

    // ------------------------------------------------------------------ Create

    /// <summary>TC-M09-01: valid form → saved (trimmed), status from the dates, creator = staff profile.</summary>
    [Fact]
    public async Task Create_Valid()
    {
        using var db = CreateDb();
        var id = await Service(db).CreateAsync(Form("  Hanoi Spring Exhibition  "), _staffId);

        var e = await db.Exhibitions.SingleAsync(x => x.Id == id);
        Assert.Equal("Hanoi Spring Exhibition", e.Name);
        Assert.Equal(ExhibitionStatus.UPCOMING, e.Status);
        Assert.Equal(_staffId, e.CreatedBy);
    }

    /// <summary>A MANAGER can create too: created_by is the manager's staff_profile id (A-14, US-12 AC1).</summary>
    [Fact]
    public async Task Create_ByManager_UsesManagerProfile()
    {
        using var db = CreateDb();
        var id = await Service(db).CreateAsync(Form(startIn: 0), _managerId);

        var e = await db.Exhibitions.SingleAsync(x => x.Id == id);
        Assert.Equal(_managerId, e.CreatedBy);
        Assert.Equal(ExhibitionStatus.ONGOING, e.Status);
    }

    /// <summary>BR-42: start ≤ end (service rule and form rule).</summary>
    [Fact]
    public async Task Create_EndBeforeStart_Rejected()
    {
        using var db = CreateDb();
        var form = Form();
        form.EndDate = form.StartDate!.Value.AddDays(-1);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => Service(db).CreateAsync(form, _staffId));
        Assert.Equal(ExhibitionFormVm.EndBeforeStartMessage, ex.Message);
        Assert.Equal(nameof(ExhibitionFormVm.EndDate), ex.Field);
        var results = new List<ValidationResult>();
        Assert.False(Validator.TryValidateObject(form, new ValidationContext(form), results, true));
        Assert.Contains(results, r => r.ErrorMessage == ExhibitionFormVm.EndBeforeStartMessage);
        Assert.Empty(db.Exhibitions);
    }

    /// <summary>A new exhibition cannot already be over.</summary>
    [Fact]
    public async Task Create_EndInPast_Rejected()
    {
        using var db = CreateDb();
        var ex = await Assert.ThrowsAsync<BusinessException>(() => Service(db).CreateAsync(Form(startIn: -10, days: 5), _staffId));
        Assert.Equal(ExhibitionService.EndInPastMessage, ex.Message);
        Assert.Empty(db.Exhibitions);
    }

    /// <summary>Blank name / venue / city are refused by the service too.</summary>
    [Fact]
    public async Task Create_BlankText_Rejected()
    {
        using var db = CreateDb();
        var form = Form();
        form.Venue = "   ";
        var ex = await Assert.ThrowsAsync<BusinessException>(() => Service(db).CreateAsync(form, _staffId));
        Assert.Equal(ExhibitionService.TextRequiredMessage, ex.Message);
    }

    // ------------------------------------------------------------------ Search

    /// <summary>F-M09-02: filters by name, city, calculated status and date range; status calculated, not stored.</summary>
    [Fact]
    public async Task Search_FiltersAndCalculatedStatus()
    {
        using var db = CreateDb();
        Add(db, "Spring Expo", 5, 10, "Hanoi");
        Add(db, "Summer Expo", -2, 3, "Da Nang");
        Add(db, "Autumn Show", -30, -20, "Hanoi");
        var service = Service(db);

        var all = await service.SearchAsync(new ExhibitionSearchVm());
        Assert.Equal(3, all.TotalCount);
        Assert.Equal("Spring Expo", all.Items[0].Name); // newest start first
        Assert.Equal(ExhibitionStatus.ONGOING, all.Items.Single(x => x.Name == "Summer Expo").Status);
        Assert.Equal(ExhibitionStatus.FINISHED, all.Items.Single(x => x.Name == "Autumn Show").Status);

        Assert.Equal(2, (await service.SearchAsync(new ExhibitionSearchVm { Keyword = "Expo" })).TotalCount);
        Assert.Equal("Summer Expo", (await service.SearchAsync(new ExhibitionSearchVm { City = "Nang" })).Items.Single().Name);
        Assert.Equal("Autumn Show", (await service.SearchAsync(new ExhibitionSearchVm { Status = ExhibitionStatus.FINISHED })).Items.Single().Name);
        Assert.Equal("Spring Expo", (await service.SearchAsync(new ExhibitionSearchVm { Status = ExhibitionStatus.UPCOMING })).Items.Single().Name);
        var range = await service.SearchAsync(new ExhibitionSearchVm { From = Today.AddDays(4), To = Today.AddDays(30) });
        Assert.Equal(new[] { "Spring Expo" }, range.Items.Select(x => x.Name));
    }

    // ------------------------------------------------------------------ Details

    /// <summary>Details: entry counts by status (every status listed), sales total, delete allowed only without entries.</summary>
    [Fact]
    public async Task GetDetails_CountsEntriesByStatus()
    {
        using var db = CreateDb();
        var e = Add(db, "Hanoi", -20, -5);
        AddEntry(db, e, EntryStatus.SOLD, 250m, Today.AddDays(-12));
        AddEntry(db, e, EntryStatus.SOLD, 320m, Today.AddDays(-10));
        AddEntry(db, e, EntryStatus.UNSOLD);

        var vm = await Service(db).GetDetailsAsync(e.Id);
        Assert.Equal(ExhibitionStatus.FINISHED, vm.Status);
        Assert.Equal(Enum.GetValues<EntryStatus>().Length, vm.EntryCounts.Count);
        Assert.Equal(2, vm.EntryCounts.Single(c => c.Status == EntryStatus.SOLD).Count);
        Assert.Equal(1, vm.EntryCounts.Single(c => c.Status == EntryStatus.UNSOLD).Count);
        Assert.Equal(0, vm.EntryCounts.Single(c => c.Status == EntryStatus.PENDING).Count);
        Assert.Equal(3, vm.TotalEntries);
        Assert.Equal(570m, vm.SoldTotal);
        Assert.False(vm.CanDelete);
        Assert.Equal("Le Minh Hoang", vm.CreatedByName);
    }

    /// <summary>Unknown id → NotFoundException (404).</summary>
    [Fact]
    public async Task GetDetails_Unknown_NotFound()
    {
        using var db = CreateDb();
        await Assert.ThrowsAsync<NotFoundException>(() => Service(db).GetDetailsAsync(999));
        await Assert.ThrowsAsync<NotFoundException>(() => Service(db).GetForEditAsync(999));
        await Assert.ThrowsAsync<NotFoundException>(() => Service(db).DeleteAsync(999));
    }

    // ------------------------------------------------------------------ Update

    /// <summary>Without entries every field can change; the stored status is refreshed.</summary>
    [Fact]
    public async Task Update_NoEntries_ChangesEverything()
    {
        using var db = CreateDb();
        var e = Add(db, "Old", 5, 10);
        var form = Form("New name", startIn: -1, days: 3);

        await Service(db).UpdateAsync(e.Id, form);

        var saved = await db.Exhibitions.SingleAsync(x => x.Id == e.Id);
        Assert.Equal("New name", saved.Name);
        Assert.Equal(Today.AddDays(-1), saved.StartDate);
        Assert.Equal(ExhibitionStatus.ONGOING, saved.Status);
    }

    /// <summary>Decision BE2-09: a finished exhibition with entries keeps its dates; other fields can still change.</summary>
    [Fact]
    public async Task Update_FinishedWithEntries_DatesLocked()
    {
        using var db = CreateDb();
        var e = Add(db, "Hanoi", -20, -5);
        AddEntry(db, e, EntryStatus.UNSOLD);
        var service = Service(db);
        Assert.True((await service.GetForEditAsync(e.Id)).DatesLocked);

        var form = Form("Hanoi", startIn: 1, days: 5);
        var ex = await Assert.ThrowsAsync<BusinessException>(() => service.UpdateAsync(e.Id, form));
        Assert.Equal(ExhibitionService.DatesLockedMessage, ex.Message);

        // Same dates, new description → allowed
        form.StartDate = e.StartDate;
        form.EndDate = e.EndDate;
        form.Description = "Updated";
        await service.UpdateAsync(e.Id, form);
        Assert.Equal("Updated", (await db.Exhibitions.SingleAsync(x => x.Id == e.Id)).Description);
    }

    /// <summary>BR-44: with sold entries the new period must still contain every sale date.</summary>
    [Fact]
    public async Task Update_SoldDateOutsideNewPeriod_Rejected()
    {
        using var db = CreateDb();
        var e = Add(db, "Running", -5, 5);
        AddEntry(db, e, EntryStatus.SOLD, 200m, Today.AddDays(-3));
        var service = Service(db);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => service.UpdateAsync(e.Id, Form("Running", startIn: -2, days: 7)));
        Assert.Equal(ExhibitionService.SoldOutsidePeriodMessage, ex.Message);

        // Extending the end date keeps the sale inside → allowed
        await service.UpdateAsync(e.Id, Form("Running", startIn: -5, days: 15));
        Assert.Equal(Today.AddDays(10), (await db.Exhibitions.SingleAsync(x => x.Id == e.Id)).EndDate);
    }

    /// <summary>A changed end date cannot be in the past.</summary>
    [Fact]
    public async Task Update_EndMovedToPast_Rejected()
    {
        using var db = CreateDb();
        var e = Add(db, "Running", -5, 5);
        var ex = await Assert.ThrowsAsync<BusinessException>(() => Service(db).UpdateAsync(e.Id, Form("Running", startIn: -5, days: 3)));
        Assert.Equal(ExhibitionService.EndInPastMessage, ex.Message);
    }

    // ------------------------------------------------------------------ Delete

    /// <summary>BR-42: an exhibition without entries is deleted.</summary>
    [Fact]
    public async Task Delete_NoEntries_Deleted()
    {
        using var db = CreateDb();
        var e = Add(db, "Empty", 5, 10);
        await Service(db).DeleteAsync(e.Id);
        Assert.Empty(db.Exhibitions);
    }

    /// <summary>BR-42, US-12 AC2: an exhibition with entries cannot be deleted.</summary>
    [Fact]
    public async Task Delete_WithEntries_Blocked()
    {
        using var db = CreateDb();
        var e = Add(db, "Busy", 5, 10);
        AddEntry(db, e, EntryStatus.PENDING);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => Service(db).DeleteAsync(e.Id));
        Assert.Equal(ExhibitionService.HasEntriesDeleteMessage, ex.Message);
        Assert.Single(db.Exhibitions);
    }
}
