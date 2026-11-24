using InstituteOfFineArts.Web.Data;
using InstituteOfFineArts.Web.Helpers;
using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace InstituteOfFineArts.Web.Services.Implementations;

/// <summary>
/// Turns the judges' grades of a competition into the result of each entry (table evaluation), shared by grading,
/// jury changes and publishing.
/// <para>
/// An entry is complete when every member of the jury has graded it. Then its evaluation row holds the result:
/// DISQUALIFIED when a judge disqualified it (their reasons in negative_points), otherwise the average of the judges'
/// weighted totals (<see cref="GradingMath"/>) and the rating from the thresholds. An incomplete entry has no
/// evaluation row, so it counts as "not graded" everywhere (progress, BR-08 before publishing).
/// </para>
/// A competition without a jury is left alone: its evaluations, if any, were made before juries existed.
/// </summary>
public static class JuryResults
{
    /// <summary>Result of one entry from the judges' grades.</summary>
    /// <param name="SubmissionId">Entry</param>
    /// <param name="JudgesDone">Judges of the jury who graded it</param>
    /// <param name="JuryCount">Members of the jury</param>
    /// <param name="Disqualified">A judge disqualified it</param>
    /// <param name="Average">Average weighted total, full precision (null when disqualified or nobody graded)</param>
    /// <param name="Reasons">Disqualification reasons, one per judge who disqualified</param>
    public sealed record EntryResult(long SubmissionId, int JudgesDone, int JuryCount, bool Disqualified, decimal? Average,
        IReadOnlyList<string> Reasons)
    {
        /// <summary>Every member of the jury graded it.</summary>
        public bool Complete => JuryCount > 0 && JudgesDone >= JuryCount;
    }

    /// <summary>The institute's thresholds (defaults for the missing ones).</summary>
    public static async Task<Dictionary<Rating, decimal>> ThresholdsAsync(AppDbContext db)
    {
        var stored = await db.RatingThresholds.AsNoTracking().ToDictionaryAsync(t => t.Rating, t => t.MinScore);
        return GradingMath.ThresholdRatings.ToDictionary(r => r, r => stored.TryGetValue(r, out var v) ? v : GradingMath.DefaultThresholds[r]);
    }

    /// <summary>Results of the entries of a competition (or of one entry), from the grades of the current jury.</summary>
    public static async Task<Dictionary<long, EntryResult>> CalculateAsync(AppDbContext db, long competitionId, long? submissionId = null)
    {
        var jury = await db.CompetitionJudges.AsNoTracking().Where(j => j.CompetitionId == competitionId).Select(j => j.JudgeId).ToListAsync();
        var weights = await db.CompetitionCriteria.AsNoTracking().Where(c => c.CompetitionId == competitionId)
            .ToDictionaryAsync(c => c.Id, c => c.WeightPercent);
        var entries = await db.Submissions.AsNoTracking()
            .Where(s => s.CompetitionId == competitionId && (submissionId == null || s.Id == submissionId))
            .Select(s => s.Id).ToListAsync();
        var grades = await db.JudgeEvaluations.AsNoTracking()
            .Where(e => e.Submission.CompetitionId == competitionId && (submissionId == null || e.SubmissionId == submissionId)
                        && jury.Contains(e.JudgeId))
            .Select(e => new
            {
                e.SubmissionId, e.Disqualified, e.NegativePoints,
                Scores = e.Scores.Select(s => new { s.CompetitionCriterionId, s.Score }).ToList(),
            })
            .ToListAsync();

        var byEntry = grades.ToLookup(g => g.SubmissionId);
        return entries.ToDictionary(id => id, id =>
        {
            var mine = byEntry[id].ToList();
            var disqualified = mine.Any(g => g.Disqualified);
            var totals = mine.Where(g => !g.Disqualified)
                .Select(g => GradingMath.JudgeTotal(g.Scores.Select(s => (s.Score, weights.GetValueOrDefault(s.CompetitionCriterionId)))))
                .ToList();
            return new EntryResult(id, mine.Count, jury.Count, disqualified, disqualified ? null : GradingMath.Average(totals),
                mine.Where(g => g.Disqualified && g.NegativePoints != null).Select(g => g.NegativePoints!).ToList());
        });
    }

    /// <summary>
    /// Writes the result rows (evaluation) of a competition, or of one entry, from the judges' grades: complete entries
    /// get or update their row, incomplete ones lose it. Saves the changes. Nothing happens without a jury.
    /// </summary>
    public static async Task RecomputeAsync(AppDbContext db, long competitionId, long? submissionId = null)
    {
        var judges = await db.CompetitionJudges.AsNoTracking().Where(j => j.CompetitionId == competitionId)
            .OrderByDescending(j => j.IsChair).ThenBy(j => j.Id).Select(j => j.JudgeId).ToListAsync();
        if (judges.Count == 0)
        {
            return;
        }
        var thresholds = await ThresholdsAsync(db);
        var results = await CalculateAsync(db, competitionId, submissionId);
        var ids = results.Keys.ToList();
        var existing = await db.Evaluations.Where(e => ids.Contains(e.SubmissionId)).ToDictionaryAsync(e => e.SubmissionId);
        var now = DateTimeHelper.Now;

        foreach (var result in results.Values)
        {
            existing.TryGetValue(result.SubmissionId, out var evaluation);
            if (!result.Complete)
            {
                if (evaluation != null)
                {
                    db.Evaluations.Remove(evaluation);
                }
                continue;
            }

            if (evaluation == null)
            {
                evaluation = new Evaluation { SubmissionId = result.SubmissionId, EvaluatedBy = judges[0] };
                db.Evaluations.Add(evaluation);
            }
            evaluation.EvaluatedBy = judges[0];
            evaluation.EvaluatedAt = now;
            evaluation.PositivePoints = null;
            evaluation.ImprovementAreas = null;
            if (result.Disqualified)
            {
                evaluation.Rating = Rating.DISQUALIFIED;
                evaluation.TotalScore = null;
                evaluation.NegativePoints = Cut(string.Join(" | ", result.Reasons.DefaultIfEmpty("Disqualified by the jury.")), 2000);
            }
            else
            {
                var average = result.Average ?? 0m;
                evaluation.Rating = GradingMath.RatingOf(average, thresholds);
                evaluation.TotalScore = Math.Round(average, 4);
                evaluation.NegativePoints = null;
            }
        }
        await db.SaveChangesAsync();
    }

    /// <summary>Cuts a text to a maximum length.</summary>
    private static string Cut(string text, int max) => text.Length <= max ? text : text[..max];
}
