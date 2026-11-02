namespace InstituteOfFineArts.Web.Models.Entities;

/// <summary>
/// Planned award of a competition (table competition_award).
/// </summary>
public class CompetitionAward : BaseEntity
{
    /// <summary>Foreign key to the competition.</summary>
    public long CompetitionId { get; set; }

    /// <summary>Award name: First Prize, Second Prize, Honorable Mention…</summary>
    public string AwardName { get; set; } = string.Empty;

    /// <summary>Rank, 1 = highest (stored in column rank_no to avoid clashing with the SQL RANK function).</summary>
    public int Rank { get; set; }

    /// <summary>Prize value (USD) – financial requirement.</summary>
    public decimal PrizeValue { get; set; }

    /// <summary>Prize description: gift, certificate…</summary>
    public string? PrizeDescription { get; set; }

    /// <summary>Number of slots for this award.</summary>
    public int Quantity { get; set; } = 1;

    /// <summary>Competition.</summary>
    public Competition Competition { get; set; } = null!;

    /// <summary>Times this award has been issued.</summary>
    public ICollection<AwardIssued> IssuedAwards { get; set; } = new List<AwardIssued>();
}
