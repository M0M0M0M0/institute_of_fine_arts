namespace InstituteOfFineArts.Web.Models.Entities;

/// <summary>
/// A criterion chosen for one competition with its weight (table competition_criterion). The weights of a competition
/// add up to exactly 100 %; they cannot change once an entry has been graded.
/// </summary>
public class CompetitionCriterion : BaseEntity
{
    /// <summary>Competition.</summary>
    public long CompetitionId { get; set; }

    /// <summary>Criterion of the library.</summary>
    public long CriterionId { get; set; }

    /// <summary>Weight in percent (&gt; 0, the competition's weights add up to 100).</summary>
    public decimal WeightPercent { get; set; }

    /// <summary>Display order on the form and the score sheet.</summary>
    public int SortOrder { get; set; }

    /// <summary>Competition.</summary>
    public Competition Competition { get; set; } = null!;

    /// <summary>Criterion of the library.</summary>
    public GradingCriterion Criterion { get; set; } = null!;
}
