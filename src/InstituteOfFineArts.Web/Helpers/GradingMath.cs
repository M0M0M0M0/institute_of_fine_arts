using InstituteOfFineArts.Web.Models.Enums;

namespace InstituteOfFineArts.Web.Helpers;

/// <summary>
/// The arithmetic of criteria grading and automatic awards, kept in one place and free of the database so it can be
/// tested on its own.
/// <list type="bullet">
/// <item>A judge's total = Σ score × weight / 100 (weights add up to 100, so the total is 0–100).</item>
/// <item>An entry's result = the average of its judges' totals; DISQUALIFIED when a judge disqualified it.</item>
/// <item>The rating comes from the average through the institute's thresholds (BEST ≥ …, else NORMAL).</item>
/// <item>Comparisons use the full decimal value; rounding is for display only (decided 18/11/2026).</item>
/// <item>Awards: entries ranked by average, equal averages share a place ("1, 1, 2"); each award, by rank, takes the
/// next places until its quantity is reached – a tie group is never split, so a shared place can exceed the quantity,
/// and every winner receives the full prize.</item>
/// </list>
/// </summary>
public static class GradingMath
{
    /// <summary>Default thresholds (lowest average for each rating) used when the table is empty.</summary>
    public static readonly IReadOnlyDictionary<Rating, decimal> DefaultThresholds = new Dictionary<Rating, decimal>
    {
        [Rating.BEST] = 90m,
        [Rating.BETTER] = 80m,
        [Rating.GOOD] = 65m,
        [Rating.MODERATE] = 50m,
    };

    /// <summary>Ratings that have a threshold, from the highest.</summary>
    public static readonly Rating[] ThresholdRatings = [Rating.BEST, Rating.BETTER, Rating.GOOD, Rating.MODERATE];

    /// <summary>Weighted total of one judge: Σ score × weight / 100.</summary>
    /// <param name="scores">(score 0–100, weight in percent) per criterion</param>
    public static decimal JudgeTotal(IEnumerable<(decimal Score, decimal Weight)> scores)
        => scores.Sum(s => s.Score * s.Weight) / 100m;

    /// <summary>Average of the judges' totals (full precision); null when there is none.</summary>
    public static decimal? Average(IReadOnlyCollection<decimal> totals)
        => totals.Count == 0 ? null : totals.Sum() / totals.Count;

    /// <summary>Rating of an average score with the given thresholds (missing ones fall back to the defaults).</summary>
    public static Rating RatingOf(decimal average, IReadOnlyDictionary<Rating, decimal> thresholds)
    {
        foreach (var rating in ThresholdRatings)
        {
            var min = thresholds.TryGetValue(rating, out var t) ? t : DefaultThresholds[rating];
            if (average >= min)
            {
                return rating;
            }
        }
        return Rating.NORMAL;
    }

    /// <summary>Null when the thresholds are valid (each 0–100, strictly going down from BEST), else the problem.</summary>
    public static string? CheckThresholds(IReadOnlyDictionary<Rating, decimal> thresholds)
    {
        decimal? previous = null;
        foreach (var rating in ThresholdRatings)
        {
            if (!thresholds.TryGetValue(rating, out var value))
            {
                return $"Please enter the lowest score for {rating}.";
            }
            if (value is < 0 or > 100)
            {
                return "Each threshold must be between 0 and 100.";
            }
            if (previous is { } p && value >= p)
            {
                return "Thresholds must go down from BEST to MODERATE (each lower than the one above).";
            }
            previous = value;
        }
        return null;
    }

    /// <summary>One entry in the award ranking.</summary>
    /// <param name="SubmissionId">Entry</param>
    /// <param name="Average">Average of the jury, full precision</param>
    public sealed record Ranked(long SubmissionId, decimal Average);

    /// <summary>One award to hand out, in rank order.</summary>
    /// <param name="AwardId">competition_award.id</param>
    /// <param name="Rank">1 = highest award</param>
    /// <param name="Quantity">Number of winners planned</param>
    public sealed record AwardSlot(long AwardId, int Rank, int Quantity);

    /// <summary>One winner.</summary>
    /// <param name="AwardId">competition_award.id</param>
    /// <param name="SubmissionId">Winning entry</param>
    /// <param name="Place">Place in the ranking (dense: 1, 1, 2…)</param>
    public sealed record Winner(long AwardId, long SubmissionId, int Place);

    /// <summary>Dense places of the entries: equal averages share a place; highest average first.</summary>
    public static IReadOnlyList<(int Place, IReadOnlyList<Ranked> Entries)> Places(IEnumerable<Ranked> entries)
        => entries
            .GroupBy(e => e.Average)
            .OrderByDescending(g => g.Key)
            .Select((g, i) => (i + 1, (IReadOnlyList<Ranked>)g.OrderBy(e => e.SubmissionId).ToList()))
            .ToList();

    /// <summary>
    /// Hands out the awards: awards by rank (then id); each takes whole places from the top until it has at least its
    /// quantity of winners or the places run out. Entries left after the last award win nothing.
    /// </summary>
    public static IReadOnlyList<Winner> Awards(IEnumerable<Ranked> entries, IEnumerable<AwardSlot> awards)
    {
        var places = Places(entries);
        var winners = new List<Winner>();
        var next = 0;
        foreach (var award in awards.OrderBy(a => a.Rank).ThenBy(a => a.AwardId))
        {
            var given = 0;
            while (given < award.Quantity && next < places.Count)
            {
                var (place, group) = places[next++];
                winners.AddRange(group.Select(e => new Winner(award.AwardId, e.SubmissionId, place)));
                given += group.Count;
            }
        }
        return winners;
    }
}
