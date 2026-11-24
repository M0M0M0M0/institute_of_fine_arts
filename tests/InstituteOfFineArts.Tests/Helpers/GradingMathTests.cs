using InstituteOfFineArts.Web.Helpers;
using InstituteOfFineArts.Web.Models.Enums;

namespace InstituteOfFineArts.Tests.Helpers;

/// <summary>
/// GradingMath (11/2026): weighted total (BR-23), average without rounding (BR-28), rating from the thresholds (BR-27),
/// dense places and awards from the ranking (BR-30, BR-31).
/// </summary>
public class GradingMathTests
{
    [Fact]
    public void JudgeTotal_IsWeightedByPercent()
        => Assert.Equal(89m, GradingMath.JudgeTotal([(95m, 60m), (80m, 40m)]));

    [Fact]
    public void Average_KeepsFullPrecision_NullWhenEmpty()
    {
        Assert.Equal(89.5m, GradingMath.Average([95m, 84m]));
        Assert.Equal(200m / 3m, GradingMath.Average([60m, 70m, 70m]));
        Assert.Null(GradingMath.Average([]));
    }

    /// <summary>Default thresholds 90 / 80 / 65 / 50; the boundary belongs to the higher rating.</summary>
    [Theory]
    [InlineData(100, Rating.BEST)]
    [InlineData(90, Rating.BEST)]
    [InlineData(89.999, Rating.BETTER)]
    [InlineData(80, Rating.BETTER)]
    [InlineData(65, Rating.GOOD)]
    [InlineData(50, Rating.MODERATE)]
    [InlineData(49.99, Rating.NORMAL)]
    [InlineData(0, Rating.NORMAL)]
    public void RatingOf_DefaultThresholds(double average, Rating expected)
        => Assert.Equal(expected, GradingMath.RatingOf((decimal)average, GradingMath.DefaultThresholds));

    /// <summary>BR-28: 89.995 is not rounded up to 90 before the comparison.</summary>
    [Fact]
    public void RatingOf_DoesNotRoundFirst()
        => Assert.Equal(Rating.BETTER, GradingMath.RatingOf(89.995m, GradingMath.DefaultThresholds));

    [Fact]
    public void RatingOf_CustomThresholds_MissingOnesUseDefaults()
    {
        var custom = new Dictionary<Rating, decimal> { [Rating.BEST] = 85m };
        Assert.Equal(Rating.BEST, GradingMath.RatingOf(86m, custom));
        Assert.Equal(Rating.GOOD, GradingMath.RatingOf(70m, custom));
    }

    [Fact]
    public void CheckThresholds_MustGoDownStrictly()
    {
        Assert.Null(GradingMath.CheckThresholds(GradingMath.DefaultThresholds));
        var equal = new Dictionary<Rating, decimal> { [Rating.BEST] = 80m, [Rating.BETTER] = 80m, [Rating.GOOD] = 65m, [Rating.MODERATE] = 50m };
        Assert.NotNull(GradingMath.CheckThresholds(equal));
        var outOfRange = new Dictionary<Rating, decimal> { [Rating.BEST] = 101m, [Rating.BETTER] = 80m, [Rating.GOOD] = 65m, [Rating.MODERATE] = 50m };
        Assert.NotNull(GradingMath.CheckThresholds(outOfRange));
    }

    /// <summary>Dense places: equal averages share a place, the next place follows (1, 1, 2).</summary>
    [Fact]
    public void Places_AreDense()
    {
        var places = GradingMath.Places([new(1, 90m), new(2, 95m), new(3, 90m), new(4, 70m)]);
        Assert.Equal([1, 2, 3], places.Select(p => p.Place));
        Assert.Equal([2L], places[0].Entries.Select(e => e.SubmissionId));
        Assert.Equal([1L, 3L], places[1].Entries.Select(e => e.SubmissionId));
    }

    /// <summary>BR-31: a tie is never split, so an award may go beyond its quantity; the next award starts after the tie.</summary>
    [Fact]
    public void Awards_TieGoesBeyondQuantity()
    {
        var winners = GradingMath.Awards(
            [new(1, 95m), new(2, 95m), new(3, 90m), new(4, 80m)],
            [new GradingMath.AwardSlot(10, 1, 1), new GradingMath.AwardSlot(20, 2, 1)]);

        Assert.Equal([1L, 2L], winners.Where(w => w.AwardId == 10).Select(w => w.SubmissionId));
        Assert.Equal([3L], winners.Where(w => w.AwardId == 20).Select(w => w.SubmissionId));
        Assert.DoesNotContain(winners, w => w.SubmissionId == 4);
    }

    /// <summary>Awards are handed out by rank, whatever their order; more slots than entries leaves the rest empty.</summary>
    [Fact]
    public void Awards_ByRank_SlotsCanStayEmpty()
    {
        var winners = GradingMath.Awards(
            [new(1, 70m), new(2, 88m)],
            [new GradingMath.AwardSlot(30, 3, 2), new GradingMath.AwardSlot(10, 1, 1), new GradingMath.AwardSlot(20, 2, 1)]);

        Assert.Equal((10L, 2L), (winners[0].AwardId, winners[0].SubmissionId));
        Assert.Equal((20L, 1L), (winners[1].AwardId, winners[1].SubmissionId));
        Assert.Equal(2, winners.Count);
    }
}
