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
/// Description and Rules of the competition form written with the rich text editor (TODO step 8, BR-30): sanitized
/// when saved, at most 4000 characters of text, older plain text loaded as paragraphs without a false history entry.
/// </summary>
public class CompetitionRichTextTests
{
    private static readonly DateOnly Today = DateTimeHelper.Today;
    private long _staffId;

    private AppDbContext CreateDb()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var staff = new StaffProfile
        {
            StaffCode = "NV0001", JoinedDate = new DateOnly(2024, 1, 1),
            Account = new Account { Username = "staff01", FullName = "Le Minh Hoang", Email = "s@ifa.local", Role = Role.STAFF, PasswordHash = "x" },
        };
        db.StaffProfiles.Add(staff);
        db.SaveChanges();
        db.ChangeTracker.Clear();
        _staffId = staff.Id;
        return db;
    }

    private static CompetitionService Service(AppDbContext db)
        => new(db, new Mock<IFileStorageService>().Object, NullLogger<CompetitionService>.Instance, new Mock<INotificationService>().Object);

    private static CompetitionFormVm Form(string? description, string? rules = null) => new()
    {
        Title = "Lunar New Year 2027", Description = description, Rules = rules,
        StartDate = Today.AddDays(3), EndDate = Today.AddDays(20),
    };

    /// <summary>The posted HTML is saved without what is not allowed; an empty editor saves nothing.</summary>
    [Fact]
    public async Task Create_SanitizesHtml()
    {
        using var db = CreateDb();
        var id = await Service(db).CreateAsync(Form("<p><strong>Paint</strong> the festival<img src=x onerror=alert(1)></p><script>x()</script>", "<p></p>"), _staffId);

        var c = await db.Competitions.SingleAsync(x => x.Id == id);
        Assert.Equal("<p><strong>Paint</strong> the festival</p>", c.Description);
        Assert.Null(c.Rules);
    }

    /// <summary>More than 4000 characters of text is refused on its field; the tags do not count.</summary>
    [Fact]
    public async Task Create_TextTooLong_Refused()
    {
        using var db = CreateDb();
        var service = Service(db);
        var ok = "<p><strong>" + new string('a', RichText.MaxTextLength) + "</strong></p>";
        await service.CreateAsync(Form(ok), _staffId);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => service.CreateAsync(Form(null, "<p>" + new string('a', RichText.MaxTextLength + 1) + "</p>"), _staffId));
        Assert.Equal(nameof(CompetitionFormVm.Rules), ex.Field);
    }

    /// <summary>Older plain text opens in the editor as paragraphs; saving it unchanged is not logged as a change.</summary>
    [Fact]
    public async Task Edit_PlainText_LoadedAsParagraphs_NoFalseChange()
    {
        using var db = CreateDb();
        var service = Service(db);
        var id = await service.CreateAsync(Form("Paint the festival.\n\nOne entry per student."), _staffId);
        Assert.Equal("Paint the festival.\n\nOne entry per student.", (await db.Competitions.SingleAsync()).Description);
        db.ChangeTracker.Clear();

        var form = await service.GetForEditAsync(id);
        Assert.Equal("<p>Paint the festival.</p><p>One entry per student.</p>", form.Description);
        // The editor posts it back with a line break between the paragraphs
        form.Description = "<p>Paint the festival.</p>\n<p>One entry per student.</p>";
        var changes = await service.UpdateAsync(id, form);
        Assert.DoesNotContain("Description changed", changes);

        form.Description = "<p>Paint the <em>festival</em>.</p>";
        changes = await service.UpdateAsync(id, form);
        Assert.Contains("Description changed", changes);
    }
}
