namespace InstituteOfFineArts.Web.ViewModels.Staff.Competitions;

/// <summary>
/// One milestone row of the competition create / edit form (timeline, step 7). The submission period and the
/// results come from the competition itself; these rows are the other dates (announcement, ceremony, exhibition…).
/// </summary>
public class CompetitionMilestoneRowVm
{
    /// <summary>Id of an existing milestone (0 = new row).</summary>
    public long Id { get; set; }

    /// <summary>Day.</summary>
    public DateOnly? Date { get; set; }

    /// <summary>Short title, e.g. "Award ceremony".</summary>
    public string? Title { get; set; }

    /// <summary>What happens that day (optional).</summary>
    public string? Content { get; set; }

    /// <summary>True when the user removed an existing milestone from the form.</summary>
    public bool Remove { get; set; }

    /// <summary>True for a new row with nothing typed in.</summary>
    public bool IsBlank => Id == 0 && Date == null && string.IsNullOrWhiteSpace(Title) && string.IsNullOrWhiteSpace(Content);
}
