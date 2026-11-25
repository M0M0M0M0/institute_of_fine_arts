using InstituteOfFineArts.Web.Data;
using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Helpers;
using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.Services.Interfaces;
using InstituteOfFineArts.Web.ViewModels.Staff.Evaluations;
using Microsoft.EntityFrameworkCore;

namespace InstituteOfFineArts.Web.Services.Implementations;

/// <summary>
/// Grading by the jury (F-M07-03..05, F-M07-08; TODO steps 4–5).
/// <list type="bullet">
/// <item>Only the teachers on the jury of the competition grade its entries, each on their own (one judge_evaluation
/// per judge and entry). Nobody grading sees an entry before the deadline (404 while UPCOMING / ONGOING).</item>
/// <item>A grade is a score 0–100 (2 decimals at most) and an optional comment for every criterion of the
/// competition, plus general comments (positive points required, BR-23). Or the judge disqualifies the entry with a
/// reason; an entry submitted after the deadline can only be disqualified (BR-14).</item>
/// <item>Grades are made while the competition is CLOSED (BR-21) and are read only once it is GRADED.</item>
/// <item>After each change the entry's result (table evaluation) is recalculated (JuryResults): it exists once every
/// judge has graded, with the jury's average and the rating from the thresholds.</item>
/// <item>Judges grade independently: the page shows who in the jury has graded, never the others' scores; the whole
/// score sheet is shown after publishing (<see cref="GetScoreSheetAsync"/>).</item>
/// </list>
/// </summary>
public class EvaluationService : IEvaluationService
{
    /// <summary>Message when the competition is still UPCOMING / ONGOING (BR-21).</summary>
    public const string NotClosedMessage = "Entries can only be evaluated after the submission deadline.";

    /// <summary>Message when the competition results are published.</summary>
    public const string GradedReadOnlyMessage = "Results have been published; evaluations can no longer be changed.";

    /// <summary>Message when a late entry is graded instead of disqualified (BR-14).</summary>
    public const string LateOnlyDisqualifiedMessage = "This entry was submitted after the deadline and can only be disqualified.";

    /// <summary>Message when a comment is longer than the form allows.</summary>
    public const string CommentTooLongMessage = "Each comment must be at most 2000 characters";

    /// <summary>Message when a criterion comment is too long.</summary>
    public const string CriterionCommentTooLongMessage = "Each criterion comment must be at most 1000 characters";

    /// <summary>Message when deleting a grade that does not exist.</summary>
    public const string NoEvaluationMessage = "You have not graded this entry yet.";

    /// <summary>Message when the teacher is not on the jury.</summary>
    public const string NotJudgeMessage = "You are not on the jury of this competition.";

    /// <summary>Message when the competition has no criteria yet.</summary>
    public const string NoCriteriaMessage = "This competition has no grading criteria yet. Ask the staff to add them.";

    /// <summary>Message when the same judge saved the same entry at the same moment (two tabs).</summary>
    public const string ConcurrentCreateMessage = "You have just graded this entry in another window. Please review your grade and save again.";

    /// <summary>Message when the grade was changed after the page was opened (BA-07).</summary>
    public const string ChangedMeanwhileMessage = "Your grade was changed in another window while you were editing it. Reload the page to see the latest version, then save again.";

    /// <summary>Database access.</summary>
    private readonly AppDbContext _db;

    /// <summary>Logs saved / deleted grades.</summary>
    private readonly ILogger<EvaluationService> _logger;

    /// <summary>Receives the required services through Dependency Injection.</summary>
    public EvaluationService(AppDbContext db, ILogger<EvaluationService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<EvaluationPageVm> GetPageAsync(long submissionId, long? judgeId)
    {
        var data = await _db.Submissions.AsNoTracking()
            .Where(s => s.Id == submissionId)
            .Select(s => new
            {
                s.Id, s.Title, s.SubmittedAt, s.TextType, s.TextContent, s.Description,
                StudentName = s.Student.Account.FullName,
                s.Student.StudentCode,
                s.Competition,
                ImageIds = s.Images.OrderByDescending(i => i.IsCover).ThenBy(i => i.SortOrder).ThenBy(i => i.Id).Select(i => i.Id).ToList(),
            })
            .FirstOrDefaultAsync()
            ?? throw new NotFoundException($"Submission {submissionId} was not found.");

        // Nobody grading sees a painting before the deadline
        var status = CompetitionStatusResolver.Resolve(data.Competition);
        if (status is CompetitionStatus.UPCOMING or CompetitionStatus.ONGOING)
        {
            throw new NotFoundException($"Submission {submissionId} was not found.");
        }

        var jury = await JuryAsync(data.Competition.Id);
        var isJudge = judgeId is { } me && jury.Any(j => j.JudgeId == me);
        if (judgeId != null && !isJudge)
        {
            // A teacher who is not on this jury does not see its entries
            throw new NotFoundException($"Submission {submissionId} was not found.");
        }

        var criteria = await CriteriaAsync(data.Competition.Id);
        var grades = await _db.JudgeEvaluations.AsNoTracking()
            .Where(e => e.SubmissionId == submissionId)
            .Select(e => new { e.JudgeId, e.Id })
            .ToListAsync();
        var done = grades.Select(g => g.JudgeId).ToHashSet();
        var isLate = CompetitionStatusResolver.IsLateSubmission(data.SubmittedAt, data.Competition.EndDate);

        var page = new EvaluationPageVm
        {
            SubmissionId = data.Id, Title = data.Title, StudentName = data.StudentName, StudentCode = data.StudentCode,
            CompetitionId = data.Competition.Id, CompetitionTitle = data.Competition.Title, CompetitionCode = data.Competition.Code,
            CompetitionStatus = status, EndDate = data.Competition.EndDate, SubmittedAt = data.SubmittedAt, IsLate = isLate,
            TextType = data.TextType, TextContent = data.TextContent, Description = data.Description,
            ImageIds = data.ImageIds, Criteria = criteria, IsJudge = isJudge,
            Jury = jury.Select(j => new JuryProgressVm(j.Name, j.IsChair, done.Contains(j.JudgeId), j.JudgeId == judgeId)).ToList(),
        };
        page.ReadOnlyReason = !isJudge ? NotJudgeMessage
            : status == CompetitionStatus.GRADED ? GradedReadOnlyMessage
            : criteria.Count == 0 ? NoCriteriaMessage
            : null;
        page.CanEvaluate = page.ReadOnlyReason == null;

        // The judge's own grade in the form
        var mine = judgeId == null ? null : await _db.JudgeEvaluations.AsNoTracking()
            .Include(e => e.Scores)
            .Include(e => e.Judge).ThenInclude(j => j.Account)
            .FirstOrDefaultAsync(e => e.SubmissionId == submissionId && e.JudgeId == judgeId);
        if (mine != null)
        {
            page.HasEvaluation = true;
            page.EvaluatedAt = mine.EvaluatedAt;
            page.EvaluatedByName = mine.Judge.Account.FullName;
            page.MyTotal = mine.TotalScore;
            var scores = mine.Scores.ToDictionary(s => s.CompetitionCriterionId);
            page.Form = new EvaluationFormVm
            {
                Disqualified = mine.Disqualified,
                PositivePoints = RichText.ToHtml(mine.PositivePoints), NegativePoints = RichText.ToHtml(mine.NegativePoints), ImprovementAreas = RichText.ToHtml(mine.ImprovementAreas),
                Scores = criteria.Select(c => new CriterionScoreVm
                {
                    CompetitionCriterionId = c.CompetitionCriterionId,
                    Score = scores.TryGetValue(c.CompetitionCriterionId, out var s) ? s.Score : null,
                    Comment = scores.TryGetValue(c.CompetitionCriterionId, out var t) ? t.Comment : null,
                }).ToList(),
            };
            page.CanDelete = isJudge && status == CompetitionStatus.CLOSED;
            page.DeleteBlockedReason = page.CanDelete ? null : status == CompetitionStatus.GRADED ? GradedReadOnlyMessage : NotJudgeMessage;
        }
        else
        {
            page.Form = new EvaluationFormVm
            {
                Scores = criteria.Select(c => new CriterionScoreVm { CompetitionCriterionId = c.CompetitionCriterionId }).ToList(),
            };
            if (isLate)
            {
                // F-M07-08: a late entry is suggested disqualified with the reason already written
                page.Form.Disqualified = true;
                page.Form.NegativePoints = $"Submitted after the submission deadline ({data.Competition.EndDate:dd MMM yyyy}, 23:59:59).";
            }
        }
        page.Form.Version = mine == null ? string.Empty : Version(mine.CreatedAt, mine.UpdatedAt);

        // After publishing, the jury sees the whole score sheet
        if (status == CompetitionStatus.GRADED)
        {
            page.ScoreSheet = await BuildScoreSheetAsync(submissionId, data.Competition.Id, criteria);
        }
        return page;
    }

    /// <inheritdoc />
    public async Task<bool> SaveAsync(long submissionId, long judgeId, EvaluationFormVm form)
    {
        var submission = await _db.Submissions.Include(s => s.Competition).FirstOrDefaultAsync(s => s.Id == submissionId)
            ?? throw new NotFoundException($"Submission {submissionId} was not found.");

        // BR-21: only after the deadline and before the results are published
        var status = CompetitionStatusResolver.Resolve(submission.Competition);
        if (EditBlockedReason(status) is { } reason)
        {
            throw new BusinessException(reason);
        }
        if (!await _db.CompetitionJudges.AnyAsync(j => j.CompetitionId == submission.CompetitionId && j.JudgeId == judgeId))
        {
            throw new BusinessException(NotJudgeMessage);
        }
        var criteria = await _db.CompetitionCriteria.AsNoTracking().Where(c => c.CompetitionId == submission.CompetitionId)
            .ToDictionaryAsync(c => c.Id, c => c.WeightPercent);
        if (criteria.Count == 0)
        {
            throw new BusinessException(NoCriteriaMessage);
        }

        var (scores, positive, negative, improvement) = Validate(form, criteria.Keys.ToHashSet());

        // BR-14: an entry submitted after the deadline can only be disqualified
        if (!form.Disqualified && CompetitionStatusResolver.IsLateSubmission(submission.SubmittedAt, submission.Competition.EndDate))
        {
            throw new BusinessException(LateOnlyDisqualifiedMessage, nameof(EvaluationFormVm.Disqualified));
        }

        // BA-07: refuse to overwrite a grade that changed (or appeared) after the page was opened
        var grade = await _db.JudgeEvaluations.Include(e => e.Scores)
            .FirstOrDefaultAsync(e => e.SubmissionId == submissionId && e.JudgeId == judgeId);
        if (form.Version != null && form.Version != (grade == null ? string.Empty : Version(grade.CreatedAt, grade.UpdatedAt)))
        {
            throw new BusinessException(grade != null && form.Version == string.Empty ? ConcurrentCreateMessage : ChangedMeanwhileMessage);
        }

        var created = grade == null;
        if (grade == null)
        {
            grade = new JudgeEvaluation { SubmissionId = submissionId, JudgeId = judgeId };
            _db.JudgeEvaluations.Add(grade);
        }
        grade.Disqualified = form.Disqualified;
        grade.PositivePoints = positive;
        grade.NegativePoints = negative;
        grade.ImprovementAreas = improvement;
        grade.EvaluatedAt = DateTimeHelper.Now;
        // Scores: update the rows in place, add the missing ones, remove the others (a disqualification has none);
        // updating in place avoids a delete + insert on the unique (grade, criterion) index in one save
        var existing = grade.Scores.ToDictionary(s => s.CompetitionCriterionId);
        foreach (var s in scores)
        {
            if (existing.Remove(s.Id, out var row))
            {
                row.Score = s.Score;
                row.Comment = s.Comment;
            }
            else
            {
                grade.Scores.Add(new JudgeScore { CompetitionCriterionId = s.Id, Score = s.Score, Comment = s.Comment });
            }
        }
        _db.JudgeScores.RemoveRange(existing.Values);
        grade.TotalScore = form.Disqualified ? null : Math.Round(GradingMath.JudgeTotal(scores.Select(s => (s.Score, criteria[s.Id]))), 2, MidpointRounding.AwayFromZero);
        // An unchanged row would keep its version: mark it modified so the version always moves on a save
        if (!created)
        {
            _db.Entry(grade).State = EntityState.Modified;
        }

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException) when (created)
        {
            // Unique index UX_judge_evaluation: the same judge saved first in another window
            throw new BusinessException(ConcurrentCreateMessage);
        }

        await JuryResults.RecomputeAsync(_db, submission.CompetitionId, submissionId);
        _logger.LogInformation("Submission {SubmissionId} {Action} by judge {JudgeId} ({Result})", submissionId,
            created ? "graded" : "re-graded", judgeId, form.Disqualified ? "disqualified" : grade.TotalScore?.ToString("0.00"));
        return created;
    }

    /// <inheritdoc />
    public async Task DeleteAsync(long submissionId, long judgeId)
    {
        var submission = await _db.Submissions.Include(s => s.Competition).FirstOrDefaultAsync(s => s.Id == submissionId)
            ?? throw new NotFoundException($"Submission {submissionId} was not found.");
        var grade = await _db.JudgeEvaluations.FirstOrDefaultAsync(e => e.SubmissionId == submissionId && e.JudgeId == judgeId)
            ?? throw new BusinessException(NoEvaluationMessage);
        if (EditBlockedReason(CompetitionStatusResolver.Resolve(submission.Competition)) is { } reason)
        {
            throw new BusinessException(reason);
        }

        _db.JudgeEvaluations.Remove(grade);
        await _db.SaveChangesAsync();
        await JuryResults.RecomputeAsync(_db, submission.CompetitionId, submissionId);
        _logger.LogInformation("Grade of submission {SubmissionId} by judge {JudgeId} deleted", submissionId, judgeId);
    }

    /// <inheritdoc />
    public async Task<ScoreSheetVm?> GetScoreSheetAsync(long submissionId, ScoreSheetViewer viewer)
    {
        var data = await _db.Submissions.AsNoTracking().Where(s => s.Id == submissionId)
            .Select(s => new { s.StudentId, s.Competition, Complete = s.Evaluation != null })
            .FirstOrDefaultAsync();
        if (data == null)
        {
            return null;
        }
        var graded = CompetitionStatusResolver.Resolve(data.Competition) == CompetitionStatus.GRADED;
        var allowed = viewer.Role switch
        {
            RoleNames.Student => graded && viewer.ProfileId == data.StudentId,
            RoleNames.Teacher => graded && await _db.CompetitionJudges.AnyAsync(j => j.CompetitionId == data.Competition.Id && j.JudgeId == viewer.ProfileId),
            RoleNames.Staff => graded || data.Complete,
            _ => false,
        };
        return allowed ? await BuildScoreSheetAsync(submissionId, data.Competition.Id, await CriteriaAsync(data.Competition.Id)) : null;
    }

    /// <summary>The score sheet of an entry: each judge's scores and comments, averages, total and rating.</summary>
    private async Task<ScoreSheetVm> BuildScoreSheetAsync(long submissionId, long competitionId, IReadOnlyList<CriterionLineVm> criteria)
    {
        var chairs = await _db.CompetitionJudges.AsNoTracking().Where(j => j.CompetitionId == competitionId && j.IsChair)
            .Select(j => j.JudgeId).ToListAsync();
        var grades = await _db.JudgeEvaluations.AsNoTracking()
            .Where(e => e.SubmissionId == submissionId)
            .Select(e => new
            {
                e.JudgeId, Name = e.Judge.Account.FullName, e.Disqualified, e.TotalScore, e.PositivePoints, e.NegativePoints,
                e.ImprovementAreas, e.EvaluatedAt,
                Scores = e.Scores.Select(s => new { s.CompetitionCriterionId, s.Score, s.Comment }).ToList(),
            })
            .ToListAsync();
        var result = await _db.Evaluations.AsNoTracking().Where(e => e.SubmissionId == submissionId)
            .Select(e => new { e.Rating, e.TotalScore }).FirstOrDefaultAsync();

        var judges = grades
            .OrderByDescending(g => chairs.Contains(g.JudgeId)).ThenBy(g => g.Name)
            .Select(g => new JudgeColumnVm
            {
                Name = g.Name, IsChair = chairs.Contains(g.JudgeId), Disqualified = g.Disqualified, Total = g.TotalScore,
                PositivePoints = g.PositivePoints, NegativePoints = g.NegativePoints, ImprovementAreas = g.ImprovementAreas,
                EvaluatedAt = g.EvaluatedAt,
                Scores = g.Scores.ToDictionary(s => s.CompetitionCriterionId, s => (s.Score, s.Comment)),
            })
            .ToList();
        var averages = criteria.ToDictionary(c => c.CompetitionCriterionId, c =>
        {
            var values = judges.Where(j => j.Scores.ContainsKey(c.CompetitionCriterionId)).Select(j => j.Scores[c.CompetitionCriterionId].Score).ToList();
            return values.Count == 0 ? (decimal?)null : Math.Round(values.Average(), 2, MidpointRounding.AwayFromZero);
        }).Where(x => x.Value != null).ToDictionary(x => x.Key, x => x.Value!.Value);

        return new ScoreSheetVm
        {
            Criteria = criteria, Judges = judges, CriterionAverages = averages,
            Total = result?.TotalScore is { } t ? Math.Round(t, 2, MidpointRounding.AwayFromZero) : null,
            Rating = result?.Rating,
            Legacy = judges.Count == 0 && result != null,
        };
    }

    /// <summary>Jury of a competition with names, chair first.</summary>
    private async Task<List<(long JudgeId, string Name, bool IsChair)>> JuryAsync(long competitionId)
        => (await _db.CompetitionJudges.AsNoTracking().Where(j => j.CompetitionId == competitionId)
                .OrderByDescending(j => j.IsChair).ThenBy(j => j.Judge.Account.FullName)
                .Select(j => new { j.JudgeId, j.Judge.Account.FullName, j.IsChair }).ToListAsync())
            .Select(j => (j.JudgeId, j.FullName, j.IsChair)).ToList();

    /// <summary>Criteria of a competition in display order.</summary>
    private async Task<IReadOnlyList<CriterionLineVm>> CriteriaAsync(long competitionId)
        => await _db.CompetitionCriteria.AsNoTracking().Where(c => c.CompetitionId == competitionId)
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Id)
            .Select(c => new CriterionLineVm(c.Id, c.Criterion.Name, c.Criterion.Description, c.WeightPercent))
            .ToListAsync();

    /// <summary>Version token of a grade: its last change time in whole seconds (the column keeps no fractions).</summary>
    internal static string Version(DateTime createdAt, DateTime? updatedAt)
    {
        var ticks = (updatedAt ?? createdAt).Ticks;
        return (ticks - ticks % TimeSpan.TicksPerSecond).ToString();
    }

    /// <summary>
    /// Checks the form again (no caller can skip the rules) and returns the scores and the trimmed comments.
    /// Disqualified: a reason, no scores. Otherwise: every criterion of the competition once, 0–100 with at most 2
    /// decimals, and the positive points.
    /// </summary>
    private static (List<(long Id, decimal Score, string? Comment)> Scores, string? Positive, string? Negative, string? Improvement)
        Validate(EvaluationFormVm form, HashSet<long> criteria)
    {
        // Rich text (BR-50): sanitized HTML, the limit counts the text
        var positive = CheckComment(form.PositivePoints, nameof(EvaluationFormVm.PositivePoints));
        var negative = CheckComment(form.NegativePoints, nameof(EvaluationFormVm.NegativePoints));
        var improvement = CheckComment(form.ImprovementAreas, nameof(EvaluationFormVm.ImprovementAreas));

        if (form.Disqualified)
        {
            if (negative == null)
            {
                throw new BusinessException(EvaluationFormVm.DisqualifiedReasonMessage, nameof(EvaluationFormVm.NegativePoints));
            }
            return ([], positive, negative, improvement);
        }

        var scores = new List<(long, decimal, string?)>();
        var seen = new HashSet<long>();
        for (var i = 0; i < form.Scores.Count; i++)
        {
            var line = form.Scores[i];
            var field = $"{nameof(EvaluationFormVm.Scores)}[{i}].{nameof(CriterionScoreVm.Score)}";
            if (!criteria.Contains(line.CompetitionCriterionId) || !seen.Add(line.CompetitionCriterionId))
            {
                throw new BusinessException("This criterion does not belong to the competition. Reload the page and try again.", field);
            }
            if (line.Score is not { } score)
            {
                throw new BusinessException(EvaluationFormVm.ScoreRequiredMessage, field);
            }
            if (score is < 0 or > 100 || decimal.Round(score, 2) != score)
            {
                throw new BusinessException(EvaluationFormVm.ScoreRangeMessage, field);
            }
            var comment = Clean(line.Comment);
            if (comment?.Length > EvaluationFormVm.MaxCriterionCommentLength)
            {
                throw new BusinessException(CriterionCommentTooLongMessage, $"{nameof(EvaluationFormVm.Scores)}[{i}].{nameof(CriterionScoreVm.Comment)}");
            }
            scores.Add((line.CompetitionCriterionId, score, comment));
        }
        if (seen.Count != criteria.Count)
        {
            throw new BusinessException(EvaluationFormVm.ScoreRequiredMessage, nameof(EvaluationFormVm.Scores));
        }
        if (positive == null)
        {
            throw new BusinessException(EvaluationFormVm.PositivePointsMessage, nameof(EvaluationFormVm.PositivePoints));
        }
        return (scores, positive, null == negative ? null : negative, improvement);
    }

    /// <summary>A general comment from the rich text editor: sanitized, null when empty; throws when it is too long.</summary>
    private static string? CheckComment(string? posted, string field)
        => RichText.CleanChecked(posted, EvaluationFormVm.MaxCommentLength, field, CommentTooLongMessage);

    /// <summary>Why a grade cannot be created / edited / deleted in this status (null = CLOSED, allowed).</summary>
    private static string? EditBlockedReason(CompetitionStatus status) => status switch
    {
        CompetitionStatus.CLOSED => null,
        CompetitionStatus.GRADED => GradedReadOnlyMessage,
        _ => NotClosedMessage,
    };

    /// <summary>Trims text; blank becomes null.</summary>
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
