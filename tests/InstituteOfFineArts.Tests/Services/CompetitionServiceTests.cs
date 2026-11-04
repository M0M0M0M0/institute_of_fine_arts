using InstituteOfFineArts.Web.Data;
using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Helpers;
using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.Services.Implementations;
using InstituteOfFineArts.Web.Services.Interfaces;
using InstituteOfFineArts.Web.ViewModels.Staff.Competitions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace InstituteOfFineArts.Tests.Services;

/// <summary>
/// CompetitionService (task BE1-05): code generation, BR-06 dates, BR-07 status, search, BR-10 edit limits,
/// BR-11 delete, cover handling. Dates are relative to today so the tests never expire.
/// </summary>
public class CompetitionServiceTests
{
    /// <summary>Today in the institute time zone.</summary>
    private static readonly DateOnly Today = DateTimeHelper.Today;

    /// <summary>staff_profile.id of the creator in the test database.</summary>
    private long _staffId;

    /// <summary>Fake file storage (no disk access).</summary>
    private readonly Mock<IFileStorageService> _files = new();

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

    /// <summary>Service with the fake storage.</summary>
    private CompetitionService Service(AppDbContext db) => new(db, _files.Object, NullLogger<CompetitionService>.Instance);

    /// <summary>Valid form starting in <paramref name="startIn"/> days and lasting <paramref name="days"/> days.</summary>
    private static CompetitionFormVm Form(string title = "Lunar New Year 2027", int startIn = 3, int days = 17) => new()
    {
        Title = title, Description = "Paint the festival", Rules = "One entry per student",
        StartDate = Today.AddDays(startIn), EndDate = Today.AddDays(startIn + days),
    };

    /// <summary>Adds a competition directly (for search / edit / delete tests).</summary>
    private Competition Add(AppDbContext db, string title, int startIn, int endIn, CompetitionStatus stored = CompetitionStatus.UPCOMING,
        int submissions = 0, string? cover = null, string code = "")
    {
        var c = new Competition
        {
            Code = code == "" ? "X" + Guid.NewGuid().ToString("N")[..8] : code, Title = title,
            StartDate = Today.AddDays(startIn), EndDate = Today.AddDays(endIn), Status = stored, CreatedBy = _staffId, CoverImagePath = cover,
        };
        for (var i = 0; i < submissions; i++)
        {
            c.Submissions.Add(new Submission { StudentId = i + 1, Title = "Entry", ImagePath = "p.jpg", OriginalFileName = "p.jpg", TextContent = "t", Description = "d" });
        }
        db.Competitions.Add(c);
        db.SaveChanges();
        return c;
    }

    // ------------------------------------------------------------------ Create

    /// <summary>TC-M05-01: valid form → saved with code CT{year}-01, then -02; status from the dates; creator set.</summary>
    [Fact]
    public async Task Create_GeneratesCodeAndStatus()
    {
        using var db = CreateDb();
        var service = Service(db);

        var id1 = await service.CreateAsync(Form("  Lunar New Year 2027  "), _staffId);
        var id2 = await service.CreateAsync(Form("Family Portrait", startIn: 0), _staffId);

        var c1 = await db.Competitions.SingleAsync(c => c.Id == id1);
        var c2 = await db.Competitions.SingleAsync(c => c.Id == id2);
        var year = Today.AddDays(3).Year;
        Assert.Equal($"CT{year}-01", c1.Code);
        Assert.Equal($"CT{Today.Year}-0{(Today.Year == year ? 2 : 1)}", c2.Code);
        Assert.Equal("Lunar New Year 2027", c1.Title);
        Assert.Equal(CompetitionStatus.UPCOMING, c1.Status);
        Assert.Equal(CompetitionStatus.ONGOING, c2.Status);
        Assert.Equal(_staffId, c1.CreatedBy);
    }

    /// <summary>The number continues after the highest existing code of the year.</summary>
    [Fact]
    public async Task Create_CodeContinuesAfterHighest()
    {
        using var db = CreateDb();
        var year = Today.AddDays(3).Year;
        Add(db, "Old", -400, -390, code: $"CT{year}-07");
        var id = await Service(db).CreateAsync(Form(), _staffId);
        Assert.Equal($"CT{year}-08", (await db.Competitions.SingleAsync(c => c.Id == id)).Code);
    }

    /// <summary>BR-06: a new competition cannot end in the past.</summary>
    [Fact]
    public async Task Create_EndInPast_Rejected()
    {
        using var db = CreateDb();
        var ex = await Assert.ThrowsAsync<BusinessException>(() => Service(db).CreateAsync(Form(startIn: -10, days: 5), _staffId));
        Assert.Equal(CompetitionService.EndInPastMessage, ex.Message);
        Assert.Equal(nameof(CompetitionFormVm.EndDate), ex.Field);
        Assert.Empty(db.Competitions);
    }

    /// <summary>TC-M05-02: end before start → rejected (form rule and service rule).</summary>
    [Fact]
    public async Task Create_EndBeforeStart_Rejected()
    {
        using var db = CreateDb();
        var form = Form(startIn: 10, days: -5);
        Assert.Contains(form.Validate(new System.ComponentModel.DataAnnotations.ValidationContext(form)),
            r => r.ErrorMessage == CompetitionFormVm.EndBeforeStartMessage);
        var ex = await Assert.ThrowsAsync<BusinessException>(() => Service(db).CreateAsync(form, _staffId));
        Assert.Equal(CompetitionFormVm.EndBeforeStartMessage, ex.Message);
    }

    /// <summary>Cover image is stored in the folder of the new id and its path saved.</summary>
    [Fact]
    public async Task Create_WithCover_SavesPath()
    {
        using var db = CreateDb();
        _files.Setup(f => f.SaveCompetitionCoverAsync(It.IsAny<IFormFile>(), It.IsAny<long>(), It.IsAny<string>()))
              .ReturnsAsync((IFormFile _, long id, string _) => $"competitions/{id}/cover-1.jpg");
        var form = Form();
        form.CoverImage = TestImages.AsFormFile(new byte[] { 1 });

        var id = await Service(db).CreateAsync(form, _staffId);
        Assert.Equal($"competitions/{id}/cover-1.jpg", (await db.Competitions.SingleAsync()).CoverImagePath);
    }

    /// <summary>A rejected cover undoes the insert so the user can fix the form.</summary>
    [Fact]
    public async Task Create_InvalidCover_NothingSaved()
    {
        using var db = CreateDb();
        _files.Setup(f => f.SaveCompetitionCoverAsync(It.IsAny<IFormFile>(), It.IsAny<long>(), It.IsAny<string>()))
              .ThrowsAsync(new BusinessException(JpegValidator.NotJpegMessage, "CoverImage"));
        var form = Form();
        form.CoverImage = TestImages.AsFormFile(new byte[] { 1 }, "x.png", "image/png");

        var ex = await Assert.ThrowsAsync<BusinessException>(() => Service(db).CreateAsync(form, _staffId));
        Assert.Equal("CoverImage", ex.Field);
        Assert.Empty(db.Competitions);
    }

    // ------------------------------------------------------------------ Search

    /// <summary>TC-M05-03 / TC-M05-07: filter by calculated status (stale stored values are corrected) and keyword.</summary>
    [Fact]
    public async Task Search_ByStatusAndKeyword()
    {
        using var db = CreateDb();
        Add(db, "Hometown Landscapes", -40, -30, CompetitionStatus.GRADED);
        Add(db, "Wildlife", -20, -1, CompetitionStatus.ONGOING);           // stored ONGOING, really CLOSED
        Add(db, "Colors of Autumn", -5, 0, CompetitionStatus.UPCOMING);    // ends today → still ONGOING
        Add(db, "Family Portrait", 5, 20);
        var service = Service(db);

        async Task<string[]> Titles(CompetitionStatus? status, string? keyword = null)
            => (await service.SearchAsync(new CompetitionSearchVm { Status = status, Keyword = keyword })).Items.Select(i => i.Title).ToArray();

        Assert.Equal(new[] { "Hometown Landscapes" }, await Titles(CompetitionStatus.GRADED));
        Assert.Equal(new[] { "Wildlife" }, await Titles(CompetitionStatus.CLOSED));
        Assert.Equal(new[] { "Colors of Autumn" }, await Titles(CompetitionStatus.ONGOING));
        Assert.Equal(new[] { "Family Portrait" }, await Titles(CompetitionStatus.UPCOMING));
        Assert.Equal(new[] { "Colors of Autumn" }, await Titles(null, "Autumn"));
        Assert.Equal(4, (await service.SearchAsync(new CompetitionSearchVm())).TotalCount);

        // Shown status is calculated for today
        var wildlife = (await service.SearchAsync(new CompetitionSearchVm { Keyword = "Wildlife" })).Items.Single();
        Assert.Equal(CompetitionStatus.CLOSED, wildlife.Status);
    }

    /// <summary>Date range keeps competitions whose period overlaps it; newest start first; counts included.</summary>
    [Fact]
    public async Task Search_DateRangeOrderAndCounts()
    {
        using var db = CreateDb();
        Add(db, "A", -40, -30);
        Add(db, "B", -10, 10, submissions: 2);
        Add(db, "C", 20, 30);
        var result = await Service(db).SearchAsync(new CompetitionSearchVm { From = Today.AddDays(-12), To = Today.AddDays(25) });

        Assert.Equal(new[] { "C", "B" }, result.Items.Select(i => i.Title));
        Assert.Equal(2, result.Items.Single(i => i.Title == "B").SubmissionCount);
    }

    /// <summary>Paging: page size and page number.</summary>
    [Fact]
    public async Task Search_Paging()
    {
        using var db = CreateDb();
        for (var i = 0; i < 12; i++)
        {
            Add(db, $"Comp {i:D2}", i, i + 5);
        }
        var page2 = await Service(db).SearchAsync(new CompetitionSearchVm { Page = 2 }, pageSize: 5);
        Assert.Equal(12, page2.TotalCount);
        Assert.Equal(3, page2.TotalPages);
        Assert.Equal(5, page2.Items.Count);
    }

    // ------------------------------------------------------------------ Details

    /// <summary>Details: awards by rank, entries count, delete blocked with entries.</summary>
    [Fact]
    public async Task Details_AwardsCountsAndFlags()
    {
        using var db = CreateDb();
        var c = Add(db, "Wildlife", -5, 5, submissions: 3);
        db.CompetitionAwards.AddRange(
            new CompetitionAward { CompetitionId = c.Id, AwardName = "Second Prize", Rank = 2, PrizeValue = 150, Quantity = 1 },
            new CompetitionAward { CompetitionId = c.Id, AwardName = "First Prize", Rank = 1, PrizeValue = 300, Quantity = 1 });
        db.SaveChanges();

        var vm = await Service(db).GetDetailsAsync(c.Id);
        Assert.Equal(new[] { "First Prize", "Second Prize" }, vm.Awards.Select(a => a.Name));
        Assert.Equal(3, vm.SubmissionCount);
        Assert.Equal(CompetitionStatus.ONGOING, vm.Status);
        Assert.Equal("Le Minh Hoang", vm.CreatedByName);
        Assert.True(vm.CanEdit);
        Assert.False(vm.CanDelete);
        Assert.Empty(vm.Warnings);
    }

    /// <summary>BR-09: an upcoming competition without awards shows a warning.</summary>
    [Fact]
    public async Task Details_NoAwards_Warning()
    {
        using var db = CreateDb();
        var c = Add(db, "Family Portrait", 5, 20);
        var vm = await Service(db).GetDetailsAsync(c.Id);
        Assert.Contains(CompetitionService.NoAwardsWarning, vm.Warnings);
        Assert.True(vm.CanDelete);
    }

    /// <summary>Unknown id → NotFoundException.</summary>
    [Fact]
    public async Task Details_Unknown_NotFound()
    {
        using var db = CreateDb();
        await Assert.ThrowsAsync<NotFoundException>(() => Service(db).GetDetailsAsync(999));
    }

    // ------------------------------------------------------------------ Update (BR-10)

    /// <summary>TC-M05-04: without entries every field can change; status is recalculated.</summary>
    [Fact]
    public async Task Update_NoEntries_AllFields()
    {
        using var db = CreateDb();
        var c = Add(db, "Family Portrait", 5, 20);
        await Service(db).UpdateAsync(c.Id, new CompetitionFormVm
        {
            Title = "Family Portraits", Rules = "New rules", StartDate = Today, EndDate = Today.AddDays(10),
        });
        var saved = await db.Competitions.SingleAsync(x => x.Id == c.Id);
        Assert.Equal("Family Portraits", saved.Title);
        Assert.Equal("New rules", saved.Rules);
        Assert.Equal(CompetitionStatus.ONGOING, saved.Status);
    }

    /// <summary>With entries: title, start date and shortening the end date are refused.</summary>
    [Theory]
    [InlineData("Changed", 0, 0, CompetitionService.TitleLockedMessage)]
    [InlineData("Wildlife", 1, 0, CompetitionService.StartLockedMessage)]
    [InlineData("Wildlife", 0, -1, CompetitionService.EndOnlyExtendMessage)]
    public async Task Update_WithEntries_Locked(string title, int startShift, int endShift, string message)
    {
        using var db = CreateDb();
        var c = Add(db, "Wildlife", -5, 10, CompetitionStatus.ONGOING, submissions: 1);
        var ex = await Assert.ThrowsAsync<BusinessException>(() => Service(db).UpdateAsync(c.Id, new CompetitionFormVm
        {
            Title = title, StartDate = c.StartDate.AddDays(startShift), EndDate = c.EndDate.AddDays(endShift),
        }));
        Assert.Equal(message, ex.Message);
    }

    /// <summary>With entries: extending the end date and changing description/rules are allowed.</summary>
    [Fact]
    public async Task Update_WithEntries_ExtendAndText()
    {
        using var db = CreateDb();
        var c = Add(db, "Wildlife", -5, 10, CompetitionStatus.ONGOING, submissions: 1);
        await Service(db).UpdateAsync(c.Id, new CompetitionFormVm
        {
            Title = "Wildlife", Description = "Updated", StartDate = c.StartDate, EndDate = c.EndDate.AddDays(7),
        });
        var saved = await db.Competitions.SingleAsync(x => x.Id == c.Id);
        Assert.Equal(Today.AddDays(17), saved.EndDate);
        Assert.Equal("Updated", saved.Description);
    }

    /// <summary>GRADED competitions are read only.</summary>
    [Fact]
    public async Task Update_Graded_ReadOnly()
    {
        using var db = CreateDb();
        var c = Add(db, "Hometown Landscapes", -40, -30, CompetitionStatus.GRADED);
        var service = Service(db);
        var ex = await Assert.ThrowsAsync<BusinessException>(() => service.UpdateAsync(c.Id, Form()));
        Assert.Equal(CompetitionService.GradedReadOnlyMessage, ex.Message);
        await Assert.ThrowsAsync<BusinessException>(() => service.GetForEditAsync(c.Id));
        Assert.False((await service.GetDetailsAsync(c.Id)).CanEdit);
    }

    /// <summary>A new cover replaces the old one; the old file is deleted after saving.</summary>
    [Fact]
    public async Task Update_ReplaceCover_DeletesOld()
    {
        using var db = CreateDb();
        var c = Add(db, "Family Portrait", 5, 20, cover: "competitions/1/old.jpg");
        _files.Setup(f => f.SaveCompetitionCoverAsync(It.IsAny<IFormFile>(), c.Id, It.IsAny<string>())).ReturnsAsync("competitions/1/new.jpg");
        var form = Form("Family Portrait", 5, 15);
        form.CoverImage = TestImages.AsFormFile(new byte[] { 1 });

        await Service(db).UpdateAsync(c.Id, form);
        Assert.Equal("competitions/1/new.jpg", (await db.Competitions.SingleAsync()).CoverImagePath);
        _files.Verify(f => f.Delete("competitions/1/old.jpg"), Times.Once);
    }

    /// <summary>"Remove cover" clears the path and deletes the file.</summary>
    [Fact]
    public async Task Update_RemoveCover()
    {
        using var db = CreateDb();
        var c = Add(db, "Family Portrait", 5, 20, cover: "competitions/1/old.jpg");
        var form = Form("Family Portrait", 5, 15);
        form.RemoveCover = true;

        await Service(db).UpdateAsync(c.Id, form);
        Assert.Null((await db.Competitions.SingleAsync()).CoverImagePath);
        _files.Verify(f => f.Delete("competitions/1/old.jpg"), Times.Once);
    }

    // ------------------------------------------------------------------ Delete (BR-11)

    /// <summary>TC-M05-05: no entries → deleted with its cover file.</summary>
    [Fact]
    public async Task Delete_NoEntries_Deleted()
    {
        using var db = CreateDb();
        var c = Add(db, "Family Portrait", 5, 20, cover: "competitions/1/c.jpg");
        await Service(db).DeleteAsync(c.Id);
        Assert.Empty(db.Competitions);
        _files.Verify(f => f.Delete("competitions/1/c.jpg"), Times.Once);
    }

    /// <summary>TC-M05-06: entries exist → blocked, nothing removed.</summary>
    [Fact]
    public async Task Delete_WithEntries_Blocked()
    {
        using var db = CreateDb();
        var c = Add(db, "Colors of Autumn", -5, 5, submissions: 6, cover: "competitions/1/c.jpg");
        var ex = await Assert.ThrowsAsync<BusinessException>(() => Service(db).DeleteAsync(c.Id));
        Assert.Equal(CompetitionService.HasSubmissionsDeleteMessage, ex.Message);
        Assert.Single(db.Competitions);
        _files.Verify(f => f.Delete(It.IsAny<string?[]>()), Times.Never);
    }
}
