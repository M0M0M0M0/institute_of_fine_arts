using System.ComponentModel.DataAnnotations;

namespace InstituteOfFineArts.Web.ViewModels.Staff.Exhibitions;

/// <summary>
/// Create / edit form of an exhibition (F-M09-01), used by both Staff and Manager.
/// The status is never entered: it follows the dates (BR-42).
/// </summary>
public class ExhibitionFormVm : IValidatableObject
{
    /// <summary>Message when the end date is before the start date (BR-42).</summary>
    public const string EndBeforeStartMessage = "End date must be on or after the start date";

    /// <summary>Exhibition id (0 when creating).</summary>
    public long Id { get; set; }

    /// <summary>Exhibition name.</summary>
    [Required(ErrorMessage = "Please enter the exhibition name")]
    [StringLength(200, ErrorMessage = "Name must be at most 200 characters")]
    [Display(Name = "Name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Description.</summary>
    [StringLength(4000, ErrorMessage = "Description must be at most 4000 characters")]
    [Display(Name = "Description")]
    public string? Description { get; set; }

    /// <summary>Venue (address of the gallery / museum).</summary>
    [Required(ErrorMessage = "Please enter the venue")]
    [StringLength(255, ErrorMessage = "Venue must be at most 255 characters")]
    [Display(Name = "Venue")]
    public string Venue { get; set; } = string.Empty;

    /// <summary>City / province (domestic exhibitions only).</summary>
    [Required(ErrorMessage = "Please enter the city")]
    [StringLength(100, ErrorMessage = "City must be at most 100 characters")]
    [Display(Name = "City / province")]
    public string City { get; set; } = string.Empty;

    /// <summary>Organizer.</summary>
    [StringLength(200, ErrorMessage = "Organizer must be at most 200 characters")]
    [Display(Name = "Organizer")]
    public string? Organizer { get; set; }

    /// <summary>First day of the exhibition.</summary>
    [Required(ErrorMessage = "Please choose the start date")]
    [DataType(DataType.Date)]
    [Display(Name = "Start date")]
    public DateOnly? StartDate { get; set; }

    /// <summary>Last day of the exhibition.</summary>
    [Required(ErrorMessage = "Please choose the end date")]
    [DataType(DataType.Date)]
    [Display(Name = "End date")]
    public DateOnly? EndDate { get; set; }

    /// <summary>Edit only: true when artworks have been sent to the exhibition (shown as a notice on the form).</summary>
    public bool HasEntries { get; set; }

    /// <summary>Edit only: true when the exhibition is finished and has entries, so its dates are read-only.</summary>
    public bool DatesLocked { get; set; }

    /// <summary>Checks rules that involve two fields.</summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (StartDate is { } start && EndDate is { } end && end < start)
        {
            yield return new ValidationResult(EndBeforeStartMessage, new[] { nameof(EndDate) });
        }
    }
}
