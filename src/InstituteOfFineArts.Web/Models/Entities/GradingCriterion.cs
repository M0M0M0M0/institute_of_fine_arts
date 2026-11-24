namespace InstituteOfFineArts.Web.Models.Entities;

/// <summary>
/// One criterion of the grading library (table grading_criterion), managed by the administrator, e.g. "Colour",
/// "Composition", "Idea". Competitions pick some of them with a weight. A criterion already used is never deleted,
/// only hidden (Active = false) so old score sheets keep their names.
/// </summary>
public class GradingCriterion : BaseEntity
{
    /// <summary>Name (unique), e.g. "Light and shadow".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>What the judges look at, shown on the grading form.</summary>
    public string? Description { get; set; }

    /// <summary>False = hidden: cannot be picked for a competition any more.</summary>
    public bool Active { get; set; } = true;
}
