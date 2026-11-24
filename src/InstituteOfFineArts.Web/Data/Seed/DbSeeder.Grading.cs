using InstituteOfFineArts.Web.Helpers;
using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace InstituteOfFineArts.Web.Data.Seed;

/// <summary>
/// Sample data of the jury grading (steps 4–6, 11/2026): the criteria library, a jury and weighted criteria for every
/// sample competition, each judge's scores (matching the rating the sample entry already had), the final result of each
/// entry, the awards handed out by <see cref="GradingMath"/> and the pictures of each entry (submission_image).
/// </summary>
public static partial class DbSeeder
{
    /// <summary>Criteria library of the institute (name, description).</summary>
    private static readonly (string Name, string Description)[] CriteriaLibrary =
    [
        ("Creativity", "Original idea and a personal way of seeing the subject."),
        ("Technique", "Control of the medium: brushwork, line, blending, detail."),
        ("Composition", "Balance, focal point and how the eye moves through the picture."),
        ("Theme relevance", "How well the work answers the theme of the competition."),
        ("Use of colour", "Harmony, contrast and mood created by the palette."),
        ("Light and shadow", "Believable light, values and volume."),
        ("Perspective and proportion", "Space, depth and the proportions of figures and objects."),
        ("Originality", "The work is the student's own and avoids clichés."),
        ("Emotional impact", "The feeling or story the painting conveys."),
        ("Accompanying text", "Quality of the poem, quotation or story and how it fits the painting."),
        ("Presentation", "Finish, cleanliness and framing of the work."),
        ("Attention to detail", "Care given to small parts of the picture."),
        ("Storytelling", "A clear narrative or message."),
        ("Texture", "Rendering of surfaces and materials."),
        ("Expressiveness", "Energy and character of the marks."),
        ("Cultural insight", "Understanding of the place, people or tradition shown."),
        ("Experimentation", "Bold use of materials, formats or techniques."),
        ("Consistency of style", "One coherent visual language across the work."),
        ("Observation", "Evidence of study from life."),
        ("Overall impression", "The work as a whole."),
    ];

    /// <summary>Criteria sets used in turn by the sample competitions: (criterion name, weight %), sum 100.</summary>
    private static readonly (string Name, decimal Weight)[][] CriteriaSets =
    [
        [("Creativity", 30m), ("Technique", 25m), ("Composition", 25m), ("Theme relevance", 20m)],
        [("Originality", 30m), ("Use of colour", 25m), ("Theme relevance", 25m), ("Presentation", 20m)],
        [("Technique", 40m), ("Creativity", 35m), ("Accompanying text", 25m)],
        [("Composition", 25m), ("Light and shadow", 25m), ("Emotional impact", 30m), ("Accompanying text", 20m)],
    ];

    /// <summary>Score band of each rating used to place the sample averages (well inside the default thresholds).</summary>
    private static (decimal Low, decimal High) Band(Rating rating) => rating switch
    {
        Rating.BEST => (90m, 99m),
        Rating.BETTER => (80m, 90m),
        Rating.GOOD => (65m, 80m),
        Rating.MODERATE => (50m, 65m),
        _ => (25m, 50m),
    };

    /// <summary>Short comments of the other judges, by rating.</summary>
    private static (string Positive, string? Negative, string? Improve) JudgeComment(Rating rating, int judge) => (rating, judge % 2) switch
    {
        (Rating.BEST, 0) => ("A memorable, mature piece.", "Hardly anything to fault.", "Try a larger format next time."),
        (Rating.BEST, _) => ("Strong idea carried out with real skill.", null, "Keep developing this personal voice."),
        (Rating.BETTER, 0) => ("Confident handling of colour.", "Some areas feel unfinished.", "Give the edges the same care as the centre."),
        (Rating.BETTER, _) => ("Clear subject and pleasing harmony.", "The focal point could be stronger.", "Use contrast to lead the eye."),
        (Rating.GOOD, _) => ("Honest observation and a good start.", "Proportions drift in places.", "Measure before painting."),
        (Rating.MODERATE, _) => ("The theme is understood.", "Values are flat.", "Practise light and shadow studies."),
        _ => ("The work is complete.", "Little attention to detail.", "Plan the composition with thumbnails first."),
    };

    /// <summary>
    /// Runs after the sample data and the archive. Criteria and thresholds are added once; juries, scores and awards
    /// only for competitions that have no jury yet and whose entries were graded the old way (one evaluation, no judges)
    /// or not at all – so competitions created later in the app are never touched. Pictures are added for every entry
    /// that has none.
    /// </summary>
    /// <param name="db">DbContext</param>
    /// <param name="services">Scoped services (file storage, to copy extra demo pictures)</param>
    /// <param name="newCompetitionIds">Competitions seeded in this run (null = every competition without a jury)</param>
    internal static async Task SeedGradingAsync(AppDbContext db, IServiceProvider services, IReadOnlyCollection<long>? newCompetitionIds)
    {
        // ---- criteria library (once)
        if (!await db.GradingCriteria.AnyAsync())
        {
            db.GradingCriteria.AddRange(CriteriaLibrary.Select(c => new GradingCriterion { Name = c.Name, Description = c.Description }));
            await db.SaveChangesAsync();
        }
        if (!await db.RatingThresholds.AnyAsync())
        {
            db.RatingThresholds.AddRange(GradingMath.DefaultThresholds.Select(t => new RatingThreshold { Rating = t.Key, MinScore = t.Value }));
            await db.SaveChangesAsync();
        }

        // ---- pictures of entries saved before submission_image existed: their image becomes the cover
        await AddMissingPicturesAsync(db);

        var criteria = await db.GradingCriteria.ToDictionaryAsync(c => c.Name);
        var teachers = await db.StaffProfiles.AsNoTracking()
            .Where(s => s.Account.Role == Role.TEACHER && s.Account.Active)
            .OrderBy(s => s.Account.Username).Select(s => s.Id).ToListAsync();
        if (teachers.Count == 0)
        {
            return;
        }

        var query = db.Competitions.Where(c => !c.Judges.Any());
        if (newCompetitionIds != null)
        {
            query = query.Where(c => newCompetitionIds.Contains(c.Id));
        }
        var competitions = await query.OrderBy(c => c.StartDate).ThenBy(c => c.Id).ToListAsync();
        var today = DateTimeHelper.Today;

        for (var k = 0; k < competitions.Count; k++)
        {
            var competition = competitions[k];

            // ---- jury: 3 judges, or 2 for every third competition; the first is the chair
            var size = Math.Min(teachers.Count, k % 3 == 2 ? 2 : 3);
            var jury = Enumerable.Range(0, size).Select(i => teachers[(k + i) % teachers.Count]).ToList();
            for (var i = 0; i < jury.Count; i++)
            {
                db.CompetitionJudges.Add(new CompetitionJudge { CompetitionId = competition.Id, JudgeId = jury[i], IsChair = i == 0 });
            }

            // ---- weighted criteria (sum 100)
            var set = CriteriaSets[k % CriteriaSets.Length];
            var rows = set.Select((c, i) => new CompetitionCriterion
            {
                CompetitionId = competition.Id, CriterionId = criteria[c.Name].Id, WeightPercent = c.Weight, SortOrder = i,
            }).ToList();
            db.CompetitionCriteria.AddRange(rows);
            await db.SaveChangesAsync();

            // ---- entries and the rating they already had (old single evaluation), best first
            var entries = await db.Submissions
                .Where(s => s.CompetitionId == competition.Id)
                .Include(s => s.Evaluation)
                .Include(s => s.AwardIssued).ThenInclude(a => a!.CompetitionAward)
                .Where(s => !s.JudgeEvaluations.Any())
                .ToListAsync();
            var graded = entries.Where(s => s.Evaluation != null)
                .OrderBy(s => (int)s.Evaluation!.Rating)
                .ThenBy(s => s.AwardIssued == null ? int.MaxValue : s.AwardIssued.CompetitionAward.Rank)
                .ThenBy(s => s.Id)
                .ToList();

            // Averages spread evenly inside the band of the rating, awarded entries on top
            foreach (var band in graded.GroupBy(s => s.Evaluation!.Rating))
            {
                var list = band.ToList();
                var (low, high) = Band(band.Key);
                for (var i = 0; i < list.Count; i++)
                {
                    var target = band.Key == Rating.DISQUALIFIED
                        ? 40m
                        : Math.Round(high - 2.5m - i * (high - low - 5m) / Math.Max(1, list.Count - 1), 2);
                    AddJudgeEvaluations(db, list[i], list[i].Evaluation!, jury, rows, target, i);
                }
            }

            // Closed and not yet published: half of the ungraded entries already have the chair's scores (grading in progress)
            var closed = competition.PublishedAt == null && today > competition.EndDate;
            if (closed)
            {
                var ungraded = entries.Where(s => s.Evaluation == null).OrderBy(s => s.Id).ToList();
                for (var i = 0; i < ungraded.Count; i += 2)
                {
                    var rating = (Rating)(i % 3 + 1); // BETTER, GOOD, MODERATE – one judge only, so no result yet
                    var (low, high) = Band(rating);
                    db.JudgeEvaluations.Add(NewJudgeEvaluation(ungraded[i], jury[0], rows, (low + high) / 2m, i, rating,
                        competition.EndDate.AddDays(1)));
                }
            }
            await db.SaveChangesAsync();

            // ---- final results from the judges' totals
            var totals = await db.JudgeEvaluations.AsNoTracking()
                .Where(j => j.Submission.CompetitionId == competition.Id && !j.Disqualified && j.TotalScore != null)
                .GroupBy(j => j.SubmissionId)
                .Select(g => new { SubmissionId = g.Key, Totals = g.Select(j => j.TotalScore!.Value).ToList() })
                .ToDictionaryAsync(x => x.SubmissionId, x => x.Totals);
            foreach (var s in graded)
            {
                var evaluation = s.Evaluation!;
                evaluation.EvaluatedBy = jury[0];
                if (evaluation.Rating == Rating.DISQUALIFIED)
                {
                    evaluation.TotalScore = null;
                    continue;
                }
                var average = GradingMath.Average(totals[s.Id])!.Value;
                evaluation.TotalScore = Math.Round(average, 4);
                evaluation.Rating = GradingMath.RatingOf(average, GradingMath.DefaultThresholds);
            }
            await db.SaveChangesAsync();

            // ---- published competitions: the awards come from the ranking (BR-30, BR-31), replacing the hand-picked ones
            if (competition.PublishedAt != null)
            {
                var old = await db.AwardsIssued.Where(a => a.Submission.CompetitionId == competition.Id).ToListAsync();
                var issuedBy = old.FirstOrDefault()?.IssuedBy ?? competition.PublishedBy ?? competition.CreatedBy;
                var issuedOn = DateOnly.FromDateTime(competition.PublishedAt.Value);
                db.AwardsIssued.RemoveRange(old);
                await db.SaveChangesAsync();

                var ranked = graded.Where(s => s.Evaluation!.Rating != Rating.DISQUALIFIED && s.Evaluation.TotalScore != null)
                    .Select(s => new GradingMath.Ranked(s.Id, s.Evaluation!.TotalScore!.Value));
                var slots = await db.CompetitionAwards.AsNoTracking().Where(a => a.CompetitionId == competition.Id)
                    .Select(a => new GradingMath.AwardSlot(a.Id, a.Rank, a.Quantity)).ToListAsync();
                foreach (var w in GradingMath.Awards(ranked, slots))
                {
                    db.AwardsIssued.Add(new AwardIssued
                    {
                        CompetitionAwardId = w.AwardId, SubmissionId = w.SubmissionId, IssuedBy = issuedBy, IssuedDate = issuedOn,
                        Remarks = $"Place {w.Place} in the jury ranking.",
                    });
                }
                await db.SaveChangesAsync();
            }
        }

        // ---- a few sample entries with several pictures (BR-18), so the gallery and the grading page show them
        if (newCompetitionIds == null || newCompetitionIds.Count > 0)
        {
            await AddDemoPicturesAsync(db, services);
        }
    }

    /// <summary>Every judge of the jury grades the entry so that the average lands on <paramref name="target"/>.</summary>
    private static void AddJudgeEvaluations(AppDbContext db, Submission s, Evaluation evaluation, List<long> jury,
        List<CompetitionCriterion> rows, decimal target, int seed)
    {
        // Judges disagree a little: offsets that add up to zero
        decimal[] offsets = jury.Count switch
        {
            1 => [0m],
            2 => [-1.2m, 1.2m],
            _ => [-1.5m, 0.5m, 1m],
        };
        for (var j = 0; j < jury.Count; j++)
        {
            var day = DateOnly.FromDateTime(evaluation.EvaluatedAt).AddDays(j == 0 ? 0 : -j);
            if (evaluation.Rating == Rating.DISQUALIFIED && j == 0)
            {
                // The chair disqualifies the entry (BR-23): the reason is the one the sample evaluation had
                db.JudgeEvaluations.Add(new JudgeEvaluation
                {
                    SubmissionId = s.Id, JudgeId = jury[j], Disqualified = true,
                    NegativePoints = evaluation.NegativePoints ?? "The entry breaks the rules of the competition.",
                    EvaluatedAt = day.ToDateTime(new TimeOnly(10, 0)),
                });
                continue;
            }
            var rating = evaluation.Rating == Rating.DISQUALIFIED ? Rating.NORMAL : evaluation.Rating;
            var je = NewJudgeEvaluation(s, jury[j], rows, target + offsets[j % offsets.Length], seed + j, rating, day);
            if (j == 0 && evaluation.Rating != Rating.DISQUALIFIED)
            {
                // The chair keeps the comments of the sample evaluation
                je.PositivePoints = evaluation.PositivePoints ?? je.PositivePoints;
                je.NegativePoints = evaluation.NegativePoints;
                je.ImprovementAreas = evaluation.ImprovementAreas;
            }
            db.JudgeEvaluations.Add(je);
        }
    }

    /// <summary>One judge's scores around <paramref name="total"/>: criteria differ a little but the weighted total stays close.</summary>
    private static JudgeEvaluation NewJudgeEvaluation(Submission s, long judgeId, List<CompetitionCriterion> rows, decimal total,
        int seed, Rating rating, DateOnly day)
    {
        // Deviations per criterion; the last one balances the weighted sum to zero
        var deltas = new decimal[rows.Count];
        decimal[] pattern = [2.5m, -2m, 1.5m, -1m, 0.5m];
        for (var i = 0; i < rows.Count - 1; i++)
        {
            deltas[i] = pattern[(i + seed) % pattern.Length];
        }
        var last = rows[^1].WeightPercent;
        deltas[^1] = -rows.Take(rows.Count - 1).Select((r, i) => deltas[i] * r.WeightPercent).Sum() / last;

        var (positive, negative, improve) = JudgeComment(rating, (int)(judgeId % 2));
        var je = new JudgeEvaluation
        {
            SubmissionId = s.Id, JudgeId = judgeId, Disqualified = false,
            PositivePoints = positive, NegativePoints = negative, ImprovementAreas = improve,
            EvaluatedAt = day.ToDateTime(new TimeOnly(9 + (int)(judgeId % 6), 30)),
        };
        for (var i = 0; i < rows.Count; i++)
        {
            je.Scores.Add(new JudgeScore
            {
                CompetitionCriterionId = rows[i].Id,
                Score = Math.Clamp(Math.Round(total + deltas[i], 2), 0m, 100m),
            });
        }
        je.TotalScore = GradingMath.JudgeTotal(je.Scores.Select((sc, i) => (sc.Score, rows[i].WeightPercent)));
        return je;
    }

    /// <summary>Entries without pictures get their own image as the only (cover) picture.</summary>
    private static async Task AddMissingPicturesAsync(AppDbContext db)
    {
        var missing = await db.Submissions.Where(s => !s.Images.Any())
            .Select(s => new { s.Id, s.ImagePath, s.ThumbnailPath, s.OriginalFileName, s.FileSize }).ToListAsync();
        foreach (var s in missing)
        {
            db.SubmissionImages.Add(new SubmissionImage
            {
                SubmissionId = s.Id, ImagePath = s.ImagePath, ThumbnailPath = s.ThumbnailPath, OriginalFileName = s.OriginalFileName,
                FileSize = s.FileSize, ContentType = ImageValidator.ContentTypeOf(s.ImagePath), SortOrder = 0, IsCover = true,
            });
        }
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Two sample entries (the BEST one of Hometown Landscapes and the first entry of City at Night) get two more pictures:
    /// copies of other sample paintings, so each entry still owns its files.
    /// </summary>
    private static async Task AddDemoPicturesAsync(AppDbContext db, IServiceProvider services)
    {
        var files = services.GetRequiredService<IFileStorageService>();
        var targets = await db.Submissions
            .Where(s => (s.Competition.Title == "Hometown Landscapes" || s.Competition.Title == "City at Night") && s.Images.Count() == 1)
            .OrderBy(s => s.Competition.Title).ThenBy(s => s.Id)
            .ToListAsync();
        var picked = targets.GroupBy(s => s.CompetitionId).Select(g => g.First()).ToList();
        if (picked.Count == 0)
        {
            return;
        }
        var donors = await db.Submissions.AsNoTracking()
            .Where(s => s.Competition.Title == "Wildlife" || s.Competition.Title == "Hometown Landscapes")
            .OrderBy(s => s.Id).Select(s => new { s.Id, s.ImagePath, s.ThumbnailPath, s.OriginalFileName, s.FileSize })
            .ToListAsync();

        var n = 0;
        foreach (var s in picked)
        {
            var extra = donors.Where(d => d.Id != s.Id).Skip(n).Take(2).ToList();
            n += 2;
            for (var i = 0; i < extra.Count; i++)
            {
                var d = extra[i];
                var image = CopyStored(files, d.ImagePath);
                var thumb = d.ThumbnailPath == null ? null : CopyStored(files, d.ThumbnailPath);
                if (image == null)
                {
                    continue;
                }
                db.SubmissionImages.Add(new SubmissionImage
                {
                    SubmissionId = s.Id, ImagePath = image, ThumbnailPath = thumb,
                    OriginalFileName = i == 0 ? "detail-closeup.jpg" : "side-view.jpg", FileSize = d.FileSize,
                    ContentType = ImageValidator.ContentTypeOf(image), SortOrder = i + 1, IsCover = false,
                });
            }
        }
        await db.SaveChangesAsync();
    }

    /// <summary>Copies a stored file next to itself under a new random name; null when the source is missing.</summary>
    private static string? CopyStored(IFileStorageService files, string relativePath)
    {
        var source = files.ResolvePath(relativePath);
        if (!File.Exists(source))
        {
            return null;
        }
        var folder = Path.GetDirectoryName(relativePath.Replace('\\', '/'))?.Replace('\\', '/') ?? string.Empty;
        var copy = $"{folder}/{Guid.NewGuid():N}{Path.GetExtension(relativePath)}".TrimStart('/');
        File.Copy(source, files.ResolvePath(copy));
        return copy;
    }
}
