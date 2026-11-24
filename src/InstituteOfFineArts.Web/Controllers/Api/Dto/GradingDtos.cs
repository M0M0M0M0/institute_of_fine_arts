using InstituteOfFineArts.Web.Data;
using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Helpers;
using InstituteOfFineArts.Web.Infrastructure.Api;
using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.Services.Interfaces;
using InstituteOfFineArts.Web.ViewModels.Staff.Evaluations;
using Microsoft.EntityFrameworkCore;

namespace InstituteOfFineArts.Web.Controllers.Api.Dto;

/// <summary>One competition in the grading screen of the mobile app (mobile/lib/features/grading/grading.dart).</summary>
public class GradingCompetitionDto
{
    public long Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateOnly EndDate { get; set; }
    public CompetitionStatus Status { get; set; }
    public int SubmissionCount { get; set; }

    /// <summary>For a judge: entries they graded; for staff: entries graded by the whole jury.</summary>
    public int EvaluatedCount { get; set; }

    public bool HasCover { get; set; }
}

/// <summary>One row of the entries; Rating = the jury's result (null until every judge graded).</summary>
public class GradingItemDto
{
    public long Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string StudentCode { get; set; } = string.Empty;
    public DateTime SubmittedAt { get; set; }
    public Rating? Rating { get; set; }

    /// <summary>The signed-in judge has graded it (always false for staff).</summary>
    public bool GradedByMe { get; set; }

    public long SubmissionId { get; set; }
    public bool HasThumbnail { get; set; }
}

/// <summary>Entries of one competition; Total / Evaluated = progress of the whole competition (for a judge: their own).</summary>
public class GradingListDto
{
    public long CompetitionId { get; set; }
    public string CompetitionTitle { get; set; } = string.Empty;
    public CompetitionStatus Status { get; set; }
    public int Total { get; set; }
    public int Evaluated { get; set; }
    public List<GradingItemDto> Items { get; set; } = [];
}

/// <summary>A criterion of the competition with its weight.</summary>
public class GradingCriterionDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Weight { get; set; }
}

/// <summary>Score and comment on one criterion.</summary>
public class GradingScoreDto
{
    public long CriterionId { get; set; }
    public decimal? Score { get; set; }
    public string? Comment { get; set; }
}

/// <summary>The signed-in judge's grade; RowVersion is sent back to edit or delete it.</summary>
public class GradingEvaluationDto
{
    public bool Disqualified { get; set; }
    public List<GradingScoreDto> Scores { get; set; } = [];

    /// <summary>Weighted total (2 decimals), null when disqualified.</summary>
    public decimal? TotalScore { get; set; }

    public string? PositivePoints { get; set; }
    public string? NegativePoints { get; set; }
    public string? ImprovementAreas { get; set; }
    public string EvaluatedByName { get; set; } = string.Empty;
    public DateTime EvaluatedAt { get; set; }
    public string RowVersion { get; set; } = string.Empty;
}

/// <summary>A jury member and whether they graded the entry (no scores before publishing).</summary>
public class GradingJudgeDto
{
    public string Name { get; set; } = string.Empty;
    public bool IsChair { get; set; }
    public bool Done { get; set; }
    public bool IsMe { get; set; }
}

/// <summary>One judge's column of the score sheet.</summary>
public class ScoreSheetJudgeDto
{
    public string Name { get; set; } = string.Empty;
    public bool IsChair { get; set; }
    public bool Disqualified { get; set; }
    public decimal? TotalScore { get; set; }
    public List<GradingScoreDto> Scores { get; set; } = [];
    public string? PositivePoints { get; set; }
    public string? NegativePoints { get; set; }
    public string? ImprovementAreas { get; set; }
}

/// <summary>The whole jury's score sheet (after publishing; staff also once the entry is fully graded).</summary>
public class ScoreSheetDto
{
    public List<GradingCriterionDto> Criteria { get; set; } = [];
    public List<ScoreSheetJudgeDto> Judges { get; set; } = [];
    public List<GradingScoreDto> Averages { get; set; } = [];
    public decimal? TotalScore { get; set; }
    public Rating? Rating { get; set; }
    public bool Legacy { get; set; }

    /// <summary>Maps the web view model.</summary>
    public static ScoreSheetDto? From(ScoreSheetVm? vm) => vm == null ? null : new ScoreSheetDto
    {
        Criteria = vm.Criteria.Select(c => new GradingCriterionDto { Id = c.CompetitionCriterionId, Name = c.Name, Description = c.Description, Weight = c.Weight }).ToList(),
        Judges = vm.Judges.Select(j => new ScoreSheetJudgeDto
        {
            Name = j.Name, IsChair = j.IsChair, Disqualified = j.Disqualified, TotalScore = j.Total,
            Scores = j.Scores.Select(s => new GradingScoreDto { CriterionId = s.Key, Score = s.Value.Score, Comment = s.Value.Comment }).ToList(),
            PositivePoints = j.PositivePoints, NegativePoints = j.NegativePoints, ImprovementAreas = j.ImprovementAreas,
        }).ToList(),
        Averages = vm.CriterionAverages.Select(a => new GradingScoreDto { CriterionId = a.Key, Score = a.Value }).ToList(),
        TotalScore = vm.Total,
        Rating = vm.Rating,
        Legacy = vm.Legacy,
    };
}

/// <summary>Entry details for grading.</summary>
public class GradingDetailDto
{
    public long Id { get; set; }
    public long CompetitionId { get; set; }
    public string CompetitionTitle { get; set; } = string.Empty;
    public CompetitionStatus CompetitionStatus { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public TextType TextType { get; set; }
    public string TextContent { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string StudentCode { get; set; } = string.Empty;
    public DateTime SubmittedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    /// <summary>Submitted after the deadline: can only be disqualified (BR-14).</summary>
    public bool IsLate { get; set; }

    public bool CanEvaluate { get; set; }
    public bool CanDeleteEvaluation { get; set; }
    public string? LockReason { get; set; }

    /// <summary>Criteria of the competition with their weights.</summary>
    public List<GradingCriterionDto> Criteria { get; set; } = [];

    /// <summary>The signed-in judge's grade (null when none, and for staff).</summary>
    public GradingEvaluationDto? Evaluation { get; set; }

    /// <summary>The jury's result (null until every judge graded).</summary>
    public Rating? ResultRating { get; set; }

    /// <summary>The jury's average (2 decimals), null until complete or when disqualified.</summary>
    public decimal? ResultScore { get; set; }

    /// <summary>The jury and who has graded.</summary>
    public List<GradingJudgeDto> Jury { get; set; } = [];

    /// <summary>The whole score sheet, when the caller may see it.</summary>
    public ScoreSheetDto? ScoreSheet { get; set; }

    /// <summary>Pictures of the entry (ids for /api/submissions/{id}/images/{imageId}), cover first.</summary>
    public List<long> ImageIds { get; set; } = [];

    /// <summary>Next entry the signed-in judge has not graded (null when none).</summary>
    public long? NextUnevaluatedId { get; set; }

    public int Total { get; set; }
    public int Evaluated { get; set; }
    public long SubmissionId { get; set; }
    public bool HasThumbnail { get; set; }
}

/// <summary>Body of PUT /api/staff/submissions/{id}/evaluation (a judge's grade).</summary>
public class GradingEvaluationRequest
{
    /// <summary>True = disqualify (reason in NegativePoints, no scores).</summary>
    public bool Disqualified { get; set; }

    /// <summary>One score per criterion of the competition.</summary>
    public List<GradingScoreDto> Scores { get; set; } = [];

    public string? PositivePoints { get; set; }
    public string? NegativePoints { get; set; }
    public string? ImprovementAreas { get; set; }

    /// <summary>Required when the judge already graded the entry.</summary>
    public string? RowVersion { get; set; }
}

/// <summary>Read side of the grading API (AsNoTracking queries + the grading page of the web).</summary>
public static class GradingQueries
{
    /// <summary>What a teacher sees: competitions whose submission period is over (they grade the CLOSED ones).</summary>
    public static readonly IReadOnlySet<CompetitionStatus> TeacherStatuses = new HashSet<CompetitionStatus> { CompetitionStatus.CLOSED, CompetitionStatus.GRADED };

    /// <summary>What staff see: published results only, read only (staff do not grade).</summary>
    public static readonly IReadOnlySet<CompetitionStatus> StaffStatuses = new HashSet<CompetitionStatus> { CompetitionStatus.GRADED };

    /// <summary>
    /// Competitions of the given statuses with their grading progress; ready to grade first. For a judge only the
    /// competitions they judge, with the entries they graded themselves.
    /// </summary>
    /// <param name="db">Database</param>
    /// <param name="visible"><see cref="TeacherStatuses"/> or <see cref="StaffStatuses"/></param>
    /// <param name="judgeId">staff_profile.id of the teacher; null for staff</param>
    public static async Task<List<GradingCompetitionDto>> CompetitionsAsync(AppDbContext db, IReadOnlySet<CompetitionStatus> visible, long? judgeId = null)
    {
        var competitions = await db.Competitions.AsNoTracking()
            .Where(c => judgeId == null || c.Judges.Any(j => j.JudgeId == judgeId))
            .ToListAsync();
        var counts = await db.Submissions.AsNoTracking()
            .GroupBy(s => s.CompetitionId)
            .Select(g => new
            {
                Id = g.Key, Total = g.Count(),
                Evaluated = judgeId == null ? g.Count(s => s.Evaluation != null) : g.Count(s => s.JudgeEvaluations.Any(e => e.JudgeId == judgeId)),
            })
            .ToDictionaryAsync(x => x.Id);

        return competitions
            .Select(c => new
            {
                Competition = c,
                Status = CompetitionStatusResolver.Resolve(c),
                Total = counts.TryGetValue(c.Id, out var n) ? n.Total : 0,
                Evaluated = counts.TryGetValue(c.Id, out var m) ? m.Evaluated : 0,
            })
            .Where(x => visible.Contains(x.Status))
            .OrderBy(x => x.Status == CompetitionStatus.CLOSED ? 0 : 1)
            .ThenByDescending(x => x.Competition.EndDate)
            .Select(x => new GradingCompetitionDto
            {
                Id = x.Competition.Id, Title = x.Competition.Title, EndDate = x.Competition.EndDate, Status = x.Status,
                SubmissionCount = x.Total, EvaluatedCount = x.Evaluated, HasCover = x.Competition.CoverImagePath != null,
            })
            .ToList();
    }

    /// <summary>All entries of a competition matching the filters, oldest submission first.</summary>
    /// <param name="visible">Statuses the caller may see; another status is reported as not found (no leak before the deadline)</param>
    /// <param name="judgeId">Teacher: must judge the competition; "graded" = graded by them</param>
    public static async Task<GradingListDto> ListAsync(AppDbContext db, long competitionId, bool? graded, Rating? rating, string? q,
        IReadOnlySet<CompetitionStatus> visible, long? judgeId = null)
    {
        var competition = await db.Competitions.AsNoTracking().FirstOrDefaultAsync(c => c.Id == competitionId);
        if (competition == null || !visible.Contains(CompetitionStatusResolver.Resolve(competition))
            || (judgeId != null && !await db.CompetitionJudges.AnyAsync(j => j.CompetitionId == competitionId && j.JudgeId == judgeId)))
        {
            throw new NotFoundException($"Competition {competitionId} was not found.");
        }

        var all = db.Submissions.AsNoTracking().Where(s => s.CompetitionId == competitionId);
        var total = await all.CountAsync();
        var evaluated = judgeId == null
            ? await all.CountAsync(s => s.Evaluation != null)
            : await all.CountAsync(s => s.JudgeEvaluations.Any(e => e.JudgeId == judgeId));

        var query = all;
        if (graded is { } g)
        {
            query = judgeId == null
                ? (g ? query.Where(s => s.Evaluation != null) : query.Where(s => s.Evaluation == null))
                : (g ? query.Where(s => s.JudgeEvaluations.Any(e => e.JudgeId == judgeId)) : query.Where(s => !s.JudgeEvaluations.Any(e => e.JudgeId == judgeId)));
        }
        if (rating is { } r)
        {
            query = query.Where(s => s.Evaluation != null && s.Evaluation.Rating == r);
        }
        if (!string.IsNullOrWhiteSpace(q))
        {
            var keyword = q.Trim();
            query = query.Where(s => s.Title.Contains(keyword)
                || s.Student.Account.FullName.Contains(keyword)
                || s.Student.StudentCode.Contains(keyword));
        }

        var items = await query
            .OrderBy(s => s.SubmittedAt).ThenBy(s => s.Id)
            .Select(s => new GradingItemDto
            {
                Id = s.Id, Title = s.Title, StudentName = s.Student.Account.FullName, StudentCode = s.Student.StudentCode,
                SubmittedAt = s.SubmittedAt,
                Rating = s.Evaluation != null ? (Rating?)s.Evaluation.Rating : null,
                GradedByMe = judgeId != null && s.JudgeEvaluations.Any(e => e.JudgeId == judgeId),
                SubmissionId = s.Id, HasThumbnail = s.ThumbnailPath != null,
            })
            .ToListAsync();

        return new GradingListDto
        {
            CompetitionId = competition.Id, CompetitionTitle = competition.Title,
            Status = CompetitionStatusResolver.Resolve(competition), Total = total, Evaluated = evaluated, Items = items,
        };
    }

    /// <summary>The entry, the criteria, the judge's own grade, the jury's progress and the next entry to grade.</summary>
    /// <param name="visible">Statuses the caller may see; another status is reported as not found</param>
    /// <param name="judgeId">Teacher (must judge the competition); null for staff (read only)</param>
    /// <param name="viewer">For the score sheet: who is asking</param>
    public static async Task<GradingDetailDto> DetailAsync(AppDbContext db, IEvaluationService evaluations, long submissionId,
        IReadOnlySet<CompetitionStatus> visible, long? judgeId, ScoreSheetViewer viewer)
    {
        EvaluationPageVm page = await evaluations.GetPageAsync(submissionId, judgeId);
        if (!visible.Contains(page.CompetitionStatus))
        {
            throw new NotFoundException($"Submission {submissionId} was not found.");
        }

        var meta = await db.Submissions.AsNoTracking()
            .Where(s => s.Id == submissionId)
            .Select(s => new
            {
                s.UpdatedAt,
                HasThumbnail = s.ThumbnailPath != null,
                Result = s.Evaluation == null ? null : new { s.Evaluation.Rating, s.Evaluation.TotalScore },
                Mine = judgeId == null ? null : s.JudgeEvaluations.Where(e => e.JudgeId == judgeId)
                    .Select(e => new { e.CreatedAt, e.UpdatedAt }).FirstOrDefault(),
            })
            .FirstAsync();

        var entries = await db.Submissions.AsNoTracking()
            .Where(s => s.CompetitionId == page.CompetitionId)
            .OrderBy(s => s.SubmittedAt).ThenBy(s => s.Id)
            .Select(s => new
            {
                s.Id,
                Evaluated = judgeId == null ? s.Evaluation != null : s.JudgeEvaluations.Any(e => e.JudgeId == judgeId),
            })
            .ToListAsync();
        var index = entries.FindIndex(e => e.Id == submissionId);
        // First entry not graded (by this judge) after this one in list order, else the first one before it
        var next = entries.Skip(index + 1).Concat(entries.Take(Math.Max(index, 0)))
            .Where(e => !e.Evaluated && e.Id != submissionId)
            .Select(e => (long?)e.Id)
            .FirstOrDefault();

        var criteria = page.Criteria.ToDictionary(c => c.CompetitionCriterionId);
        return new GradingDetailDto
        {
            Id = page.SubmissionId, CompetitionId = page.CompetitionId, CompetitionTitle = page.CompetitionTitle,
            CompetitionStatus = page.CompetitionStatus, Title = page.Title, Description = page.Description,
            TextType = page.TextType, TextContent = page.TextContent, StudentName = page.StudentName,
            StudentCode = page.StudentCode, SubmittedAt = page.SubmittedAt, UpdatedAt = meta.UpdatedAt, IsLate = page.IsLate,
            CanEvaluate = judgeId != null && page.CanEvaluate,
            CanDeleteEvaluation = judgeId != null && page.CanDelete,
            LockReason = judgeId != null ? page.ReadOnlyReason : "Only the jury grades entries; staff see the published results.",
            Criteria = page.Criteria.Select(c => new GradingCriterionDto { Id = c.CompetitionCriterionId, Name = c.Name, Description = c.Description, Weight = c.Weight }).ToList(),
            Evaluation = !page.HasEvaluation || meta.Mine == null ? null : new GradingEvaluationDto
            {
                Disqualified = page.Form.Disqualified,
                Scores = page.Form.Scores.Where(s => criteria.ContainsKey(s.CompetitionCriterionId))
                    .Select(s => new GradingScoreDto { CriterionId = s.CompetitionCriterionId, Score = s.Score, Comment = s.Comment }).ToList(),
                TotalScore = page.MyTotal,
                PositivePoints = page.Form.PositivePoints, NegativePoints = page.Form.NegativePoints, ImprovementAreas = page.Form.ImprovementAreas,
                EvaluatedByName = page.EvaluatedByName ?? string.Empty,
                EvaluatedAt = page.EvaluatedAt ?? default,
                RowVersion = RowVersion.Of(meta.Mine.CreatedAt, meta.Mine.UpdatedAt),
            },
            ResultRating = meta.Result?.Rating,
            ResultScore = meta.Result?.TotalScore is { } total ? Math.Round(total, 2, MidpointRounding.AwayFromZero) : null,
            Jury = page.Jury.Select(j => new GradingJudgeDto { Name = j.Name, IsChair = j.IsChair, Done = j.Done, IsMe = j.IsMe }).ToList(),
            ScoreSheet = ScoreSheetDto.From(await evaluations.GetScoreSheetAsync(submissionId, viewer)),
            ImageIds = page.ImageIds.ToList(),
            NextUnevaluatedId = next,
            SubmissionId = page.SubmissionId,
            HasThumbnail = meta.HasThumbnail,
            Total = entries.Count,
            Evaluated = entries.Count(e => e.Evaluated),
        };
    }

    /// <summary>Change times of the judge's grade of an entry, null when none (for the rowVersion check).</summary>
    public static async Task<(DateTime CreatedAt, DateTime? UpdatedAt)?> EvaluationStampAsync(AppDbContext db, long submissionId, long judgeId)
    {
        var stamp = await db.JudgeEvaluations.AsNoTracking()
            .Where(e => e.SubmissionId == submissionId && e.JudgeId == judgeId)
            .Select(e => new { e.CreatedAt, e.UpdatedAt })
            .FirstOrDefaultAsync();
        return stamp == null ? null : (stamp.CreatedAt, stamp.UpdatedAt);
    }
}
