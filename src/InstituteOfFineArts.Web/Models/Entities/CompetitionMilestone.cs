namespace InstituteOfFineArts.Web.Models.Entities;

/// <summary>
/// A date on the timeline of a competition (table competition_milestone, step 7 of 11/2026), written by the staff:
/// the announcement, the expected results day, the award ceremony, the exhibition… It is information only: the
/// submission period still comes from <see cref="Competition.StartDate"/> / <see cref="Competition.EndDate"/> and the
/// results from publishing, which appear on the timeline by themselves (see Helpers/CompetitionTimeline).
/// </summary>
public class CompetitionMilestone : BaseEntity
{
    /// <summary>Competition.</summary>
    public long CompetitionId { get; set; }

    /// <summary>Day of the milestone.</summary>
    public DateOnly Date { get; set; }

    /// <summary>Short title, e.g. "Award ceremony".</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>What happens that day (optional), e.g. the place and time of the ceremony.</summary>
    public string? Content { get; set; }

    /// <summary>Order among milestones of the same day (0 first).</summary>
    public int SortOrder { get; set; }

    /// <summary>Competition.</summary>
    public Competition Competition { get; set; } = null!;
}
