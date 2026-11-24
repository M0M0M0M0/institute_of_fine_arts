using System.ComponentModel.DataAnnotations;

namespace InstituteOfFineArts.Web.ViewModels.Admin.Grading;

/// <summary>Admin page /admin/grading: the criteria library and the rating thresholds.</summary>
public class GradingLibraryVm
{
    /// <summary>Criteria, active first.</summary>
    public IReadOnlyList<CriterionListItemVm> Criteria { get; set; } = [];

    /// <summary>Current thresholds.</summary>
    public ThresholdsFormVm Thresholds { get; set; } = new();
}

/// <summary>One criterion of the library.</summary>
/// <param name="Id">grading_criterion.id</param>
/// <param name="Name">Name</param>
/// <param name="Description">What the judges look at</param>
/// <param name="Active">Can be picked for new competitions</param>
/// <param name="UsedBy">Number of competitions using it</param>
public record CriterionListItemVm(long Id, string Name, string? Description, bool Active, int UsedBy);

/// <summary>Create / edit form of a criterion.</summary>
public class CriterionFormVm
{
    /// <summary>0 = new.</summary>
    public long Id { get; set; }

    /// <summary>Unique name, e.g. "Light and shadow".</summary>
    [Required(ErrorMessage = "Please enter the name of the criterion.")]
    [StringLength(100, ErrorMessage = "The name must be at most 100 characters.")]
    [Display(Name = "Name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>What the judges look at.</summary>
    [StringLength(500, ErrorMessage = "The description must be at most 500 characters.")]
    [Display(Name = "What the judges look at")]
    public string? Description { get; set; }

    /// <summary>Can be picked for new competitions.</summary>
    [Display(Name = "Available for new competitions")]
    public bool Active { get; set; } = true;
}

/// <summary>Lowest average score of each rating (below MODERATE = NORMAL).</summary>
public class ThresholdsFormVm
{
    /// <summary>BEST from this average.</summary>
    [Range(0, 100, ErrorMessage = "Between 0 and 100.")]
    public decimal? Best { get; set; }

    /// <summary>BETTER from this average.</summary>
    [Range(0, 100, ErrorMessage = "Between 0 and 100.")]
    public decimal? Better { get; set; }

    /// <summary>GOOD from this average.</summary>
    [Range(0, 100, ErrorMessage = "Between 0 and 100.")]
    public decimal? Good { get; set; }

    /// <summary>MODERATE from this average.</summary>
    [Range(0, 100, ErrorMessage = "Between 0 and 100.")]
    public decimal? Moderate { get; set; }
}
