using InstituteOfFineArts.Web.Data;
using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Helpers;
using InstituteOfFineArts.Web.Infrastructure.Api;
using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.Services.Implementations;
using Microsoft.EntityFrameworkCore;

namespace InstituteOfFineArts.Web.Controllers.Api.Dto;

/// <summary>Row of the "Awarding" tab (mobile AwardingCompetition).</summary>
public sealed record AwardingCompetitionDto(
    long Id, string Title, DateOnly EndDate, CompetitionStatus Status, int SubmissionCount, int EvaluatedCount,
    int TotalSlots, int IssuedSlots, DateTime? AdjustedAt);

/// <summary>One issued award (mobile IssuedAward). The system deletes revoked awards, so Status is always "Active".</summary>
public sealed record IssuedAwardDto(
    long Id, long SubmissionId, string SubmissionTitle, string StudentName, string StudentCode, Rating? Rating,
    DateOnly IssuedDate, string? Remarks, decimal PrizeValue, string Status, string? RevokedReason,
    bool IsDelivered, DateTime? DeliveredAt, string RowVersion);

/// <summary>Defined award with its slots (mobile AwardSlot).</summary>
public sealed record AwardSlotDto(
    long Id, string Name, int Rank, decimal PrizeValue, int Quantity, string? Description, int ActiveCount,
    int Remaining, List<IssuedAwardDto> Issued);

/// <summary>Awarding page of one competition with the publish checklist (mobile AwardingDetail).</summary>
public sealed record AwardingDetailDto(
    long Id, string Title, CompetitionStatus Status, int SubmissionCount, int EvaluatedCount, int PendingEvaluations,
    int EmptySlots, bool CanAward, bool CanPublish, string? PublishBlockReason, bool IsPublished, DateTime? PublishedAt,
    DateTime? AdjustedAt, List<AwardSlotDto> Awards,
    // Not supported by this system (no column / rule): the app hides "mark as delivered" and "adjust evaluation"
    bool CanMarkDelivered = false, bool CanAdjustEvaluation = false);

/// <summary>Entry that may receive an award (mobile AwardCandidate).</summary>
public sealed record AwardCandidateDto(long SubmissionId, string Title, string StudentName, string StudentCode, Rating Rating);

/// <summary>Body of POST competitions/{id}/awards.</summary>
public sealed class IssueAwardRequest
{
    public long? CompetitionAwardId { get; set; }
    public long? SubmissionId { get; set; }
    public string? Remarks { get; set; }

    /// <summary>Sent by the app after publishing; this system needs no reason to issue an award, so it is ignored.</summary>
    public string? Reason { get; set; }
}

/// <summary>Body of PUT awards/{id}.</summary>
public sealed class UpdateAwardRequest
{
    public long? CompetitionAwardId { get; set; }
    public long? SubmissionId { get; set; }
    public string? Remarks { get; set; }
    public string? RowVersion { get; set; }
}

/// <summary>Body of POST competitions/{id}/publish.</summary>
public sealed class PublishRequest
{
    public bool ConfirmEmptySlots { get; set; }
}

/// <summary>Body of POST awards/{id}/revoke.</summary>
public sealed class RevokeRequest
{
    public string? Reason { get; set; }
    public string? RowVersion { get; set; }
}

/// <summary>Read side of the awarding API (AsNoTracking queries mapped to the DTOs above).</summary>
public static class AwardingQueries
{
    /// <summary>Competitions of the Awarding tab: CLOSED or GRADED, newest deadline first.</summary>
    public static async Task<List<AwardingCompetitionDto>> CompetitionsAsync(AppDbContext db)
    {
        var today = DateTimeHelper.Today;
        var rows = await db.Competitions.AsNoTracking()
            .Where(c => c.Status == CompetitionStatus.GRADED || c.EndDate < today)
            .OrderByDescending(c => c.EndDate).ThenByDescending(c => c.Id)
            .Select(c => new
            {
                c.Id, c.Title, c.EndDate, c.Status,
                SubmissionCount = c.Submissions.Count,
                EvaluatedCount = c.Submissions.Count(s => s.Evaluation != null),
                TotalSlots = c.Awards.Sum(a => (int?)a.Quantity) ?? 0,
                IssuedSlots = c.Awards.Sum(a => (int?)a.IssuedAwards.Count) ?? 0,
            })
            .ToListAsync();
        return rows.Select(r => new AwardingCompetitionDto(
            r.Id, r.Title, r.EndDate, r.Status == CompetitionStatus.GRADED ? CompetitionStatus.GRADED : CompetitionStatus.CLOSED,
            r.SubmissionCount, r.EvaluatedCount, r.TotalSlots, r.IssuedSlots, null)).ToList();
    }

    /// <summary>Awarding page of a competition; BusinessException when it is not CLOSED / GRADED yet.</summary>
    public static async Task<AwardingDetailDto> DetailAsync(AppDbContext db, long competitionId)
    {
        var competition = await db.Competitions.AsNoTracking().FirstOrDefaultAsync(c => c.Id == competitionId)
            ?? throw new NotFoundException($"Competition {competitionId} was not found.");
        var status = CompetitionStatusResolver.Resolve(competition);
        if (status is not (CompetitionStatus.CLOSED or CompetitionStatus.GRADED))
        {
            throw new BusinessException(AwardIssuedService.NotClosedMessage);
        }

        var submissionCount = await db.Submissions.CountAsync(s => s.CompetitionId == competitionId);
        var evaluatedCount = await db.Submissions.CountAsync(s => s.CompetitionId == competitionId && s.Evaluation != null);
        var pending = submissionCount - evaluatedCount;

        var awards = await db.CompetitionAwards.AsNoTracking()
            .Where(a => a.CompetitionId == competitionId)
            .Include(a => a.IssuedAwards).ThenInclude(i => i.Submission).ThenInclude(s => s.Student).ThenInclude(st => st.Account)
            .Include(a => a.IssuedAwards).ThenInclude(i => i.Submission).ThenInclude(s => s.Evaluation)
            .OrderBy(a => a.Rank).ThenBy(a => a.AwardName).ThenBy(a => a.Id)
            .ToListAsync();

        var slots = awards.Select(a =>
        {
            var issued = a.IssuedAwards.OrderBy(i => i.IssuedDate).ThenBy(i => i.Id).Select(i => new IssuedAwardDto(
                i.Id, i.SubmissionId, i.Submission.Title, i.Submission.Student.Account.FullName, i.Submission.Student.StudentCode,
                i.Submission.Evaluation?.Rating, i.IssuedDate, i.Remarks, a.PrizeValue, "Active", null, false, null,
                RowVersion.Of(i.CreatedAt, i.UpdatedAt))).ToList();
            return new AwardSlotDto(a.Id, a.AwardName, a.Rank, a.PrizeValue, a.Quantity, a.PrizeDescription,
                issued.Count, Math.Max(0, a.Quantity - issued.Count), issued);
        }).ToList();

        var isPublished = status == CompetitionStatus.GRADED;
        string? blockReason = null;
        if (!isPublished && pending > 0)
        {
            blockReason = CompetitionResultService.NotGradedMessage(pending);
        }
        return new AwardingDetailDto(
            competition.Id, competition.Title, status, submissionCount, evaluatedCount, pending,
            slots.Sum(s => s.Remaining), true, !isPublished && pending == 0, blockReason, isPublished,
            isPublished ? competition.PublishedAt : null, null, slots);
    }
}
