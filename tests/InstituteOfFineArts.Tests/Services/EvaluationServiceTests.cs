using InstituteOfFineArts.Web.Data;
using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Helpers;
using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.Services.Implementations;
using InstituteOfFineArts.Web.ViewModels.Staff.Evaluations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace InstituteOfFineArts.Tests.Services;

/// <summary>
/// EvaluationService (task BE1-10, F-M07-03..05/08): evaluate only when CLOSED (BR-21), GRADED read only,
/// one evaluation per submission with the evaluator overwritten (BR-22), BR-23 comments, late entry only
/// DISQUALIFIED (BR-14, TC-M07-04), delete blocked by award / exhibition entry / GRADED, unknown entry → not found.
/// </summary>
public class EvaluationServiceTests
{
    /// <summary>Today in the institute time zone.</summary>
    private static readonly DateOnly Today = DateTimeHelper.Today;

    /// <summary>staff_profile.id of staff01 in the test database.</summary>
    private long _staff1;

    /// <summary>staff_profile.id of staff02 in the test database.</summary>
    private long _staff2;

    /// <summary>student_profile.id in the test database.</summary>
    private long _studentId;

    /// <summary>Empty in-memory database with two staff members and one student.</summary>
    private AppDbContext CreateDb()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var staff1 = new StaffProfile
        {
            StaffCode = "NV0003", JoinedDate = new DateOnly(2024, 1, 1),
            Account = new Account { Username = "staff01", FullName = "Le Minh Hoang", Email = "s1@ifa.local", Role = Role.STAFF, PasswordHash = "x" },
        };
        var staff2 = new StaffProfile
        {
            StaffCode = "NV0004", JoinedDate = new DateOnly(2024, 1, 1),
            Account = new Account { Username = "staff02", FullName = "Pham Thu Ha", Email = "s2@ifa.local", Role = Role.STAFF, PasswordHash = "x" },
        };
        var student = new StudentProfile
        {
            StudentCode = "HV0001", EnrollmentDate = new DateOnly(2025, 9, 1), CourseName = "Painting",
            Account = new Account { Username = "student01", FullName = "Nguyen Van An", Email = "st@ifa.local", Role = Role.STUDENT, PasswordHash = "x" },
        };
        db.StaffProfiles.AddRange(staff1, staff2);
        db.StudentProfiles.Add(student);
        db.SaveChanges();
        _staff1 = staff1.Id;
        _staff2 = staff2.Id;
        _studentId = student.Id;
        return db;
    }

    /// <summary>Service under test.</summary>
    private static EvaluationService Service(AppDbContext db) => new(db, NullLogger<EvaluationService>.Instance);

    /// <summary>
    /// Adds a competition in the given calculated status with one entry and returns the entry.
    /// <paramref name="late"/> = submitted the day after the end date (BR-14).
    /// </summary>
    private Submission AddEntry(AppDbContext db, CompetitionStatus status = CompetitionStatus.CLOSED, bool late = false)
    {
        // Dates that make CompetitionStatusResolver return the wanted status (GRADED is stored)
        var (start, end) = status switch
        {
            CompetitionStatus.UPCOMING => (Today.AddDays(5), Today.AddDays(30)),
            CompetitionStatus.ONGOING => (Today.AddDays(-5), Today.AddDays(5)),
            _ => (Today.AddDays(-30), Today.AddDays(-5)),
        };
        var c = new Competition
        {
            Code = "X" + Guid.NewGuid().ToString("N")[..8], Title = "City at Night", StartDate = start, EndDate = end,
            Status = status == CompetitionStatus.GRADED ? CompetitionStatus.GRADED : CompetitionStatus.UPCOMING, CreatedBy = _staff1,
        };
        db.Competitions.Add(c);
        db.SaveChanges();

        var submittedAt = late ? end.AddDays(1).ToDateTime(new TimeOnly(9, 0)) : start.ToDateTime(new TimeOnly(10, 0));
        var s = new Submission
        {
            CompetitionId = c.Id, StudentId = _studentId, Title = "Neon Street", ImagePath = "p.jpg", OriginalFileName = "p.jpg",
            FileSize = 10, TextContent = "A poem", Description = "Why I painted it", SubmittedAt = submittedAt,
        };
        db.Submissions.Add(s);
        db.SaveChanges();
        return s;
    }

    /// <summary>Adds an evaluation made by staff01.</summary>
    private void AddEvaluation(AppDbContext db, Submission s, Rating rating = Rating.GOOD)
    {
        db.Evaluations.Add(new Evaluation
        {
            SubmissionId = s.Id, EvaluatedBy = _staff1, Rating = rating, PositivePoints = "Nice colors",
            NegativePoints = rating == Rating.DISQUALIFIED ? "Late" : null, EvaluatedAt = DateTime.Now.AddDays(-1),
        });
        db.SaveChanges();
    }

    /// <summary>Valid form.</summary>
    private static EvaluationFormVm Form(Rating? rating = Rating.BEST, string? positive = "Strong composition",
        string? negative = "Dark corners", string? improvement = "Perspective") => new()
    {
        Rating = rating, PositivePoints = positive, NegativePoints = negative, ImprovementAreas = improvement,
    };

    /// <summary>TC-M07-02: CLOSED competition → evaluation created with rating, trimmed comments, evaluator and time.</summary>
    [Fact]
    public async Task Create_InClosed_Saved()
    {
        using var db = CreateDb();
        var s = AddEntry(db);
        var before = DateTimeHelper.Now.AddSeconds(-1);

        var created = await Service(db).SaveAsync(s.Id, _staff1, Form(positive: "  Strong composition  ", improvement: "   "));

        Assert.True(created);
        var e = await db.Evaluations.SingleAsync();
        Assert.Equal(s.Id, e.SubmissionId);
        Assert.Equal(Rating.BEST, e.Rating);
        Assert.Equal("Strong composition", e.PositivePoints);
        Assert.Equal("Dark corners", e.NegativePoints);
        Assert.Null(e.ImprovementAreas);
        Assert.Equal(_staff1, e.EvaluatedBy);
        Assert.True(e.EvaluatedAt >= before);
    }

    /// <summary>BR-21: UPCOMING / ONGOING → "only after the deadline"; GRADED → read only. Nothing is saved.</summary>
    [Theory]
    [InlineData(CompetitionStatus.UPCOMING, EvaluationService.NotClosedMessage)]
    [InlineData(CompetitionStatus.ONGOING, EvaluationService.NotClosedMessage)]
    [InlineData(CompetitionStatus.GRADED, EvaluationService.GradedReadOnlyMessage)]
    public async Task Save_NotClosed_Refused(CompetitionStatus status, string message)
    {
        using var db = CreateDb();
        var s = AddEntry(db, status);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => Service(db).SaveAsync(s.Id, _staff1, Form()));
        Assert.Equal(message, ex.Message);
        Assert.Null(ex.Field);
        Assert.Equal(0, await db.Evaluations.CountAsync());

        // The page is still shown, read only with the same reason
        var page = await Service(db).GetPageAsync(s.Id);
        Assert.False(page.CanEvaluate);
        Assert.Equal(message, page.ReadOnlyReason);
        Assert.Equal(status, page.CompetitionStatus);
    }

    /// <summary>GRADED with an evaluation → editing refused, the evaluation is unchanged and cannot be deleted.</summary>
    [Fact]
    public async Task Graded_ExistingEvaluation_ReadOnly()
    {
        using var db = CreateDb();
        var s = AddEntry(db, CompetitionStatus.GRADED);
        AddEvaluation(db, s);
        var service = Service(db);

        await Assert.ThrowsAsync<BusinessException>(() => service.SaveAsync(s.Id, _staff2, Form(Rating.NORMAL)));
        var ex = await Assert.ThrowsAsync<BusinessException>(() => service.DeleteAsync(s.Id));
        Assert.Equal(EvaluationService.GradedReadOnlyMessage, ex.Message);

        var e = await db.Evaluations.AsNoTracking().SingleAsync();
        Assert.Equal(Rating.GOOD, e.Rating);
        Assert.Equal(_staff1, e.EvaluatedBy);
        var page = await service.GetPageAsync(s.Id);
        Assert.True(page.HasEvaluation);
        Assert.False(page.CanDelete);
        Assert.Equal(Rating.GOOD, page.Form.Rating);
    }

    /// <summary>BR-23: DISQUALIFIED without a reason (blank Negative points) → refused on NegativePoints.</summary>
    [Fact]
    public async Task Save_DisqualifiedWithoutReason_Refused()
    {
        using var db = CreateDb();
        var s = AddEntry(db);

        var ex = await Assert.ThrowsAsync<BusinessException>(() =>
            Service(db).SaveAsync(s.Id, _staff1, Form(Rating.DISQUALIFIED, positive: null, negative: "   ")));
        Assert.Equal(nameof(EvaluationFormVm.NegativePoints), ex.Field);
        Assert.Equal(EvaluationFormVm.DisqualifiedReasonMessage, ex.Message);
        Assert.Equal(0, await db.Evaluations.CountAsync());

        // With the reason it is accepted, positive points not needed
        await Service(db).SaveAsync(s.Id, _staff1, Form(Rating.DISQUALIFIED, positive: null, negative: "Off topic"));
        Assert.Equal(Rating.DISQUALIFIED, (await db.Evaluations.SingleAsync()).Rating);
    }

    /// <summary>BR-23: every rating other than DISQUALIFIED needs Positive points.</summary>
    [Theory]
    [InlineData(Rating.BEST)]
    [InlineData(Rating.BETTER)]
    [InlineData(Rating.GOOD)]
    [InlineData(Rating.MODERATE)]
    [InlineData(Rating.NORMAL)]
    public async Task Save_WithoutPositivePoints_Refused(Rating rating)
    {
        using var db = CreateDb();
        var s = AddEntry(db);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => Service(db).SaveAsync(s.Id, _staff1, Form(rating, positive: " ")));
        Assert.Equal(nameof(EvaluationFormVm.PositivePoints), ex.Field);
        Assert.Equal(EvaluationFormVm.PositivePointsMessage, ex.Message);
    }

    /// <summary>No rating, an undefined rating, or a comment over 2000 characters → refused on the field.</summary>
    [Fact]
    public async Task Save_InvalidValues_Refused()
    {
        using var db = CreateDb();
        var s = AddEntry(db);
        var service = Service(db);

        var noRating = await Assert.ThrowsAsync<BusinessException>(() => service.SaveAsync(s.Id, _staff1, Form(rating: null)));
        Assert.Equal(nameof(EvaluationFormVm.Rating), noRating.Field);
        var badRating = await Assert.ThrowsAsync<BusinessException>(() => service.SaveAsync(s.Id, _staff1, Form(rating: (Rating)99)));
        Assert.Equal(nameof(EvaluationFormVm.Rating), badRating.Field);
        var tooLong = await Assert.ThrowsAsync<BusinessException>(() => service.SaveAsync(s.Id, _staff1, Form(improvement: new string('a', 2001))));
        Assert.Equal(nameof(EvaluationFormVm.ImprovementAreas), tooLong.Field);
        Assert.Equal(0, await db.Evaluations.CountAsync());
    }

    /// <summary>TC-M07-03 / BR-22: saving again updates the same row (no duplicate) and overwrites evaluator and time.</summary>
    [Fact]
    public async Task Save_Second_UpdatesAndOverwritesEvaluator()
    {
        using var db = CreateDb();
        var s = AddEntry(db);
        AddEvaluation(db, s, Rating.GOOD);
        var oldTime = (await db.Evaluations.AsNoTracking().SingleAsync()).EvaluatedAt;

        var created = await Service(db).SaveAsync(s.Id, _staff2, Form(Rating.BETTER, positive: "Better light"));

        Assert.False(created);
        var e = await db.Evaluations.AsNoTracking().SingleAsync();
        Assert.Equal(Rating.BETTER, e.Rating);
        Assert.Equal("Better light", e.PositivePoints);
        Assert.Equal(_staff2, e.EvaluatedBy);
        Assert.True(e.EvaluatedAt > oldTime);

        // The page shows the last evaluator
        var page = await Service(db).GetPageAsync(s.Id);
        Assert.Equal("Pham Thu Ha", page.EvaluatedByName);
        Assert.True(page.CanDelete);
    }

    /// <summary>BA-07: the page carries the version of the evaluation; saving with that version works, saving with an
    /// older one (someone changed it meanwhile) is refused and the other staff member's evaluation is kept.</summary>
    [Fact]
    public async Task Save_ChangedMeanwhile_Refused()
    {
        using var db = CreateDb();
        var s = AddEntry(db);
        AddEvaluation(db, s, Rating.GOOD);
        var page = await Service(db).GetPageAsync(s.Id);
        Assert.False(string.IsNullOrEmpty(page.Form.Version));

        var stale = Form(Rating.BEST, positive: "Mine");
        var current = await db.Evaluations.AsNoTracking().SingleAsync();
        stale.Version = EvaluationService.Version(current.CreatedAt.AddMinutes(-5), null);
        var ex = await Assert.ThrowsAsync<BusinessException>(() => Service(db).SaveAsync(s.Id, _staff2, stale));
        Assert.Equal(EvaluationService.ChangedMeanwhileMessage, ex.Message);
        Assert.Equal(Rating.GOOD, (await db.Evaluations.AsNoTracking().SingleAsync()).Rating);

        var fresh = Form(Rating.BETTER, positive: "Better light");
        fresh.Version = page.Form.Version;
        await Service(db).SaveAsync(s.Id, _staff2, fresh);
        Assert.Equal(Rating.BETTER, (await db.Evaluations.AsNoTracking().SingleAsync()).Rating);
    }

    /// <summary>BA-07: the page was opened before anyone evaluated (empty version) but an evaluation exists now → refused;
    /// without a version (callers that check it themselves) the save is not checked.</summary>
    [Fact]
    public async Task Save_EvaluatedMeanwhile_RefusedUnlessUnchecked()
    {
        using var db = CreateDb();
        var s = AddEntry(db);
        var page = await Service(db).GetPageAsync(s.Id);
        Assert.Equal(string.Empty, page.Form.Version);
        AddEvaluation(db, s, Rating.GOOD);

        var form = Form(Rating.BEST);
        form.Version = page.Form.Version;
        var ex = await Assert.ThrowsAsync<BusinessException>(() => Service(db).SaveAsync(s.Id, _staff2, form));
        Assert.Equal(EvaluationService.ConcurrentCreateMessage, ex.Message);

        form.Version = null;
        Assert.False(await Service(db).SaveAsync(s.Id, _staff2, form));
        Assert.Equal(Rating.BEST, (await db.Evaluations.AsNoTracking().SingleAsync()).Rating);
    }

    /// <summary>TC-M07-04 / BR-14: a late entry only accepts DISQUALIFIED; the page offers and pre-selects only DISQUALIFIED.</summary>
    [Fact]
    public async Task LateEntry_OnlyDisqualified()
    {
        using var db = CreateDb();
        var s = AddEntry(db, late: true);
        var service = Service(db);

        var page = await service.GetPageAsync(s.Id);
        Assert.True(page.IsLate);
        Assert.Equal([Rating.DISQUALIFIED], page.AllowedRatings);
        Assert.Equal(Rating.DISQUALIFIED, page.Form.Rating);
        Assert.False(string.IsNullOrWhiteSpace(page.Form.NegativePoints));

        var ex = await Assert.ThrowsAsync<BusinessException>(() => service.SaveAsync(s.Id, _staff1, Form(Rating.BEST)));
        Assert.Equal(EvaluationService.LateOnlyDisqualifiedMessage, ex.Message);
        Assert.Equal(nameof(EvaluationFormVm.Rating), ex.Field);

        await service.SaveAsync(s.Id, _staff1, Form(Rating.DISQUALIFIED, positive: null, negative: "Submitted after the deadline"));
        Assert.Equal(Rating.DISQUALIFIED, (await db.Evaluations.SingleAsync()).Rating);
    }

    /// <summary>An entry submitted at 23:59 of the end date is on time: every rating is offered and accepted.</summary>
    [Fact]
    public async Task OnTimeEntry_LastMinute_NotLate()
    {
        using var db = CreateDb();
        var s = AddEntry(db);
        var c = await db.Competitions.SingleAsync();
        s.SubmittedAt = c.EndDate.ToDateTime(new TimeOnly(23, 59, 59));
        db.SaveChanges();

        var page = await Service(db).GetPageAsync(s.Id);
        Assert.False(page.IsLate);
        Assert.Equal(6, page.AllowedRatings.Count);
        Assert.Null(page.Form.Rating);
        Assert.True(await Service(db).SaveAsync(s.Id, _staff1, Form(Rating.NORMAL)));
    }

    /// <summary>F-M07-05: CLOSED entry without award / exhibition entry → evaluation deleted.</summary>
    [Fact]
    public async Task Delete_Valid()
    {
        using var db = CreateDb();
        var s = AddEntry(db);
        AddEvaluation(db, s);

        await Service(db).DeleteAsync(s.Id);
        Assert.Equal(0, await db.Evaluations.CountAsync());

        // Nothing left to delete → clear message
        var ex = await Assert.ThrowsAsync<BusinessException>(() => Service(db).DeleteAsync(s.Id));
        Assert.Equal(EvaluationService.NoEvaluationMessage, ex.Message);
    }

    /// <summary>Delete blocked when the entry has an award.</summary>
    [Fact]
    public async Task Delete_WithAward_Blocked()
    {
        using var db = CreateDb();
        var s = AddEntry(db);
        AddEvaluation(db, s, Rating.BEST);
        var award = new CompetitionAward { CompetitionId = s.CompetitionId, AwardName = "First Prize", Rank = 1, PrizeValue = 100m, Quantity = 1 };
        db.CompetitionAwards.Add(award);
        db.SaveChanges();
        db.AwardsIssued.Add(new AwardIssued { CompetitionAwardId = award.Id, SubmissionId = s.Id, IssuedBy = _staff1, IssuedDate = Today });
        db.SaveChanges();

        var ex = await Assert.ThrowsAsync<BusinessException>(() => Service(db).DeleteAsync(s.Id));
        Assert.Equal(EvaluationService.AwardedDeleteMessage, ex.Message);
        Assert.Equal(1, await db.Evaluations.CountAsync());
        Assert.False((await Service(db).GetPageAsync(s.Id)).CanDelete);
    }

    /// <summary>Delete blocked when the entry has been sent to an exhibition.</summary>
    [Fact]
    public async Task Delete_WithExhibitionEntry_Blocked()
    {
        using var db = CreateDb();
        var s = AddEntry(db);
        AddEvaluation(db, s, Rating.BEST);
        var exhibition = new Exhibition { Name = "Spring Show", Venue = "Hall A", City = "Hanoi", StartDate = Today, EndDate = Today.AddDays(10), CreatedBy = _staff1 };
        db.Exhibitions.Add(exhibition);
        db.SaveChanges();
        db.ExhibitionEntries.Add(new ExhibitionEntry { ExhibitionId = exhibition.Id, SubmissionId = s.Id, AskingPrice = 100m, ProposedBy = 1 });
        db.SaveChanges();

        var ex = await Assert.ThrowsAsync<BusinessException>(() => Service(db).DeleteAsync(s.Id));
        Assert.Equal(EvaluationService.ExhibitedDeleteMessage, ex.Message);
        Assert.Equal(1, await db.Evaluations.CountAsync());
    }

    /// <summary>Unknown submission → NotFoundException for the page, save and delete.</summary>
    [Fact]
    public async Task UnknownSubmission_NotFound()
    {
        using var db = CreateDb();
        var service = Service(db);

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetPageAsync(999));
        await Assert.ThrowsAsync<NotFoundException>(() => service.SaveAsync(999, _staff1, Form()));
        await Assert.ThrowsAsync<NotFoundException>(() => service.DeleteAsync(999));
    }

    /// <summary>BR-23 in the form: DISQUALIFIED without reason and other ratings without positive points are invalid.</summary>
    [Fact]
    public void FormVm_Validate_Br23()
    {
        var context = new System.ComponentModel.DataAnnotations.ValidationContext(new object());

        Assert.Contains(Form(Rating.DISQUALIFIED, positive: null, negative: null).Validate(context),
            r => r.MemberNames.Contains(nameof(EvaluationFormVm.NegativePoints)));
        Assert.Contains(Form(Rating.GOOD, positive: "").Validate(context),
            r => r.MemberNames.Contains(nameof(EvaluationFormVm.PositivePoints)));
        Assert.Empty(Form().Validate(context));
    }
}
