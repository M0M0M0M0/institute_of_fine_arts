using InstituteOfFineArts.Web.Models.Enums;

namespace InstituteOfFineArts.Web.Models.Entities;

/// <summary>
/// Lowest average score that gives a rating (table rating_threshold), one row per rating BEST, BETTER, GOOD and
/// MODERATE; below the MODERATE threshold the rating is NORMAL. One set for the whole institute, set by the
/// administrator; the thresholds go down from BEST to MODERATE. Changing them never changes a published result.
/// </summary>
public class RatingThreshold : BaseEntity
{
    /// <summary>BEST, BETTER, GOOD or MODERATE (unique).</summary>
    public Rating Rating { get; set; }

    /// <summary>Lowest average score (0–100) for this rating.</summary>
    public decimal MinScore { get; set; }
}
