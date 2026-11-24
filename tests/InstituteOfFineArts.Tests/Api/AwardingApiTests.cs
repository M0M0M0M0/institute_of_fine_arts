using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using InstituteOfFineArts.Web.Controllers.Api;
using InstituteOfFineArts.Web.Controllers.Api.Dto;
using InstituteOfFineArts.Web.Data;
using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Helpers;
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

/// <summary>
/// Mobile awarding API (AwardingApiController, 11/2026): JSON shape, the jury ranking, publishing gives the awards from
/// the ranking (BR-30, BR-31), hand issuing refused, remarks edit / revoke after publishing, publish checklist.
/// </summary>
public class AwardingApiTests
{
    private static readonly DateOnly Today = DateTimeHelper.Today;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private sealed record Ctx(AwardingApiController Api, AppDbContext Db, Competition Competition,
        CompetitionAward First, CompetitionAward Mention, Dictionary<string, Submission> Entries);

    /// <summary>
    /// CLOSED (or GRADED) competition with First Prize x1 and Honorable Mention x2, a jury of one teacher and one criterion;
    /// entries graded best1 = 95, best2 = 91, good = 70 (and "none" not graded when asked).
    /// </summary>
    private static async Task<Ctx> SeedAsync(bool graded = false, bool withUnevaluated = false)
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var staff = new StaffProfile
        {
            StaffCode = "NV0003", JoinedDate = new DateOnly(2024, 1, 1),
            Account = new Account { Username = "staff01", FullName = "Le Minh Hoang", Email = "s1@ifa.local", Role = Role.STAFF, PasswordHash = "x" },
        };
        var judge = new StaffProfile
        {
            StaffCode = "NV0005", JoinedDate = new DateOnly(2024, 1, 1),
            Account = new Account { Username = "teacher01", FullName = "Pham Thu Ha", Email = "t1@ifa.local", Role = Role.TEACHER, PasswordHash = "x" },
        };
        db.StaffProfiles.AddRange(staff, judge);
        db.SaveChanges();
        var c = new Competition
        {
            Code = "X1", Title = "City at Night", StartDate = Today.AddDays(-30), EndDate = Today.AddDays(-5), CreatedBy = staff.Id,
            Status = CompetitionStatus.UPCOMING,
        };
        db.Competitions.Add(c);
        var criterion = new GradingCriterion { Name = "Overall" };
        db.GradingCriteria.Add(criterion);
        db.SaveChanges();
        var first = new CompetitionAward { CompetitionId = c.Id, AwardName = "First Prize", Rank = 1, PrizeValue = 250m, Quantity = 1, PrizeDescription = "Trophy" };
        var mention = new CompetitionAward { CompetitionId = c.Id, AwardName = "Honorable Mention", Rank = 4, PrizeValue = 50m, Quantity = 2 };
        db.CompetitionAwards.AddRange(first, mention);
        var cc = new CompetitionCriterion { CompetitionId = c.Id, CriterionId = criterion.Id, WeightPercent = 100m };
        db.CompetitionCriteria.Add(cc);
        db.CompetitionJudges.Add(new CompetitionJudge { CompetitionId = c.Id, JudgeId = judge.Id, IsChair = true });
        db.SaveChanges();

        var entries = new Dictionary<string, Submission>();
        var specs = new List<(string Key, decimal? Score)> { ("best1", 95m), ("best2", 91m), ("good", 70m) };
        if (withUnevaluated) specs.Add(("none", null));
        var n = 0;
        foreach (var (key, score) in specs)
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
            if (score is { } value)
            {
                var grade = new JudgeEvaluation { SubmissionId = s.Id, JudgeId = judge.Id, PositivePoints = "Nice", TotalScore = value, EvaluatedAt = DateTime.Now };
                grade.Scores.Add(new JudgeScore { CompetitionCriterionId = cc.Id, Score = value });
                db.JudgeEvaluations.Add(grade);
                db.SaveChanges();
            }
            entries[key] = s;
        }
        await JuryResults.RecomputeAsync(db, c.Id);

        var notifications = new Mock<INotificationService>();
        var results = new CompetitionResultService(db, NullLogger<CompetitionResultService>.Instance, notifications.Object);
        var api = new AwardingApiController(db,
            new AwardIssuedService(db, NullLogger<AwardIssuedService>.Instance, notifications.Object), results)
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
        if (graded)
        {
            await results.PublishAsync(c.Id, null, staff.Id);
        }
        return new Ctx(api, db, c, first, mention, entries);
    }

    private static T Value<T>(ActionResult<T> result) => result.Value ?? throw new InvalidOperationException("no value");

    private static async Task<AwardingDetailDto> DetailAsync(Ctx c) => Value(await c.Api.Detail(c.Competition.Id));

    [Fact]
    public async Task Detail_json_uses_the_keys_of_the_app()
    {
        var c = await SeedAsync(graded: true);
        var detail = await DetailAsync(c);
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(detail, Json));
        var root = doc.RootElement;
        foreach (var key in new[] { "id", "title", "status", "submissionCount", "evaluatedCount", "pendingEvaluations", "emptySlots",
            "canAward", "canPublish", "publishBlockReason", "isPublished", "publishedAt", "adjustedAt", "awards", "ranking" })
        {
            Assert.True(root.TryGetProperty(key, out _), key);
        }
        Assert.Equal("GRADED", root.GetProperty("status").GetString());
        Assert.False(root.GetProperty("canAward").GetBoolean());
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
        var top = root.GetProperty("ranking")[0];
        Assert.Equal(1, top.GetProperty("place").GetInt32());
        Assert.Equal("First Prize", top.GetProperty("awardName").GetString());
    }

    [Fact]
    public async Task Competitions_lists_closed_and_graded_with_slot_counts()
    {
        var c = await SeedAsync(graded: true);
        var row = Assert.Single(Value(await c.Api.Competitions()));
        Assert.Equal(3, row.TotalSlots);
        Assert.Equal(3, row.IssuedSlots);
        Assert.Equal(3, row.SubmissionCount);
        Assert.Equal(3, row.EvaluatedCount);
        Assert.Null(row.AdjustedAt);
    }

    /// <summary>Before publishing the ranking previews the awards; nothing is given yet.</summary>
    [Fact]
    public async Task Ranking_and_candidates_preview_the_awards()
    {
        var c = await SeedAsync();
        var d = await DetailAsync(c);
        Assert.Equal(["First Prize", "Honorable Mention", "Honorable Mention"], d.Ranking.Select(r => r.AwardName));
        Assert.Equal([1, 2, 3], d.Ranking.Select(r => r.Place!.Value));
        Assert.Empty(d.Awards.SelectMany(a => a.Issued));

        var candidates = Value(await c.Api.Candidates(c.Competition.Id));
        Assert.Equal(3, candidates.Count);
        Assert.Equal(c.Entries["best1"].Id, candidates[0].SubmissionId);
        Assert.Equal(Rating.BEST, candidates[0].Rating);
    }

    /// <summary>BR-30: awards are not issued by hand.</summary>
    [Fact]
    public async Task Issue_by_hand_is_refused()
    {
        var c = await SeedAsync();
        var ex = await Assert.ThrowsAsync<BusinessException>(() => c.Api.Issue(c.Competition.Id,
            new IssueAwardRequest { CompetitionAwardId = c.First.Id, SubmissionId = c.Entries["best1"].Id }));
        Assert.Equal(AwardIssuedService.AutomaticAwardsMessage, ex.Message);
        Assert.Empty(c.Db.AwardsIssued);
    }

    /// <summary>Only the remarks change; another winner is refused; a stale row version is 409.</summary>
    [Fact]
    public async Task Update_changes_the_remarks_only_and_stale_row_version_is_409()
    {
        var c = await SeedAsync(graded: true);
        var issued = (await DetailAsync(c)).Awards.Single(a => a.Id == c.First.Id).Issued[0];

        var stale = await c.Api.Update(issued.Id, new UpdateAwardRequest
        { CompetitionAwardId = c.First.Id, SubmissionId = issued.SubmissionId, RowVersion = "1" });
        Assert.Equal(409, Assert.IsType<ObjectResult>(stale.Result).StatusCode);

        var moved = await Assert.ThrowsAsync<BusinessException>(() => c.Api.Update(issued.Id, new UpdateAwardRequest
        { CompetitionAwardId = c.First.Id, SubmissionId = c.Entries["good"].Id, RowVersion = issued.RowVersion }));
        Assert.Equal(AwardIssuedService.AutomaticAwardsMessage, moved.Message);

        var ok = Value(await c.Api.Update(issued.Id, new UpdateAwardRequest
        { CompetitionAwardId = c.First.Id, SubmissionId = issued.SubmissionId, Remarks = "Outstanding", RowVersion = issued.RowVersion }));
        Assert.Equal("Outstanding", ok.Awards.Single(a => a.Id == c.First.Id).Issued[0].Remarks);
    }

    [Fact]
    public async Task Unknown_award_is_404()
    {
        var c = await SeedAsync();
        await Assert.ThrowsAsync<NotFoundException>(() => c.Api.Delete(999, null));
        await Assert.ThrowsAsync<NotFoundException>(() => c.Api.Detail(999));
    }

    /// <summary>BR-33: after publishing an award is revoked with a reason (deleted, slot left empty), not deleted.</summary>
    [Fact]
    public async Task Delete_is_refused_after_publishing_but_revoke_needs_a_reason_and_works()
    {
        var c = await SeedAsync(graded: true);
        var issued = (await DetailAsync(c)).Awards.Single(a => a.Id == c.First.Id).Issued[0];
        await Assert.ThrowsAsync<BusinessException>(() => c.Api.Delete(issued.Id, issued.RowVersion));
        await Assert.ThrowsAsync<BusinessException>(() => c.Api.Revoke(issued.Id, new RevokeRequest { Reason = " ", RowVersion = issued.RowVersion }));

        var stale = await c.Api.Revoke(issued.Id, new RevokeRequest { Reason = "Wrong", RowVersion = "1" });
        Assert.Equal(409, Assert.IsType<ObjectResult>(stale.Result).StatusCode);

        var result = Value(await c.Api.Revoke(issued.Id, new RevokeRequest { Reason = "Copied work", RowVersion = issued.RowVersion }));
        Assert.Empty(result.Awards.Single(a => a.Id == c.First.Id).Issued);
        Assert.Equal(1, result.EmptySlots);
    }

    [Fact]
    public async Task Checklist_blocks_publishing_while_entries_are_not_evaluated()
    {
        var c = await SeedAsync(withUnevaluated: true);
        var d = await DetailAsync(c);
        Assert.Equal(1, d.PendingEvaluations);
        Assert.False(d.CanPublish);
        Assert.Equal(CompetitionResultService.NotGradedMessage(1), d.PublishBlockReason);
    }

    [Fact]
    public async Task Publish_blocked_when_not_all_graded()
    {
        var c = await SeedAsync(withUnevaluated: true);
        var ex = await Assert.ThrowsAsync<BusinessException>(() => c.Api.Publish(c.Competition.Id, new PublishRequest()));
        Assert.Equal(CompetitionResultService.NotGradedMessage(1), ex.Message);
    }

    /// <summary>Publishing grades the competition and gives the awards from the ranking, no confirmation needed.</summary>
    [Fact]
    public async Task Publish_grades_the_competition_and_gives_the_awards()
    {
        var c = await SeedAsync();
        Assert.True((await DetailAsync(c)).CanPublish);

        var published = Value(await c.Api.Publish(c.Competition.Id, new PublishRequest()));

        Assert.True(published.IsPublished);
        Assert.Equal(CompetitionStatus.GRADED, published.Status);
        Assert.NotNull(published.PublishedAt);
        Assert.False(published.CanPublish);
        Assert.Equal(0, published.EmptySlots);
        Assert.Equal(c.Entries["best1"].Id, published.Awards.Single(a => a.Id == c.First.Id).Issued.Single().SubmissionId);
        Assert.Equal(2, published.Awards.Single(a => a.Id == c.Mention.Id).Issued.Count);
    }

    [Fact]
    public async Task Delivery_and_adjust_are_not_supported_409()
    {
        var c = await SeedAsync();
        Assert.Equal(409, Assert.IsType<ObjectResult>(c.Api.SetDelivery(1)).StatusCode);
        Assert.Equal(409, Assert.IsType<ObjectResult>(c.Api.AdjustEvaluation(1)).StatusCode);
    }
}
