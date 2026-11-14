using InstituteOfFineArts.Web.Data;
using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Helpers;
using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.ViewModels.Public;
using Microsoft.EntityFrameworkCore;

namespace InstituteOfFineArts.Web.Controllers.Api.Dto;

// JSON shapes read by the mobile app (mobile/lib/features/competitions + submissions). Property names become camelCase.

/// <summary>CompetitionSummary of the app. Topic does not exist in this system, so it is always null.</summary>
public sealed record CompetitionSummaryDto(long Id, string Title, string? Topic, DateOnly StartDate, DateOnly EndDate,
    CompetitionStatus Status, int DaysLeft, int DaysUntilStart, int SubmissionCount);

/// <summary>Award definition of a competition.</summary>
public sealed record CompetitionAwardDto(string Name, int Rank, decimal PrizeValue, int Quantity, string? Description);

/// <summary>CompetitionDetail of the app: the summary fields plus description, conditions (= rules), deadline and awards.</summary>
public sealed record CompetitionDetailDto(long Id, string Title, string? Topic, DateOnly StartDate, DateOnly EndDate,
    CompetitionStatus Status, int DaysLeft, int DaysUntilStart, int SubmissionCount,
    string? Description, string? Conditions, DateTime Deadline, IReadOnlyList<CompetitionAwardDto> Awards);

/// <summary>Winner of the home page.</summary>
public sealed record WinnerDto(string StudentName, string AwardName, int Rank, string PaintingTitle);

/// <summary>HomeData of the app.</summary>
public sealed record HomeDto(IReadOnlyList<CompetitionSummaryDto> Upcoming, IReadOnlyList<CompetitionSummaryDto> Ongoing,
    string? LatestGradedCompetition, IReadOnlyList<WinnerDto> LatestWinners);

/// <summary>Issued award of a GRADED competition (AwardResult).</summary>
public sealed record AwardResultDto(string AwardName, int Rank, string StudentName, string PaintingTitle, decimal PrizeValue);

/// <summary>Row of "My submissions".</summary>
public sealed record SubmissionListItemDto(long Id, long CompetitionId, string CompetitionTitle, CompetitionStatus CompetitionStatus,
    string Title, DateTime SubmittedAt, DateTime? UpdatedAt, bool CanEdit, Rating? Rating);

/// <summary>Evaluation shown to the owner once the competition is GRADED (BR-24).</summary>
public sealed record SubmissionEvaluationDto(Rating Rating, string? PositivePoints, string? NegativePoints, string? ImprovementAreas,
    DateTime EvaluatedAt);

/// <summary>Award won by a submission.</summary>
public sealed record SubmissionAwardDto(string AwardName, int Rank);

/// <summary>SubmissionDetail of the app. PublicConsent has no column in this system: always true.</summary>
public sealed record SubmissionDetailDto(long Id, long CompetitionId, string CompetitionTitle, CompetitionStatus CompetitionStatus,
    string Title, string Description, TextType TextType, string TextContent, bool PublicConsent, string OriginalFileName,
    DateTime SubmittedAt, DateTime? UpdatedAt, bool CanEdit, SubmissionEvaluationDto? Evaluation,
    IReadOnlyList<SubmissionAwardDto> Awards);

/// <summary>
/// Multipart body of POST /api/competitions/{id}/submissions and PUT /api/submissions/{id}. Everything is nullable so
/// that a missing field reaches the service, which answers with its own message (e.g. "Please enter a title").
/// </summary>
public sealed class SubmissionApiForm
{
    public string? Title { get; set; }
    public string? Description { get; set; }

    /// <summary>"Poem", "POEM"… (any case).</summary>
    public string? TextType { get; set; }

    public string? TextContent { get; set; }

    /// <summary>Accepted for compatibility; there is no column for it.</summary>
    public string? PublicConsent { get; set; }

    public IFormFile? Image { get; set; }

    /// <summary>Parsed text type, or null when missing/invalid (the service then rejects it).</summary>
    public TextType? ParsedTextType
        => Enum.TryParse<TextType>(TextType?.Trim(), ignoreCase: true, out var t) && Enum.IsDefined(t) ? t : null;
}

/// <summary>Read queries of the public + student-submission endpoints (AsNoTracking, status always resolved for today).</summary>
public static class PublicAndSubmissionQueries
{
    /// <summary>Days left (ongoing only) and days until the start (upcoming only).</summary>
    public static (int DaysLeft, int DaysUntilStart) Days(CompetitionStatus status, DateOnly start, DateOnly end, DateOnly today)
        => (status == CompetitionStatus.ONGOING ? Math.Max(0, end.DayNumber - today.DayNumber) : 0,
            status == CompetitionStatus.UPCOMING ? Math.Max(0, start.DayNumber - today.DayNumber) : 0);

    /// <summary>Summary row from raw values.</summary>
    public static CompetitionSummaryDto Summary(long id, string title, DateOnly start, DateOnly end, CompetitionStatus stored,
        int submissionCount, DateOnly today)
    {
        var status = CompetitionStatusResolver.Resolve(new Competition { Status = stored, StartDate = start, EndDate = end }, today);
        var (left, until) = Days(status, start, end, today);
        return new CompetitionSummaryDto(id, title, null, start, end, status, left, until, submissionCount);
    }

    /// <summary>Home page blocks from the existing public service result.</summary>
    public static HomeDto Home(HomeVm vm, DateOnly today)
    {
        CompetitionSummaryDto Map(HomeCompetitionVm c) => Summary(c.Id, c.Title, c.StartDate, c.EndDate, c.Status, c.SubmissionCount, today);
        return new HomeDto(
            vm.Upcoming.Select(Map).ToList(),
            vm.Ongoing.Select(Map).ToList(),
            vm.LatestResults?.CompetitionTitle,
            vm.LatestResults?.Winners.Select(w => new WinnerDto(w.StudentName, w.AwardName, w.Rank, w.ArtworkTitle)).ToList()
                ?? new List<WinnerDto>());
    }

    /// <summary>All competitions, newest first.</summary>
    public static async Task<List<CompetitionSummaryDto>> CompetitionsAsync(AppDbContext db)
    {
        var today = DateTimeHelper.Today;
        var rows = await db.Competitions.AsNoTracking()
            .OrderByDescending(c => c.StartDate).ThenByDescending(c => c.Id)
            .Select(c => new { c.Id, c.Title, c.StartDate, c.EndDate, c.Status, Count = c.Submissions.Count })
            .ToListAsync();
        return rows.Select(c => Summary(c.Id, c.Title, c.StartDate, c.EndDate, c.Status, c.Count, today)).ToList();
    }

    /// <summary>One competition with its award definitions.</summary>
    public static async Task<CompetitionDetailDto> CompetitionAsync(AppDbContext db, long id)
    {
        var today = DateTimeHelper.Today;
        var c = await db.Competitions.AsNoTracking().Include(x => x.Awards).FirstOrDefaultAsync(x => x.Id == id)
                ?? throw new NotFoundException($"Competition {id} was not found.");
        var count = await db.Submissions.CountAsync(s => s.CompetitionId == id);
        var s = Summary(c.Id, c.Title, c.StartDate, c.EndDate, c.Status, count, today);
        return new CompetitionDetailDto(s.Id, s.Title, null, s.StartDate, s.EndDate, s.Status, s.DaysLeft, s.DaysUntilStart,
            s.SubmissionCount, c.Description, c.Rules, CompetitionStatusResolver.SubmissionDeadline(c.EndDate),
            c.Awards.OrderBy(a => a.Rank).ThenBy(a => a.AwardName)
                .Select(a => new CompetitionAwardDto(a.AwardName, a.Rank, a.PrizeValue, a.Quantity, a.PrizeDescription)).ToList());
    }

    /// <summary>Issued awards of a competition; empty until its results are published (GRADED).</summary>
    public static async Task<List<AwardResultDto>> AwardsAsync(AppDbContext db, long competitionId)
    {
        var competition = await db.Competitions.AsNoTracking().Where(c => c.Id == competitionId)
                              .Select(c => new { c.Status }).FirstOrDefaultAsync()
                          ?? throw new NotFoundException($"Competition {competitionId} was not found.");
        if (competition.Status != CompetitionStatus.GRADED)
        {
            return new List<AwardResultDto>();
        }
        return await db.AwardsIssued.AsNoTracking()
            .Where(a => a.Submission.CompetitionId == competitionId)
            .OrderBy(a => a.CompetitionAward.Rank).ThenBy(a => a.Submission.Student.Account.FullName).ThenBy(a => a.Id)
            .Select(a => new AwardResultDto(a.CompetitionAward.AwardName, a.CompetitionAward.Rank,
                a.Submission.Student.Account.FullName, a.Submission.Title, a.CompetitionAward.PrizeValue))
            .ToListAsync();
    }

    /// <summary>The student's own entries, newest first; rating only when GRADED (BR-24).</summary>
    public static async Task<List<SubmissionListItemDto>> MineAsync(AppDbContext db, long studentProfileId, long? competitionId,
        string? q, DateOnly? from, DateOnly? to, Rating? rating)
    {
        DateTimeHelper.EnsureValidRange(from, to);
        var today = DateTimeHelper.Today;
        var query = db.Submissions.AsNoTracking().Where(s => s.StudentId == studentProfileId);
        if (competitionId is { } cid) query = query.Where(s => s.CompetitionId == cid);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var title = q.Trim();
            query = query.Where(s => s.Title.Contains(title));
        }
        if (from is { } f) { var start = DateTimeHelper.StartOfDay(f); query = query.Where(s => s.SubmittedAt >= start); }
        if (to is { } t) { var end = DateTimeHelper.StartOfNextDay(t); query = query.Where(s => s.SubmittedAt < end); }
        if (rating is { } r)
        {
            query = query.Where(s => s.Competition.Status == CompetitionStatus.GRADED && s.Evaluation != null && s.Evaluation.Rating == r);
        }

        var rows = await query.OrderByDescending(s => s.SubmittedAt).ThenByDescending(s => s.Id)
            .Select(s => new
            {
                s.Id, s.CompetitionId, CompetitionTitle = s.Competition.Title, s.Competition.StartDate, s.Competition.EndDate,
                Stored = s.Competition.Status, s.Title, s.SubmittedAt, s.UpdatedAt,
                Rating = s.Evaluation != null ? (Rating?)s.Evaluation.Rating : null,
            }).ToListAsync();

        return rows.Select(s =>
        {
            var status = CompetitionStatusResolver.Resolve(new Competition { Status = s.Stored, StartDate = s.StartDate, EndDate = s.EndDate }, today);
            return new SubmissionListItemDto(s.Id, s.CompetitionId, s.CompetitionTitle, status, s.Title, s.SubmittedAt, s.UpdatedAt,
                status == CompetitionStatus.ONGOING, status == CompetitionStatus.GRADED ? s.Rating : null);
        }).ToList();
    }

    /// <summary>Own entry in full; another student's id looks like an unknown id (404).</summary>
    public static async Task<SubmissionDetailDto> DetailAsync(AppDbContext db, long id, long studentProfileId)
    {
        var s = await db.Submissions.AsNoTracking()
                    .Include(x => x.Competition).Include(x => x.Evaluation)
                    .Include(x => x.AwardIssued!).ThenInclude(a => a.CompetitionAward)
                    .FirstOrDefaultAsync(x => x.Id == id && x.StudentId == studentProfileId)
                ?? throw new NotFoundException($"Submission {id} was not found.");
        var status = CompetitionStatusResolver.Resolve(s.Competition);
        var graded = status == CompetitionStatus.GRADED;
        return new SubmissionDetailDto(s.Id, s.CompetitionId, s.Competition.Title, status, s.Title, s.Description, s.TextType,
            s.TextContent, true, s.OriginalFileName, s.SubmittedAt, s.UpdatedAt, status == CompetitionStatus.ONGOING,
            graded && s.Evaluation is { } e
                ? new SubmissionEvaluationDto(e.Rating, e.PositivePoints, e.NegativePoints, e.ImprovementAreas, e.EvaluatedAt)
                : null,
            graded && s.AwardIssued is { } a
                ? new[] { new SubmissionAwardDto(a.CompetitionAward.AwardName, a.CompetitionAward.Rank) }
                : Array.Empty<SubmissionAwardDto>());
    }
}
