using System.Net;
using System.Security.Claims;
using InstituteOfFineArts.Web.Data;
using InstituteOfFineArts.Web.Helpers;
using InstituteOfFineArts.Web.Infrastructure.Audit;
using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.Services.Implementations;
using InstituteOfFineArts.Web.ViewModels.Admin.AuditLog;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace InstituteOfFineArts.Tests.Services;

/// <summary>
/// Audit log by object (TODO 3b): AuditTargetService reads the object of a request from its path and finds where it
/// belongs (competition, exhibition, entry, account) with a readable sentence; AuditLogService keeps those links and
/// gives the history of one competition / exhibition / entry / account (login history included).
/// </summary>
public class AuditHistoryTests : IDisposable
{
    private readonly DbContextOptions<AppDbContext> _options =
        new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;

    private readonly AppDbContext _db;
    private readonly Competition _competition;
    private readonly CompetitionAward _award;
    private readonly Submission _entry;
    private readonly Exhibition _exhibition;
    private readonly ExhibitionEntry _artwork;
    private readonly StudentProfile _student;

    public AuditHistoryTests()
    {
        _db = new AppDbContext(_options);
        var today = DateTimeHelper.Today;
        _student = new StudentProfile
        {
            StudentCode = "HV0001", EnrollmentDate = today, CourseName = "Painting", ClassName = "PT01",
            Account = new Account { Username = "student01", FullName = "Nguyen Minh Khoa", Email = "s1@ifa.local", Role = Role.STUDENT, PasswordHash = "x" },
        };
        var staff = new StaffProfile
        {
            StaffCode = "NV0002", JoinedDate = today,
            Account = new Account { Username = "staff01", FullName = "Tran Bich Ngoc", Email = "st@ifa.local", Role = Role.STAFF, PasswordHash = "x" },
        };
        _db.AddRange(_student, staff);
        _db.SaveChanges();

        _competition = new Competition { Code = "CT01", Title = "Wildlife", StartDate = today.AddDays(-30), EndDate = today.AddDays(-10), CreatedBy = staff.Id };
        _award = new CompetitionAward { AwardName = "First Prize", Rank = 1, PrizeValue = 100m, Quantity = 1 };
        _competition.Awards.Add(_award);
        _db.Competitions.Add(_competition);
        _db.SaveChanges();
        _entry = new Submission { CompetitionId = _competition.Id, StudentId = _student.Id, Title = "Old Tiger", ImagePath = "a.jpg", OriginalFileName = "a.jpg", TextContent = "t", Description = "d" };
        _exhibition = new Exhibition { Name = "Hanoi Spring", Venue = "Hall A", City = "Hanoi", StartDate = today.AddDays(-5), EndDate = today.AddDays(5), CreatedBy = staff.Id };
        _db.AddRange(_entry, _exhibition);
        _db.SaveChanges();
        _artwork = new ExhibitionEntry { ExhibitionId = _exhibition.Id, SubmissionId = _entry.Id, AskingPrice = 200m, Status = EntryStatus.ACCEPTED, ProposedBy = staff.AccountId };
        _db.ExhibitionEntries.Add(_artwork);
        _db.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    private AuditTargetService Targets => new(_db);

    // ------------------------------------------------------------------ AuditTargetService

    /// <summary>A sale of an artwork belongs to its exhibition, its entry and the entry's competition.</summary>
    [Fact]
    public async Task Target_ArtworkSold_LinkedToExhibitionEntryAndCompetition()
    {
        var t = await Targets.ResolveAsync("POST", $"/staff/exhibition-entries/{_artwork.Id}/sold");

        Assert.Equal("ExhibitionEntry", t.Kind);
        Assert.Equal(_exhibition.Id, t.ExhibitionId);
        Assert.Equal(_entry.Id, t.SubmissionId);
        Assert.Equal(_competition.Id, t.CompetitionId);
        Assert.Equal(AuditCategory.SALE, t.Category);
        Assert.Equal("Recorded the sale of the artwork “Old Tiger”", t.Summary);
    }

    /// <summary>Paths of the web and of the API, each with its sentence and kind of work.</summary>
    [Fact]
    public async Task Target_Sentences()
    {
        var c = _competition.Id;
        var s = _entry.Id;

        var edit = await Targets.ResolveAsync("POST", $"/staff/competitions/{c}/edit");
        Assert.Equal(("Edited the competition “Wildlife”", AuditCategory.COMPETITION), (edit.Summary, edit.Category));

        var addAward = await Targets.ResolveAsync("POST", $"/staff/competitions/{c}/awards/new");
        Assert.Equal(("Added an award to the competition “Wildlife”", AuditCategory.COMPETITION), (addAward.Summary, addAward.Category));

        var editAward = await Targets.ResolveAsync("POST", $"/staff/competitions/{c}/awards/{_award.Id}/edit");
        Assert.Equal("Edited the award “First Prize”", editAward.Summary);
        Assert.Equal(c, editAward.CompetitionId);

        var grade = await Targets.ResolveAsync("POST", $"/teacher/evaluations/{s}");
        Assert.Equal(("Graded the entry “Old Tiger”", AuditCategory.GRADING), (grade.Summary, grade.Category));
        Assert.Equal(c, grade.CompetitionId);

        var ungrade = await Targets.ResolveAsync("POST", $"/teacher/evaluations/{s}/delete");
        Assert.Equal("Deleted the evaluation of the entry “Old Tiger”", ungrade.Summary);

        var apiGrade = await Targets.ResolveAsync("PUT", $"/api/staff/submissions/{s}/evaluation");
        Assert.Equal(("Graded the entry “Old Tiger”", AuditCategory.GRADING), (apiGrade.Summary, apiGrade.Category));

        var publish = await Targets.ResolveAsync("POST", $"/staff/competitions/{c}/publish");
        Assert.Equal(("Published the results of the competition “Wildlife”", AuditCategory.AWARD), (publish.Summary, publish.Category));

        var submit = await Targets.ResolveAsync("POST", $"/api/competitions/{c}/submissions");
        Assert.Equal(("Submitted an entry to the competition “Wildlife”", AuditCategory.SUBMISSION), (submit.Summary, submit.Category));

        var lockStudent = await Targets.ResolveAsync("POST", $"/admin/students/{_student.Id}/lock");
        Assert.Equal(("Locked the student account “Nguyen Minh Khoa”", AuditCategory.ACCOUNT), (lockStudent.Summary, lockStudent.Category));
        Assert.Equal(_student.AccountId, lockStudent.TargetAccountId);
    }

    /// <summary>A create has no id yet: the parent comes from the form, the name from the created id.</summary>
    [Fact]
    public async Task Target_Create_ParentFromForm_NameFromCreatedId()
    {
        var t = await Targets.ResolveAsync("POST", "/staff/exhibition-entries/new",
            new Dictionary<string, long> { ["ExhibitionId"] = _exhibition.Id, ["SubmissionId"] = _entry.Id });
        Assert.Equal("Created the artwork", t.Summary);
        Assert.Equal(_exhibition.Id, t.ExhibitionId);

        var created = await Targets.WithCreatedIdAsync(t, _artwork.Id);
        Assert.Equal("Created the artwork “Old Tiger”", created.Summary);
        Assert.Equal(_competition.Id, created.CompetitionId);
    }

    // ------------------------------------------------------------------ AuditLogService history

    private AuditLogService Log(HttpContext? http = null)
        => new(_db, _options, new HttpContextAccessor { HttpContext = http }, NullLogger<AuditLogService>.Instance);

    private static HttpContext Signed(long accountId, string username, string role)
    {
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, accountId.ToString()), new Claim(ClaimTypes.Name, username), new Claim(ClaimTypes.Role, role),
            }, "Test")),
        };
        http.Connection.RemoteIpAddress = IPAddress.Loopback;
        return http;
    }

    /// <summary>The competition's history holds its own rows and those of its entries; another competition's rows are not there.</summary>
    [Fact]
    public async Task History_OfOneCompetition()
    {
        var log = Log(Signed(9, "teacher01", RoleNames.Teacher));
        await log.RecordAsync(new AuditEntry { Action = "Evaluations/Index", Category = AuditCategory.GRADING, Summary = "Graded the entry", CompetitionId = _competition.Id, SubmissionId = _entry.Id });
        await log.RecordAsync(new AuditEntry { Action = "Competitions/Edit", Category = AuditCategory.COMPETITION, Summary = "Edited", CompetitionId = _competition.Id, Details = "Staff: POST … succeeded · Changes: End date: a → b | Rules changed" });
        await log.RecordAsync(new AuditEntry { Action = "Competitions/Edit", Category = AuditCategory.COMPETITION, Summary = "Edited another", CompetitionId = 999 });

        var all = await log.GetHistoryAsync(new AuditHistoryQuery { CompetitionId = _competition.Id });
        var grading = await log.GetHistoryAsync(new AuditHistoryQuery { CompetitionId = _competition.Id, Category = AuditCategory.GRADING });
        var ofEntry = await log.GetHistoryAsync(new AuditHistoryQuery { SubmissionId = _entry.Id });

        Assert.Equal(2, all.TotalCount);
        Assert.Equal(new[] { "End date: a → b", "Rules changed" }, all.Items.Single(r => r.Action == "Competitions/Edit").Changes);
        Assert.Equal("Graded the entry", Assert.Single(grading.Items).Text);
        Assert.Single(ofEntry.Items);
    }

    /// <summary>
    /// Login history of an account: its sign-ins and sign-outs (web and app), and the refused sign-ins with its user
    /// name – those have no user yet, so they are linked to the account that was tried.
    /// </summary>
    [Fact]
    public async Task History_LoginTab_IncludesRefusedSignIns()
    {
        var accountId = _student.AccountId;
        var log = Log();
        await log.LogAsAsync(accountId, "student01", AuditLogService.LoginAction);
        await log.LogAsAsync(null, "student01", AuditLogService.LoginFailedAction, "Invalid username or password");
        await log.LogAsAsync(null, "nobody", AuditLogService.LoginFailedAction, "Invalid username or password");
        await Log(Signed(accountId, "student01", RoleNames.Student)).RecordAsync(new AuditEntry { Action = "Submissions/Edit", Category = AuditCategory.SUBMISSION, Summary = "Edited the entry" });

        var logins = await log.GetHistoryAsync(new AuditHistoryQuery { AccountId = accountId, Category = AuditCategory.LOGIN });
        var activity = await log.GetHistoryAsync(new AuditHistoryQuery { ActorAccountId = accountId, ExceptCategory = AuditCategory.LOGIN });

        Assert.Equal(2, logins.TotalCount);
        Assert.Contains(logins.Items, r => r.Failed && r.Text == "Sign-in refused");
        Assert.Contains(logins.Items, r => r.Text == "Signed in");
        Assert.Equal("Edited the entry", Assert.Single(activity.Items).Text);
    }

    /// <summary>Opening the same entry again within the window is one visit; another entry is a new row.</summary>
    [Fact]
    public async Task Views_Deduplicated()
    {
        var log = Log(Signed(9, "teacher01", RoleNames.Teacher));
        AuditEntry View(long id) => new()
        {
            Action = "VIEW", Category = AuditCategory.SUBMISSION, Entity = "Submission", EntityId = id, SubmissionId = id,
            DedupeWindow = TimeSpan.FromMinutes(10),
        };

        await log.RecordAsync(View(_entry.Id));
        await log.RecordAsync(View(_entry.Id));
        await log.RecordAsync(View(_entry.Id + 1));

        Assert.Equal(2, await _db.AuditLogs.CountAsync());
    }

    /// <summary>The admin log page filters by kind of work and by competition.</summary>
    [Fact]
    public async Task Search_ByCategoryAndCompetition()
    {
        var log = Log(Signed(9, "staff01", RoleNames.Staff));
        await log.RecordAsync(new AuditEntry { Action = "ExhibitionEntries/Sold", Category = AuditCategory.SALE, Summary = "Recorded the sale", CompetitionId = _competition.Id, ExhibitionId = _exhibition.Id });
        await log.RecordAsync(new AuditEntry { Action = "Competitions/Edit", Category = AuditCategory.COMPETITION, Summary = "Edited", CompetitionId = _competition.Id });

        Assert.Equal(1, (await log.SearchAsync(new AuditLogSearchVm { Category = AuditCategory.SALE })).TotalCount);
        Assert.Equal(2, (await log.SearchAsync(new AuditLogSearchVm { CompetitionId = _competition.Id })).TotalCount);
        Assert.Equal(1, (await log.SearchAsync(new AuditLogSearchVm { ExhibitionId = _exhibition.Id })).TotalCount);
        Assert.Equal(1, (await log.SearchAsync(new AuditLogSearchVm { Activity = "sale" })).TotalCount);
    }
}
