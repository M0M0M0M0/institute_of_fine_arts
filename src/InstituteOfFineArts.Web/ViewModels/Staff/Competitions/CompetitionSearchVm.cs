using System.ComponentModel.DataAnnotations;
using InstituteOfFineArts.Web.Models.Enums;

namespace InstituteOfFineArts.Web.ViewModels.Staff.Competitions;

/// <summary>
/// Filters of the competition list (F-M05-04): keyword, status, date range, page.
/// </summary>
public class CompetitionSearchVm
{
    /// <summary>Part of the title or code.</summary>
    [StringLength(100)]
    [Display(Name = "Title or code")]
    public string? Keyword { get; set; }

    /// <summary>Status as shown to users (calculated from the dates, BR-07).</summary>
    [Display(Name = "Status")]
    public CompetitionStatus? Status { get; set; }

    /// <summary>Show competitions running on or after this day.</summary>
    [DataType(DataType.Date)]
    [Display(Name = "From")]
    public DateOnly? From { get; set; }

    /// <summary>Show competitions running on or before this day.</summary>
    [DataType(DataType.Date)]
    [Display(Name = "To")]
    public DateOnly? To { get; set; }

    /// <summary>Page number (1-based).</summary>
    public int Page { get; set; } = 1;
}
