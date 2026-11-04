using System.ComponentModel.DataAnnotations;

namespace InstituteOfFineArts.Web.ViewModels.Staff.Competitions;

/// <summary>
/// Create / edit form of a competition (F-M05-01, F-M05-02). The status is never entered: it follows the dates (BR-07).
/// </summary>
public class CompetitionFormVm : IValidatableObject
{
    /// <summary>Message when the end date is before the start date (TC-M05-02).</summary>
    public const string EndBeforeStartMessage = "End date must be on or after the start date";

    /// <summary>Competition id (0 when creating).</summary>
    public long Id { get; set; }

    /// <summary>Generated code, e.g. CT2026-03 (display only).</summary>
    public string? Code { get; set; }

    /// <summary>Title.</summary>
    [Required(ErrorMessage = "Please enter a title")]
    [StringLength(200, ErrorMessage = "Title must be at most 200 characters")]
    [Display(Name = "Title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>Description / theme of the competition.</summary>
    [StringLength(4000, ErrorMessage = "Description must be at most 4000 characters")]
    [Display(Name = "Description")]
    public string? Description { get; set; }

    /// <summary>Rules and conditions.</summary>
    [StringLength(4000, ErrorMessage = "Rules must be at most 4000 characters")]
    [Display(Name = "Rules & conditions")]
    public string? Rules { get; set; }

    /// <summary>First day students can submit.</summary>
    [Required(ErrorMessage = "Please choose the start date")]
    [DataType(DataType.Date)]
    [Display(Name = "Start date")]
    public DateOnly? StartDate { get; set; }

    /// <summary>Last day to submit (deadline 23:59:59 that day, BR-06).</summary>
    [Required(ErrorMessage = "Please choose the end date")]
    [DataType(DataType.Date)]
    [Display(Name = "End date (submission deadline)")]
    public DateOnly? EndDate { get; set; }

    /// <summary>Optional cover image (JPEG ≤ 5 MB).</summary>
    [Display(Name = "Cover image")]
    public IFormFile? CoverImage { get; set; }

    /// <summary>Edit only: remove the current cover image.</summary>
    [Display(Name = "Remove the current cover image")]
    public bool RemoveCover { get; set; }

    /// <summary>Edit only: true when the competition already has a cover (to show it on the form).</summary>
    public bool HasCover { get; set; }

    /// <summary>Edit only: true when entries exist, so only the end date (extend), description, rules and cover can change (BR-10).</summary>
    public bool HasSubmissions { get; set; }

    /// <summary>Checks rules that involve two fields.</summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (StartDate is { } start && EndDate is { } end && end < start)
        {
            yield return new ValidationResult(EndBeforeStartMessage, new[] { nameof(EndDate) });
        }
    }
}
