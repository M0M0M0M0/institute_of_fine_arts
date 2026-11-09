using InstituteOfFineArts.Web.Data;
using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Helpers;
using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.Services.Implementations;
using InstituteOfFineArts.Web.Services.Interfaces;
using InstituteOfFineArts.Web.ViewModels.Staff.Awards;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace InstituteOfFineArts.Tests.Services;

/// <summary>
/// AwardIssuedService (task BE1-11, F-M08-01..04): issue only in CLOSED / GRADED competitions (BR-32, TC-M08-02),
/// only to BEST entries and never DISQUALIFIED ones (BR-30, TC-M08-03), no more than quantity and one award per
/// entry / student (BR-31, US-11 AC1/AC3, TC-M08-04), edit with the same rules, revoke with a reason (BR-33), search.
/// </summary>
public class AwardIssuedServiceTests
{
    /// <summary>Today in the institute time zone.</summary>
    private static readonly DateOnly Today = DateTimeHelper.Today;

    /// <summary>staff_profile.id of staff01 in the test database.</summary>
    private long _staffId;

    /// <summary>Test data: one competition, two defined awards and six entries keyed by name.</summary>
    /// <param name="Db">In-memory database</param>
    /// <param name="Competition">Competition in the wanted status</param>
    /// <param name="First">First Prize, quantity 1</param>
    /// <param name="Mention">Honorable Mention, quantity 2</param>
    /// <param name="Entries">best1, best2, best3 (BEST), good (GOOD), dq (DISQUALIFIED), none (not evaluated)</param>
    private sealed record Data(AppDbContext Db, Competition Competition, CompetitionAward First, CompetitionAward Mention,
        Dictionary<string, Submission> Entries);

    /// <summary>Fake notification service (M12): the tests check which event is raised.</summary>
    private readonly Mock<INotificationService> _notifications = new();

    /// <summary>Service under test.</summary>
    private AwardIssuedService Service(AppDbContext db) => new(db, NullLogger<AwardIssuedService>.Instance, _notifications.Object);

    /// <summary>Builds the test data with the competition in the given calculated status.</summary>
    private Data Seed(CompetitionStatus status = CompetitionStatus.CLOSED)
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var staff = new StaffProfile
        {
            StaffCode = "NV0003", JoinedDate = new DateOnly(2024, 1, 1),
            Account = new Account { Username = "staff01", FullName = "Le Minh Hoang", Email = "s1@ifa.local", Role = Role.STAFF, PasswordHash = "x" },
        };
        db.StaffProfiles.Add(staff);
        db.SaveChanges();
        _staffId = staff.Id;

        var c = AddCompetition(db, "City at Night", status);
        var first = new CompetitionAward { CompetitionId = c.Id, AwardName = "First Prize", Rank = 1, PrizeValue = 250m, Quantity = 1 };
        var mention = new CompetitionAward { CompetitionId = c.Id, AwardName = "Honorable Mention", Rank = 4, PrizeValue = 50m, Quantity = 2 };
        db.CompetitionAwards.AddRange(first, mention);
        db.SaveChanges();

        // One entry per student (UX_submission_competition_student), with the rating in the name
        var specs = new (string Key, string Name, Rating? Rating)[]
        {
            ("best1", "Nguyen Van An", Rating.BEST), ("best2", "Tran Thi Binh", Rating.BEST), ("best3", "Le Van Cuong", Rating.BEST),
            ("good", "Pham Thi Dung", Rating.GOOD), ("dq", "Hoang Van Em", Rating.DISQUALIFIED), ("none", "Vo Thi Phuong", null),
        };
        var entries = new Dictionary<string, Submission>();
        var n = 0;
        foreach (var (key, name, rating) in specs)
        {
            n++;
            var student = new StudentProfile
            {
                StudentCode = $"HV{n:D4}", EnrollmentDate = new DateOnly(2025, 9, 1), CourseName = "Painting",
                Account = new Account { Username = $"student{n:D2}", FullName = name, Email = $"st{n}@ifa.local", Role = Role.STUDENT, PasswordHash = "x" },
            };
            db.StudentProfiles.Add(student);
            db.SaveChanges();
            entries[key] = AddEntry(db, c, student.Id, $"Painting {key}", rating, n);
        }
        return new Data(db, c, first, mention, entries);
    }

    /// <summary>Adds a competition whose calculated status is <paramref name="status"/> (GRADED is stored).</summary>
    private Competition AddCompetition(AppDbContext db, string title, CompetitionStatus status)
    {
        var (start, end) = status switch
        {
            CompetitionStatus.UPCOMING => (Today.AddDays(5), Today.AddDays(30)),
            CompetitionStatus.ONGOING => (Today.AddDays(-5), Today.AddDays(5)),
            _ => (Today.AddDays(-30), Today.AddDays(-5)),
        };
        var c = new Competition
        {
            Code = "X" + Guid.NewGuid().ToString("N")[..8], Title = title, StartDate = start, EndDate = end, CreatedBy = _staffId,
            Status = status == CompetitionStatus.GRADED ? CompetitionStatus.GRADED : CompetitionStatus.UPCOMING,
        };
        db.Competitions.Add(c);
        db.SaveChanges();
        return c;
    }

    /// <summary>Adds an entry with an optional evaluation.</summary>
    private Submission AddEntry(AppDbContext db, Competition c, long studentId, string title, Rating? rating, int hour = 10)
    {
        var s = new Submission
        {
            CompetitionId = c.Id, StudentId = studentId, Title = title, ImagePath = "p.jpg", OriginalFileName = "p.jpg",
            FileSize = 10, TextContent = "A poem", Description = "Why", SubmittedAt = c.StartDate.ToDateTime(new TimeOnly(hour % 24, 0)),
        };
        db.Submissions.Add(s);
        db.SaveChanges();
        if (rating is { } r)
        {
            db.Evaluations.Add(new Evaluation
            {
                SubmissionId = s.Id, EvaluatedBy = _staffId, Rating = r, PositivePoints = "Nice",
                NegativePoints = r == Rating.DISQUALIFIED ? "Off topic" : null, EvaluatedAt = DateTime.Now.AddDays(-1),
            });
            db.SaveChanges();
        }
        return s;
    }

    /// <summary>Adds an issued award directly.</summary>
    private AwardIssued Issue(AppDbContext db, CompetitionAward award, Submission s, DateOnly? date = null, string? remarks = null)
    {
        var issued = new AwardIssued
        {
            CompetitionAwardId = award.Id, SubmissionId = s.Id, IssuedBy = _staffId, IssuedDate = date ?? Today, Remarks = remarks,
        };
        db.AwardsIssued.Add(issued);
        db.SaveChanges();
        return issued;
    }

    /// <summary>Valid issue form for the data.</summary>
    private static AwardIssuedFormVm Form(Data d, CompetitionAward? award = null, string entry = "best1", DateOnly? date = null,
        string? remarks = "  Outstanding use of light.  ") => new()
    {
        CompetitionId = d.Competition.Id,
        CompetitionAwardId = (award ?? d.First).Id,
        SubmissionId = d.Entries[entry].Id,
        IssuedDate = date ?? Today,
        Remarks = remarks,
    };

    // ------------------------------------------------------------------ Issue

    /// <summary>TC-M08-01: BEST entry of a CLOSED competition → award issued with date, trimmed remarks and issuer.</summary>
    [Fact]
    public async Task Create_BestEntry_Saved()
    {
        var d = Seed();
        using var db = d.Db;

        var id = await Service(db).CreateAsync(Form(d), _staffId);

        var issued = await db.AwardsIssued.SingleAsync();
        Assert.Equal(id, issued.Id);
        Assert.Equal(d.First.Id, issued.CompetitionAwardId);
        Assert.Equal(d.Entries["best1"].Id, issued.SubmissionId);
        Assert.Equal(_staffId, issued.IssuedBy);
        Assert.Equal(Today, issued.IssuedDate);
        Assert.Equal("Outstanding use of light.", issued.Remarks);

        // M12 event 3: the winner is told once
        _notifications.Verify(n => n.AwardIssuedAsync(id), Times.Once);
    }

    /// <summary>BR-32 decision: a GRADED competition still accepts awards.</summary>
    [Fact]
    public async Task Create_Graded_Allowed()
    {
        var d = Seed(CompetitionStatus.GRADED);
        using var db = d.Db;

        await Service(db).CreateAsync(Form(d, remarks: null), _staffId);
        Assert.Null((await db.AwardsIssued.SingleAsync()).Remarks);
    }

    /// <summary>TC-M08-02: UPCOMING / ONGOING → "Awards can only be issued after the competition has closed."</summary>
    [Theory]
    [InlineData(CompetitionStatus.UPCOMING)]
    [InlineData(CompetitionStatus.ONGOING)]
    public async Task Create_NotClosed_Refused(CompetitionStatus status)
    {
        var d = Seed(status);
        using var db = d.Db;

        var ex = await Assert.ThrowsAsync<BusinessException>(() => Service(db).CreateAsync(Form(d), _staffId));
        Assert.Equal(AwardIssuedService.NotClosedMessage, ex.Message);
        Assert.Null(ex.Field);
        var page = await Assert.ThrowsAsync<BusinessException>(() => Service(db).GetForCreateAsync(d.Competition.Id));
        Assert.Equal(AwardIssuedService.NotClosedMessage, page.Message);
        Assert.Equal(0, await db.AwardsIssued.CountAsync());
    }

    /// <summary>US-11 AC1 / TC-M08-04: First Prize (quantity 1) already given → "No award slots left."</summary>
    [Fact]
    public async Task Create_NoSlotsLeft_Refused()
    {
        var d = Seed();
        using var db = d.Db;
        Issue(db, d.First, d.Entries["best1"]);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => Service(db).CreateAsync(Form(d, entry: "best2"), _staffId));
        Assert.Equal(AwardIssuedService.NoSlotsMessage, ex.Message);
        Assert.Equal(nameof(AwardIssuedFormVm.CompetitionAwardId), ex.Field);

        // An award with quantity 2 can be given twice, not three times
        await Service(db).CreateAsync(Form(d, d.Mention, "best2"), _staffId);
        await Service(db).CreateAsync(Form(d, d.Mention, "best3"), _staffId);
        Assert.Equal(3, await db.AwardsIssued.CountAsync());
    }

    /// <summary>BR-30 / US-11 AC2 / TC-M08-03: GOOD or not evaluated → only BEST; DISQUALIFIED → never.</summary>
    [Theory]
    [InlineData("good", AwardIssuedService.NotBestMessage)]
    [InlineData("none", AwardIssuedService.NotBestMessage)]
    [InlineData("dq", AwardIssuedService.DisqualifiedMessage)]
    public async Task Create_NotBest_Refused(string entry, string message)
    {
        var d = Seed();
        using var db = d.Db;

        var ex = await Assert.ThrowsAsync<BusinessException>(() => Service(db).CreateAsync(Form(d, entry: entry), _staffId));
        Assert.Equal(message, ex.Message);
        Assert.Equal(nameof(AwardIssuedFormVm.SubmissionId), ex.Field);
        Assert.Equal(0, await db.AwardsIssued.CountAsync());
    }

    /// <summary>US-11 AC3 / BR-31: an entry that already has Second Prize cannot also receive another award.</summary>
    [Fact]
    public async Task Create_SameEntryTwice_Refused()
    {
        var d = Seed();
        using var db = d.Db;
        Issue(db, d.Mention, d.Entries["best1"]);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => Service(db).CreateAsync(Form(d, d.First, "best1"), _staffId));
        Assert.Equal(AwardIssuedService.AlreadyAwardedMessage, ex.Message);
        Assert.Equal(nameof(AwardIssuedFormVm.SubmissionId), ex.Field);
        Assert.Equal(1, await db.AwardsIssued.CountAsync());
    }

    /// <summary>An award or an entry of another competition, or a missing / unknown competition → refused on the field.</summary>
    [Fact]
    public async Task Create_OtherCompetitionOrMissingValues_Refused()
    {
        var d = Seed();
        using var db = d.Db;
        var other = AddCompetition(db, "Wildlife", CompetitionStatus.CLOSED);
        var otherAward = new CompetitionAward { CompetitionId = other.Id, AwardName = "First Prize", Rank = 1, PrizeValue = 300m, Quantity = 1 };
        db.CompetitionAwards.Add(otherAward);
        db.SaveChanges();
        var otherEntry = AddEntry(db, other, d.Entries["best1"].StudentId, "Tiger", Rating.BEST);
        var service = Service(db);

        var wrongAward = await Assert.ThrowsAsync<BusinessException>(() => service.CreateAsync(Form(d, otherAward), _staffId));
        Assert.Equal(AwardIssuedService.AwardNotInCompetitionMessage, wrongAward.Message);
        var wrongEntry = Form(d);
        wrongEntry.SubmissionId = otherEntry.Id;
        var ex = await Assert.ThrowsAsync<BusinessException>(() => service.CreateAsync(wrongEntry, _staffId));
        Assert.Equal(AwardIssuedService.SubmissionNotInCompetitionMessage, ex.Message);

        var noCompetition = Form(d);
        noCompetition.CompetitionId = null;
        Assert.Equal(nameof(AwardIssuedFormVm.CompetitionId),
            (await Assert.ThrowsAsync<BusinessException>(() => service.CreateAsync(noCompetition, _staffId))).Field);
        var unknown = Form(d);
        unknown.CompetitionId = 999;
        Assert.Equal(AwardIssuedService.UnknownCompetitionMessage,
            (await Assert.ThrowsAsync<BusinessException>(() => service.CreateAsync(unknown, _staffId))).Message);
        var noAward = Form(d);
        noAward.CompetitionAwardId = null;
        Assert.Equal(nameof(AwardIssuedFormVm.CompetitionAwardId),
            (await Assert.ThrowsAsync<BusinessException>(() => service.CreateAsync(noAward, _staffId))).Field);
        var noEntry = Form(d);
        noEntry.SubmissionId = null;
        Assert.Equal(nameof(AwardIssuedFormVm.SubmissionId),
            (await Assert.ThrowsAsync<BusinessException>(() => service.CreateAsync(noEntry, _staffId))).Field);
        Assert.Equal(0, await db.AwardsIssued.CountAsync());
    }

    /// <summary>Issued date: required, not in the future, after the competition end date; remarks at most 1000 characters.</summary>
    [Fact]
    public async Task Create_InvalidDateOrRemarks_Refused()
    {
        var d = Seed();
        using var db = d.Db;
        var service = Service(db);

        var future = await Assert.ThrowsAsync<BusinessException>(() => service.CreateAsync(Form(d, date: Today.AddDays(1)), _staffId));
        Assert.Equal(AwardIssuedService.FutureDateMessage, future.Message);
        Assert.Equal(nameof(AwardIssuedFormVm.IssuedDate), future.Field);
        var onEnd = await Assert.ThrowsAsync<BusinessException>(() => service.CreateAsync(Form(d, date: d.Competition.EndDate), _staffId));
        Assert.Equal(AwardIssuedService.DateBeforeEndMessage, onEnd.Message);
        var noDate = Form(d);
        noDate.IssuedDate = null;
        Assert.Equal(nameof(AwardIssuedFormVm.IssuedDate),
            (await Assert.ThrowsAsync<BusinessException>(() => service.CreateAsync(noDate, _staffId))).Field);
        var longRemarks = await Assert.ThrowsAsync<BusinessException>(() => service.CreateAsync(Form(d, remarks: new string('a', 1001)), _staffId));
        Assert.Equal(nameof(AwardIssuedFormVm.Remarks), longRemarks.Field);

        // The day after the end date is the first valid day
        await service.CreateAsync(Form(d, date: d.Competition.EndDate.AddDays(1)), _staffId);
        Assert.Equal(1, await db.AwardsIssued.CountAsync());
    }

    /// <summary>UC-06 2a/2b, US-11 AC2: the form offers only awards with slots and BEST entries without an award.</summary>
    [Fact]
    public async Task GetForCreate_OnlyFreeAwardsAndEligibleEntries()
    {
        var d = Seed();
        using var db = d.Db;
        Issue(db, d.First, d.Entries["best1"]);
        AddCompetition(db, "Colors of Autumn", CompetitionStatus.ONGOING);
        var graded = AddCompetition(db, "Hometown Landscapes", CompetitionStatus.GRADED);

        var form = await Service(db).GetForCreateAsync(d.Competition.Id);

        Assert.Equal(new[] { "Honorable Mention" }, form.Awards.Select(a => a.Name));
        Assert.Equal(2, form.Awards[0].SlotsLeft);
        Assert.Equal(new[] { d.Entries["best2"].Id, d.Entries["best3"].Id }, form.Submissions.Select(s => s.Id));
        Assert.Equal(Today, form.IssuedDate);
        Assert.Equal(CompetitionStatus.CLOSED, form.CompetitionStatus);
        // Only CLOSED / GRADED competitions are offered
        Assert.Equal(new[] { d.Competition.Id, graded.Id }.Order(), form.Competitions.Select(c => c.Id).Order());

        // Without a competition: just the list; unknown competition → not found
        var empty = await Service(db).GetForCreateAsync(null);
        Assert.Empty(empty.Awards);
        Assert.Null(empty.CompetitionStatus);
        await Assert.ThrowsAsync<NotFoundException>(() => Service(db).GetForCreateAsync(999));
    }

    // ------------------------------------------------------------------ Edit

    /// <summary>F-M08-02: edit keeps its own slot and entry selectable, and can move to another eligible entry.</summary>
    [Fact]
    public async Task Update_ChangeEntryDateRemarks_Saved()
    {
        var d = Seed();
        using var db = d.Db;
        var issued = Issue(db, d.First, d.Entries["best1"], Today.AddDays(-1), "Old");
        var service = Service(db);

        // The current award (full, quantity 1) and the current entry stay in the options
        var form = await service.GetForEditAsync(issued.Id);
        Assert.Contains(form.Awards, a => a.Id == d.First.Id);
        Assert.Contains(form.Submissions, s => s.Id == d.Entries["best1"].Id);
        Assert.Equal("Old", form.Remarks);

        form.SubmissionId = d.Entries["best2"].Id;
        form.IssuedDate = Today;
        form.Remarks = "New remarks";
        var competitionId = await service.UpdateAsync(issued.Id, form);

        Assert.Equal(d.Competition.Id, competitionId);
        var saved = await db.AwardsIssued.AsNoTracking().SingleAsync();
        Assert.Equal(d.Entries["best2"].Id, saved.SubmissionId);
        Assert.Equal(d.First.Id, saved.CompetitionAwardId);
        Assert.Equal(Today, saved.IssuedDate);
        Assert.Equal("New remarks", saved.Remarks);
    }

    /// <summary>Edit with the same rules: a full award, a GOOD entry or another winner's entry are refused.</summary>
    [Fact]
    public async Task Update_BreaksRules_Refused()
    {
        var d = Seed();
        using var db = d.Db;
        Issue(db, d.First, d.Entries["best1"]);
        var mine = Issue(db, d.Mention, d.Entries["best2"]);
        var service = Service(db);

        var toFull = Form(d, d.First, "best2");
        Assert.Equal(AwardIssuedService.NoSlotsMessage,
            (await Assert.ThrowsAsync<BusinessException>(() => service.UpdateAsync(mine.Id, toFull))).Message);
        Assert.Equal(AwardIssuedService.NotBestMessage,
            (await Assert.ThrowsAsync<BusinessException>(() => service.UpdateAsync(mine.Id, Form(d, d.Mention, "good")))).Message);
        Assert.Equal(AwardIssuedService.AlreadyAwardedMessage,
            (await Assert.ThrowsAsync<BusinessException>(() => service.UpdateAsync(mine.Id, Form(d, d.Mention, "best1")))).Message);

        var saved = await db.AwardsIssued.AsNoTracking().SingleAsync(a => a.Id == mine.Id);
        Assert.Equal(d.Entries["best2"].Id, saved.SubmissionId);
    }

    /// <summary>The competition of an issued award never changes: a competition id posted by the browser is ignored.</summary>
    [Fact]
    public async Task Update_PostedCompetitionIgnored()
    {
        var d = Seed();
        using var db = d.Db;
        var issued = Issue(db, d.First, d.Entries["best1"]);
        var other = AddCompetition(db, "Wildlife", CompetitionStatus.CLOSED);

        var form = Form(d, d.First, "best1", remarks: "Kept in its competition");
        form.CompetitionId = other.Id;
        Assert.Equal(d.Competition.Id, await Service(db).UpdateAsync(issued.Id, form));

        // FillOptionsAsync (edit form shown again) also takes the competition from the database
        form.Id = issued.Id;
        form.CompetitionId = other.Id;
        await Service(db).FillOptionsAsync(form);
        Assert.Equal(d.Competition.Id, form.CompetitionId);
    }

    // ------------------------------------------------------------------ Revoke

    /// <summary>BR-33: revoke with a reason deletes the award (the slot becomes free); without a reason it is refused.</summary>
    [Fact]
    public async Task Revoke_RequiresReason()
    {
        var d = Seed(CompetitionStatus.GRADED);
        using var db = d.Db;
        var issued = Issue(db, d.First, d.Entries["best1"]);
        var service = Service(db);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => service.RevokeAsync(issued.Id, "   ", _staffId));
        Assert.Equal(RevokeAwardVm.ReasonRequiredMessage, ex.Message);
        Assert.Equal(1, await db.AwardsIssued.CountAsync());
        await Assert.ThrowsAsync<BusinessException>(() => service.RevokeAsync(issued.Id, new string('a', 501), _staffId));

        var competitionId = await service.RevokeAsync(issued.Id, "Entry found to be copied", _staffId);
        Assert.Equal(d.Competition.Id, competitionId);
        Assert.Equal(0, await db.AwardsIssued.CountAsync());
        Assert.Contains((await service.GetForCreateAsync(d.Competition.Id)).Awards, a => a.Id == d.First.Id);
    }

    /// <summary>Unknown issued award → NotFoundException for edit, save and revoke.</summary>
    [Fact]
    public async Task Unknown_NotFound()
    {
        var d = Seed();
        using var db = d.Db;
        var service = Service(db);

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetForEditAsync(999));
        await Assert.ThrowsAsync<NotFoundException>(() => service.UpdateAsync(999, Form(d)));
        await Assert.ThrowsAsync<NotFoundException>(() => service.RevokeAsync(999, "reason", _staffId));
    }

    // ------------------------------------------------------------------ Search

    /// <summary>F-M08-04: filters by competition, student name / code, award name and issued date range (both days included).</summary>
    [Fact]
    public async Task Search_Filters()
    {
        var d = Seed();
        using var db = d.Db;
        Issue(db, d.First, d.Entries["best1"], Today.AddDays(-3), "Bright");
        Issue(db, d.Mention, d.Entries["best2"], Today.AddDays(-1));
        Issue(db, d.Mention, d.Entries["best3"], Today);
        var service = Service(db);

        var all = await service.SearchAsync(new AwardIssuedSearchVm { CompetitionId = d.Competition.Id });
        Assert.Equal(3, all.TotalCount);
        Assert.Equal(d.Entries["best3"].Id, all.Items[0].SubmissionId);
        var row = all.Items.Single(i => i.AwardName == "First Prize");
        Assert.Equal("Nguyen Van An", row.StudentName);
        Assert.Equal("Painting best1", row.SubmissionTitle);
        Assert.Equal(250m, row.PrizeValue);
        Assert.Equal("Le Minh Hoang", row.IssuedByName);
        Assert.Equal("Bright", row.Remarks);

        Assert.Single((await service.SearchAsync(new AwardIssuedSearchVm { Student = "Binh" })).Items);
        Assert.Single((await service.SearchAsync(new AwardIssuedSearchVm { Student = "HV0003" })).Items);
        Assert.Equal(2, (await service.SearchAsync(new AwardIssuedSearchVm { Award = "Mention" })).TotalCount);
        Assert.Equal(2, (await service.SearchAsync(new AwardIssuedSearchVm { From = Today.AddDays(-1), To = Today })).TotalCount);
        Assert.Equal(0, (await service.SearchAsync(new AwardIssuedSearchVm { CompetitionId = 999 })).TotalCount);
    }

    /// <summary>TC-M10-03 (same rule as the reports): From after To is refused with the shared message.</summary>
    [Fact]
    public async Task Search_FromAfterTo_Refused()
    {
        var d = Seed();
        using var db = d.Db;
        var service = Service(db);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => service.SearchAsync(new AwardIssuedSearchVm { From = Today, To = Today.AddDays(-1) }));
        Assert.Equal(DateTimeHelper.InvalidDateRangeMessage, ex.Message);
    }
}
