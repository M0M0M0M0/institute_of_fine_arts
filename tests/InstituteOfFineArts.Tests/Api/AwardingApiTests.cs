using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using InstituteOfFineArts.Web.Controllers.Api;
using InstituteOfFineArts.Web.Controllers.Api.Dto;
using InstituteOfFineArts.Web.Data;
using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Helpers;
using InstituteOfFineArts.Web.Infrastructure.Api;
using InstituteOfFineArts.Web.Infrastructure.Security;
using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.Services.Implementations;
using InstituteOfFineArts.Web.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace InstituteOfFineArts.Tests.Api;

/// <summary>Mobile awarding API (AwardingApiController): JSON shape, issue / edit / delete / revoke / publish rules.</summary>
public class AwardingApiTests
{
    private static readonly DateOnly Today = DateTimeHelper.Today;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private sealed record Ctx(AwardingApiController Api, AppDbContext Db, Competition Competition,
        CompetitionAward First, CompetitionAward Mention, Dictionary<string, Submission> Entries);

    /// <summary>CLOSED (or GRADED) competition with First Prize x1, Mention x2, entries best1, best2, good, none.</summary>
    private static Ctx Seed(bool graded = false, bool withUnevaluated = false)
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var staff = new StaffProfile
        {
            StaffCode = "NV0003", JoinedDate = new DateOnly(2024, 1, 1),
            Account = new Account { Username = "staff01", FullName = "Le Minh Hoang", Email = "s1@ifa.local", Role = Role.STAFF, PasswordHash = "x" },
        };
        db.StaffProfiles.Add(staff);
        db.SaveChanges();
        var c = new Competition
        {
            Code = "X1", Title = "City at Night", StartDate = Today.AddDays(-30), EndDate = Today.AddDays(-5), CreatedBy = staff.Id,
            Status = graded ? CompetitionStatus.GRADED : CompetitionStatus.UPCOMING,
            PublishedAt = graded ? DateTime.Now : null,
        };
        db.Competitions.Add(c);
        db.SaveChanges();
        var first = new CompetitionAward { CompetitionId = c.Id, AwardName = "First Prize", Rank = 1, PrizeValue = 250m, Quantity = 1, PrizeDescription = "Trophy" };
        var mention = new CompetitionAward { CompetitionId = c.Id, AwardName = "Honorable Mention", Rank = 4, PrizeValue = 50m, Quantity = 2 };
        db.CompetitionAwards.AddRange(first, mention);
        db.SaveChanges();

        var entries = new Dictionary<string, Submission>();
        var specs = new List<(string Key, Rating? Rating)> { ("best1", Rating.BEST), ("best2", Rating.BEST), ("good", Rating.GOOD) };
        if (withUnevaluated) specs.Add(("none", null));
        var n = 0;
        foreach (var (key, rating) in specs)
        {
            n++;
            var student = new StudentProfile
            {
                StudentCode = $"HV{n:D4}", EnrollmentDate = new DateOnly(2025, 9, 1), CourseName = "Painting",
                Account = new Account { Username = $"student{n:D2}", FullName = $"Student {key}", Email = $"st{n}@ifa.local", Role = Role.STUDENT, PasswordHash = "x" },
            };
            db.StudentProfiles.Add(student);
            db.SaveChanges();
            var s = new Submission
            {
                CompetitionId = c.Id, StudentId = student.Id, Title = $"Painting {key}", ImagePath = "p.jpg", OriginalFileName = "p.jpg",
                FileSize = 10, TextContent = "A poem", Description = "Why", SubmittedAt = c.StartDate.ToDateTime(new TimeOnly(10, 0)),
            };
            db.Submissions.Add(s);
            db.SaveChanges();
            if (rating is { } r)
            {
                db.Evaluations.Add(new Evaluation { SubmissionId = s.Id, EvaluatedBy = staff.Id, Rating = r, PositivePoints = "Nice", EvaluatedAt = DateTime.Now.AddDays(-1) });
                db.SaveChanges();
            }
            entries[key] = s;
        }

        var notifications = new Mock<INotificationService>();
        var api = new AwardingApiController(db,
            new AwardIssuedService(db, NullLogger<AwardIssuedService>.Instance, notifications.Object),
            new CompetitionResultService(db, NullLogger<CompetitionResultService>.Instance, notifications.Object))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimsPrincipalExtensions.ProfileIdClaim, staff.Id.ToString()), new Claim(ClaimTypes.NameIdentifier, "1")], "test")),
                },
            },
        };
        return new Ctx(api, db, c, first, mention, entries);
    }

    private static T Value<T>(ActionResult<T> result) => result.Value ?? throw new InvalidOperationException("no value");

    private static AwardingDetailDto Detail(Ctx c) => Value(c.Api.Detail(c.Competition.Id).Result);

    private static async Task<AwardingDetailDto> Issue(Ctx c, CompetitionAward award, string entry)
        => Value(await c.Api.Issue(c.Competition.Id, new IssueAwardRequest
        { CompetitionAwardId = award.Id, SubmissionId = c.Entries[entry].Id, Remarks = "Great" }));

    [Fact]
    public async Task Detail_json_uses_the_keys_of_the_app()
    {
        var c = Seed();
        var detail = await Issue(c, c.First, "best1");
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(detail, Json));
        var root = doc.RootElement;
        foreach (var key in new[] { "id", "title", "status", "submissionCount", "evaluatedCount", "pendingEvaluations", "emptySlots",
            "canAward", "canPublish", "publishBlockReason", "isPublished", "publishedAt", "adjustedAt", "awards" })
        {
            Assert.True(root.TryGetProperty(key, out _), key);
        }
        Assert.Equal("CLOSED", root.GetProperty("status").GetString());
        var slot = root.GetProperty("awards")[0];
        Assert.Equal(1, slot.GetProperty("activeCount").GetInt32());
        Assert.Equal(0, slot.GetProperty("remaining").GetInt32());
        var issued = slot.GetProperty("issued")[0];
        Assert.Equal("Active", issued.GetProperty("status").GetString());
        Assert.Equal("BEST", issued.GetProperty("rating").GetString());
        Assert.Equal(Today.ToString("yyyy-MM-dd"), issued.GetProperty("issuedDate").GetString());
        Assert.False(issued.GetProperty("isDelivered").GetBoolean());
        Assert.True(issued.TryGetProperty("rowVersion", out var rv) && rv.GetString()!.Length > 0);
        Assert.Equal(250m, issued.GetProperty("prizeValue").GetDecimal());
    }

    [Fact]
    public async Task Competitions_lists_closed_and_graded_with_slot_counts()
    {
        var c = Seed();
        await Issue(c, c.Mention, "best1");
        var list = Value(await c.Api.Competitions());
        var row = Assert.Single(list);
        Assert.Equal(3, row.TotalSlots);
        Assert.Equal(1, row.IssuedSlots);
        Assert.Equal(3, row.SubmissionCount);
        Assert.Equal(3, row.EvaluatedCount);
        Assert.Null(row.AdjustedAt);
    }

    [Fact]
    public async Task Candidates_are_best_entries_without_an_award()
    {
        var c = Seed();
        await Issue(c, c.First, "best1");
        var list = Value(await c.Api.Candidates(c.Competition.Id));
        var one = Assert.Single(list);
        Assert.Equal(c.Entries["best2"].Id, one.SubmissionId);
        Assert.Equal(Rating.BEST, one.Rating);
    }

    [Fact]
    public async Task Issue_rejects_a_non_best_entry_and_a_full_award()
    {
        var c = Seed();
        var ex = await Assert.ThrowsAsync<BusinessException>(() => Issue(c, c.First, "good"));
        Assert.Equal(AwardIssuedService.NotBestMessage, ex.Message);
        await Issue(c, c.First, "best1");
        var full = await Assert.ThrowsAsync<BusinessException>(() => Issue(c, c.First, "best2"));
        Assert.Equal(AwardIssuedService.NoSlotsMessage, full.Message);
    }

    [Fact]
    public async Task Issue_after_publishing_is_allowed_without_a_reason()
    {
        var c = Seed(graded: true);
        var detail = await Issue(c, c.First, "best1");
        Assert.True(detail.IsPublished);
        Assert.Single(detail.Awards[0].Issued);
    }

    [Fact]
    public async Task Update_changes_the_award_and_stale_row_version_is_409()
    {
        var c = Seed();
        var detail = await Issue(c, c.First, "best1");
        var issued = detail.Awards[0].Issued[0];

        var stale = await c.Api.Update(issued.Id, new UpdateAwardRequest
        { CompetitionAwardId = c.Mention.Id, SubmissionId = c.Entries["best1"].Id, RowVersion = "1" });
        Assert.Equal(409, Assert.IsType<ObjectResult>(stale.Result).StatusCode);

        var ok = Value(await c.Api.Update(issued.Id, new UpdateAwardRequest
        { CompetitionAwardId = c.Mention.Id, SubmissionId = c.Entries["best2"].Id, Remarks = "Moved", RowVersion = issued.RowVersion }));
        Assert.Empty(ok.Awards.Single(a => a.Id == c.First.Id).Issued);
        Assert.Equal("Moved", ok.Awards.Single(a => a.Id == c.Mention.Id).Issued[0].Remarks);
    }

    [Fact]
    public async Task Unknown_award_is_404()
    {
        var c = Seed();
        await Assert.ThrowsAsync<NotFoundException>(() => c.Api.Delete(999, null));
        await Assert.ThrowsAsync<NotFoundException>(() => c.Api.Detail(999));
    }

    [Fact]
    public async Task Delete_before_publishing_removes_the_award()
    {
        var c = Seed();
        var issued = (await Issue(c, c.First, "best1")).Awards[0].Issued[0];
        var result = Value(await c.Api.Delete(issued.Id, issued.RowVersion));
        Assert.Empty(result.Awards.SelectMany(a => a.Issued));
        Assert.Equal(0, await c.Db.AwardsIssued.CountAsync());
    }

    [Fact]
    public async Task Delete_is_refused_after_publishing_but_revoke_needs_a_reason_and_works()
    {
        var c = Seed(graded: true);
        var issued = (await Issue(c, c.First, "best1")).Awards[0].Issued[0];
        await Assert.ThrowsAsync<BusinessException>(() => c.Api.Delete(issued.Id, issued.RowVersion));
        await Assert.ThrowsAsync<BusinessException>(() => c.Api.Revoke(issued.Id, new RevokeRequest { Reason = " ", RowVersion = issued.RowVersion }));

        var stale = await c.Api.Revoke(issued.Id, new RevokeRequest { Reason = "Wrong", RowVersion = "1" });
        Assert.Equal(409, Assert.IsType<ObjectResult>(stale.Result).StatusCode);

        var result = Value(await c.Api.Revoke(issued.Id, new RevokeRequest { Reason = "Wrong entry", RowVersion = issued.RowVersion }));
        Assert.Empty(result.Awards.SelectMany(a => a.Issued));
    }

    [Fact]
    public void Checklist_blocks_publishing_while_entries_are_not_evaluated()
    {
        var c = Seed(withUnevaluated: true);
        var d = Detail(c);
        Assert.Equal(1, d.PendingEvaluations);
        Assert.False(d.CanPublish);
        Assert.Equal(CompetitionResultService.NotGradedMessage(1), d.PublishBlockReason);
    }

    [Fact]
    public async Task Publish_blocked_when_not_all_graded()
    {
        var c = Seed(withUnevaluated: true);
        var ex = await Assert.ThrowsAsync<BusinessException>(() => c.Api.Publish(c.Competition.Id, new PublishRequest { ConfirmEmptySlots = true }));
        Assert.Equal(CompetitionResultService.NotGradedMessage(1), ex.Message);
    }

    [Fact]
    public async Task Publish_needs_confirmation_for_empty_slots_then_grades_the_competition()
    {
        var c = Seed();
        Assert.Equal(3, Detail(c).EmptySlots);
        Assert.True(Detail(c).CanPublish);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => c.Api.Publish(c.Competition.Id, new PublishRequest { ConfirmEmptySlots = false }));
        Assert.Contains("3 award slot", ex.Message);
        Assert.Equal(CompetitionStatus.UPCOMING, (await c.Db.Competitions.FindAsync(c.Competition.Id))!.Status);

        var published = Value(await c.Api.Publish(c.Competition.Id, new PublishRequest { ConfirmEmptySlots = true }));
        Assert.True(published.IsPublished);
        Assert.Equal(CompetitionStatus.GRADED, published.Status);
        Assert.NotNull(published.PublishedAt);
        Assert.False(published.CanPublish);
        Assert.True(published.CanAward);
    }

    [Fact]
    public async Task Publish_after_issuing_awards_reduces_empty_slots()
    {
        var c = Seed();
        await Issue(c, c.First, "best1");
        var d = await Issue(c, c.Mention, "best2");
        Assert.Equal(1, d.EmptySlots);
        var published = Value(await c.Api.Publish(c.Competition.Id, new PublishRequest { ConfirmEmptySlots = true }));
        Assert.True(published.IsPublished);
    }

    [Fact]
    public void Delivery_and_adjust_are_not_supported_409()
    {
        var c = Seed();
        Assert.Equal(409, Assert.IsType<ObjectResult>(c.Api.SetDelivery(1)).StatusCode);
        Assert.Equal(409, Assert.IsType<ObjectResult>(c.Api.AdjustEvaluation(1)).StatusCode);
    }
}
