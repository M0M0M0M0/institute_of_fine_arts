using System.ComponentModel.DataAnnotations;
using InstituteOfFineArts.Web.Models.Enums;

namespace InstituteOfFineArts.Web.ViewModels.Staff.Evaluations;

/// <summary>
/// Evaluation form of one submission (F-M07-03/04, task BE1-10): rating and the three comments.
/// The rules (BR-23) are checked here for the form and again in EvaluationService.
/// </summary>
public class EvaluationFormVm : IValidatableObject
{
    /// <summary>Longest comment accepted (the columns are nvarchar(max); 2000 keeps the comments readable).</summary>
    public const int MaxCommentLength = 2000;

    /// <summary>Message when no rating is selected.</summary>
    public const string RatingRequiredMessage = "Please select a rating";

    /// <summary>Message when a DISQUALIFIED rating has no reason (BR-23).</summary>
    public const string DisqualifiedReasonMessage = "Please enter the reason for disqualification in Negative points";

    /// <summary>Message when a rating other than DISQUALIFIED has no positive points (BR-23).</summary>
    public const string PositivePointsMessage = "Please enter the positive points of the entry";

    /// <summary>Rating: BEST, BETTER, GOOD, MODERATE, NORMAL or DISQUALIFIED.</summary>
    [Required(ErrorMessage = RatingRequiredMessage)]
    [EnumDataType(typeof(Rating), ErrorMessage = RatingRequiredMessage)]
    [Display(Name = "Rating")]
    public Rating? Rating { get; set; }

    /// <summary>Strengths of the entry – required unless disqualified.</summary>
    [StringLength(MaxCommentLength, ErrorMessage = "Positive points must be at most 2000 characters")]
    [Display(Name = "Positive points")]
    public string? PositivePoints { get; set; }

    /// <summary>Weaknesses of the entry – for DISQUALIFIED this is the reason and is required.</summary>
    [StringLength(MaxCommentLength, ErrorMessage = "Negative points must be at most 2000 characters")]
    [Display(Name = "Negative points")]
    public string? NegativePoints { get; set; }

    /// <summary>What the student should improve (optional).</summary>
    [StringLength(MaxCommentLength, ErrorMessage = "Improvement areas must be at most 2000 characters")]
    [Display(Name = "Improvement areas")]
    public string? ImprovementAreas { get; set; }

    /// <summary>
    /// Version of the evaluation when the page was opened (BA-07): its last change time, or empty when there was no
    /// evaluation yet. Sent back in a hidden field, so a save is refused when another staff member changed the evaluation
    /// in the meantime instead of silently overwriting it. Null = not checked (callers that check it themselves).
    /// </summary>
    public string? Version { get; set; }

    /// <summary>BR-23: DISQUALIFIED needs a reason, every other rating needs positive points.</summary>
    /// <param name="validationContext">Validation context (not used)</param>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // No rating → the Required attribute already reports it
        if (Rating is null)
        {
            yield break;
        }

        // The reason of a disqualification is written in Negative points
        if (Rating == Models.Enums.Rating.DISQUALIFIED)
        {
            if (string.IsNullOrWhiteSpace(NegativePoints))
            {
                yield return new ValidationResult(DisqualifiedReasonMessage, [nameof(NegativePoints)]);
            }
        }
        else if (string.IsNullOrWhiteSpace(PositivePoints))
        {
            yield return new ValidationResult(PositivePointsMessage, [nameof(PositivePoints)]);
        }
    }
}
