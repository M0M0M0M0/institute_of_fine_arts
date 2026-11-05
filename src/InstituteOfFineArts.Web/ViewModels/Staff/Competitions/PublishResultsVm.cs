using System.ComponentModel.DataAnnotations;

namespace InstituteOfFineArts.Web.ViewModels.Staff.Competitions;

/// <summary>
/// Publish results form on the competition details page (F-M05-08, task BE1-11): optional general remarks.
/// </summary>
public class PublishResultsVm
{
    /// <summary>Longest remarks accepted (the column is nvarchar(max)).</summary>
    public const int MaxRemarksLength = 2000;

    /// <summary>General remarks about the results, stored in competition.result_remarks.</summary>
    [StringLength(MaxRemarksLength, ErrorMessage = "Result remarks must be at most 2000 characters.")]
    [Display(Name = "Result remarks")]
    public string? ResultRemarks { get; set; }
}
