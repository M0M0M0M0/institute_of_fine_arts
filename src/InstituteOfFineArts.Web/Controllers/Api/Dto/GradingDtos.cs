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
    public int EvaluatedCount { get; set; }
}

/// <summary>One row of the entries to grade; Rating null = not evaluated.</summary>
public class GradingItemDto
{
    public long Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string StudentCode { get; set; } = string.Empty;
    public DateTime SubmittedAt { get; set; }
    public Rating? Rating { get; set; }
}

/// <summary>Entries of one competition; Total / Evaluated are the progress of the whole competition (not filtered).</summary>
public class GradingListDto
{
    public long CompetitionId { get; set; }
    public string CompetitionTitle { get; set; } = string.Empty;
    public CompetitionStatus Status { get; set; }
    public int Total { get; set; }
    public int Evaluated { get; set; }
    public List<GradingItemDto> Items { get; set; } = [];
}

/// <summary>Current evaluation of an entry; RowVersion is sent back to edit or delete it.</summary>
public class GradingEvaluationDto
{
    public Rating Rating { get; set; }
    public string? PositivePoints { get; set; }
    public string? NegativePoints { get; set; }
    public string? ImprovementAreas { get; set; }
    public string EvaluatedByName { get; set; } = string.Empty;
    public DateTime EvaluatedAt { get; set; }
    public string RowVersion { get; set; } = string.Empty;
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
    public bool CanEvaluate { get; set; }
    public bool CanDeleteEvaluation { get; set; }
    public string? LockReason { get; set; }
    public GradingEvaluationDto? Evaluation { get; set; }
    public long? NextUnevaluatedId { get; set; }
    public int Total { get; set; }
    public int Evaluated { get; set; }
}

/// <summary>Body of PUT /api/staff/submissions/{id}/evaluation.</summary>
public class GradingEvaluationRequest
{
    public Rating? Rating { get; set; }
    public string? PositivePoints { get; set; }
    public string? NegativePoints { get; set; }
    public string? ImprovementAreas { get; set; }

    /// <summary>Required when the entry already has an evaluation.</summary>
    public string? RowVersion { get; set; }
}

/// <summary>Read side of the grading API (AsNoTracking queries + the evaluation page of the web).</summary>
public static class GradingQueries
{
    /// <summary>Competitions with entries or CLOSED / GRADED, with their grading progress; ready to grade first.</summary>
    public static async Task<List<GradingCompetitionDto>> CompetitionsAsync(AppDbContext db)
    {
        var competitions = await db.Competitions.AsNoTracking().ToListAsync();
        var counts = await db.Submissions.AsNoTracking()
            .GroupBy(s => s.CompetitionId)
            .Select(g => new { Id = g.Key, Total = g.Count(), Evaluated = g.Count(s => s.Evaluation != null) })
            .ToDictionaryAsync(x => x.Id);

        return competitions
            .Select(c => new
            {
                Competition = c,
                Status = CompetitionStatusResolver.Resolve(c),
                Total = counts.TryGetValue(c.Id, out var n) ? n.Total : 0,
                Evaluated = counts.TryGetValue(c.Id, out var m) ? m.Evaluated : 0,
            })
            .Where(x => x.Total > 0 || x.Status is CompetitionStatus.CLOSED or CompetitionStatus.GRADED)
            // CLOSED (needs grading) first, then the rest; newest end date first
            .OrderBy(x => x.Status == CompetitionStatus.CLOSED ? 0 : 1)
            .ThenByDescending(x => x.Competition.EndDate)
            .Select(x => new GradingCompetitionDto
            {
                Id = x.Competition.Id,
                Title = x.Competition.Title,
                EndDate = x.Competition.EndDate,
                Status = x.Status,
                SubmissionCount = x.Total,
                EvaluatedCount = x.Evaluated,
            })
            .ToList();
    }

    /// <summary>All entries of a competition matching the filters, oldest submission first.</summary>
    public static async Task<GradingListDto> ListAsync(AppDbContext db, long competitionId, bool? graded, Rating? rating, string? q)
    {
        var competition = await db.Competitions.AsNoTracking().FirstOrDefaultAsync(c => c.Id == competitionId)
            ?? throw new NotFoundException($"Competition {competitionId} was not found.");

        var all = db.Submissions.AsNoTracking().Where(s => s.CompetitionId == competitionId);
        var total = await all.CountAsync();
        var evaluated = await all.CountAsync(s => s.Evaluation != null);

        var query = all;
        if (graded is { } g)
        {
            query = g ? query.Where(s => s.Evaluation != null) : query.Where(s => s.Evaluation == null);
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
                Id = s.Id,
                Title = s.Title,
                StudentName = s.Student.Account.FullName,
                StudentCode = s.Student.StudentCode,
                SubmittedAt = s.SubmittedAt,
                Rating = s.Evaluation != null ? (Rating?)s.Evaluation.Rating : null,
            })
            .ToListAsync();

        return new GradingListDto
        {
            CompetitionId = competition.Id,
            CompetitionTitle = competition.Title,
            Status = CompetitionStatusResolver.Resolve(competition),
            Total = total,
            Evaluated = evaluated,
            Items = items,
        };
    }

    /// <summary>The entry with its evaluation, what staff may do now (from the web's evaluation page) and the next entry to grade.</summary>
    public static async Task<GradingDetailDto> DetailAsync(AppDbContext db, IEvaluationService evaluations, long submissionId)
    {
        EvaluationPageVm page = await evaluations.GetPageAsync(submissionId);

        var meta = await db.Submissions.AsNoTracking()
            .Where(s => s.Id == submissionId)
            .Select(s => new
            {
                s.UpdatedAt,
                Evaluation = s.Evaluation == null ? null : new
                {
                    s.Evaluation.Rating,
                    s.Evaluation.PositivePoints,
                    s.Evaluation.NegativePoints,
                    s.Evaluation.ImprovementAreas,
                    s.Evaluation.EvaluatedAt,
                    s.Evaluation.CreatedAt,
                    EvaluationUpdatedAt = s.Evaluation.UpdatedAt,
                },
            })
            .FirstAsync();

        var entries = await db.Submissions.AsNoTracking()
            .Where(s => s.CompetitionId == page.CompetitionId)
            .OrderBy(s => s.SubmittedAt).ThenBy(s => s.Id)
            .Select(s => new { s.Id, Evaluated = s.Evaluation != null })
            .ToListAsync();
        var index = entries.FindIndex(e => e.Id == submissionId);
        // First unevaluated entry after this one in list order, else the first unevaluated one before it
        var next = entries.Skip(index + 1).Concat(entries.Take(Math.Max(index, 0)))
            .Where(e => !e.Evaluated && e.Id != submissionId)
            .Select(e => (long?)e.Id)
            .FirstOrDefault();

        return new GradingDetailDto
        {
            Id = page.SubmissionId,
            CompetitionId = page.CompetitionId,
            CompetitionTitle = page.CompetitionTitle,
            CompetitionStatus = page.CompetitionStatus,
            Title = page.Title,
            Description = page.Description,
            TextType = page.TextType,
            TextContent = page.TextContent,
            StudentName = page.StudentName,
            StudentCode = page.StudentCode,
            SubmittedAt = page.SubmittedAt,
            UpdatedAt = meta.UpdatedAt,
            CanEvaluate = page.CanEvaluate,
            CanDeleteEvaluation = page.CanDelete,
            LockReason = page.ReadOnlyReason,
            Evaluation = meta.Evaluation == null ? null : new GradingEvaluationDto
            {
                Rating = meta.Evaluation.Rating,
                PositivePoints = meta.Evaluation.PositivePoints,
                NegativePoints = meta.Evaluation.NegativePoints,
                ImprovementAreas = meta.Evaluation.ImprovementAreas,
                EvaluatedByName = page.EvaluatedByName ?? string.Empty,
                EvaluatedAt = meta.Evaluation.EvaluatedAt,
                RowVersion = RowVersion.Of(meta.Evaluation.CreatedAt, meta.Evaluation.EvaluationUpdatedAt),
            },
            NextUnevaluatedId = next,
            Total = entries.Count,
            Evaluated = entries.Count(e => e.Evaluated),
        };
    }

    /// <summary>Change times of the entry's evaluation, null when it has none (for the rowVersion check).</summary>
    public static async Task<(DateTime CreatedAt, DateTime? UpdatedAt)?> EvaluationStampAsync(AppDbContext db, long submissionId)
    {
        var stamp = await db.Evaluations.AsNoTracking()
            .Where(e => e.SubmissionId == submissionId)
            .Select(e => new { e.CreatedAt, e.UpdatedAt })
            .FirstOrDefaultAsync();
        return stamp == null ? null : (stamp.CreatedAt, stamp.UpdatedAt);
    }
}
