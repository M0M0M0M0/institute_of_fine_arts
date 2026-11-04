using System.ComponentModel.DataAnnotations;

namespace InstituteOfFineArts.Web.ViewModels.Staff.Competitions;

/// <summary>
/// Add / edit form of a defined award of a competition (F-M05-06, task BE1-06).
/// The rules (BR-09) are checked here for the form and again in CompetitionAwardService.
/// </summary>
public class CompetitionAwardFormVm
{
    /// <summary>Message when the rank is below 1.</summary>
    public const string RankMessage = "Rank must be 1 or higher (1 = highest)";

    /// <summary>Message when the prize value is negative.</summary>
    public const string PrizeMessage = "Prize value must be 0 or more (USD)";

    /// <summary>Message when the quantity is below 1.</summary>
    public const string QuantityMessage = "Quantity must be at least 1";

    /// <summary>Award id (0 when adding).</summary>
    public long Id { get; set; }

    /// <summary>Competition the award belongs to (taken from the route, never from the form).</summary>
    public long CompetitionId { get; set; }

    /// <summary>Competition title (display only).</summary>
    public string? CompetitionTitle { get; set; }

    /// <summary>Competition code (display only).</summary>
    public string? CompetitionCode { get; set; }

    /// <summary>Award name, e.g. First Prize (unique within the competition).</summary>
    [Required(ErrorMessage = "Please enter the award name")]
    [StringLength(100, ErrorMessage = "Award name must be at most 100 characters")]
    [Display(Name = "Award name")]
    public string AwardName { get; set; } = string.Empty;

    /// <summary>Rank, 1 = highest.</summary>
    [Required(ErrorMessage = "Please enter the rank")]
    [Range(1, 1000, ErrorMessage = RankMessage)]
    [Display(Name = "Rank")]
    public int? Rank { get; set; }

    /// <summary>Prize value in USD.</summary>
    [Required(ErrorMessage = "Please enter the prize value")]
    [Range(0d, 9999999999999999d, ErrorMessage = PrizeMessage)]
    [Display(Name = "Prize value (USD)")]
    public decimal? PrizeValue { get; set; }

    /// <summary>How many times the award can be given.</summary>
    [Required(ErrorMessage = "Please enter the quantity")]
    [Range(1, 1000, ErrorMessage = QuantityMessage)]
    [Display(Name = "Quantity")]
    public int? Quantity { get; set; }

    /// <summary>What the winner receives: certificate, voucher…</summary>
    [StringLength(500, ErrorMessage = "Prize description must be at most 500 characters")]
    [Display(Name = "Prize description")]
    public string? PrizeDescription { get; set; }

    /// <summary>Edit only: how many times the award has already been given (display only).</summary>
    public int IssuedCount { get; set; }
}
