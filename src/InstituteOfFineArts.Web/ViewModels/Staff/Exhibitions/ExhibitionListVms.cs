using System.ComponentModel.DataAnnotations;
using InstituteOfFineArts.Web.Models.Enums;

namespace InstituteOfFineArts.Web.ViewModels.Staff.Exhibitions;

/// <summary>
/// Filters of the exhibition list (F-M09-02): name, city, date range, status, page.
/// Used by the Staff, Manager and Student exhibition lists.
/// </summary>
public class ExhibitionSearchVm
{
    /// <summary>Part of the exhibition name.</summary>
    [StringLength(100)]
    [Display(Name = "Name")]
    public string? Keyword { get; set; }

    /// <summary>Part of the city / province.</summary>
    [StringLength(100)]
    [Display(Name = "City")]
    public string? City { get; set; }

    /// <summary>Status as shown to users (calculated from the dates, BR-42).</summary>
    [Display(Name = "Status")]
    public ExhibitionStatus? Status { get; set; }

    /// <summary>Show exhibitions running on or after this day.</summary>
    [DataType(DataType.Date)]
    [Display(Name = "From")]
    public DateOnly? From { get; set; }

    /// <summary>Show exhibitions running on or before this day.</summary>
    [DataType(DataType.Date)]
    [Display(Name = "To")]
    public DateOnly? To { get; set; }

    /// <summary>Page number (1-based).</summary>
    public int Page { get; set; } = 1;
}

/// <summary>
/// One row of the exhibition list.
/// </summary>
public class ExhibitionListItemVm
{
    /// <summary>Exhibition id.</summary>
    public long Id { get; set; }

    /// <summary>Exhibition name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Venue.</summary>
    public string Venue { get; set; } = string.Empty;

    /// <summary>City / province.</summary>
    public string City { get; set; } = string.Empty;

    /// <summary>Start date.</summary>
    public DateOnly StartDate { get; set; }

    /// <summary>End date.</summary>
    public DateOnly EndDate { get; set; }

    /// <summary>Status calculated for today (BR-42).</summary>
    public ExhibitionStatus Status { get; set; }

    /// <summary>Number of artworks sent to the exhibition (all statuses).</summary>
    public int EntryCount { get; set; }
}
