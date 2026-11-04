using InstituteOfFineArts.Web.Models.Enums;

namespace InstituteOfFineArts.Web.ViewModels.Staff.Competitions;

/// <summary>
/// One row of the competition list.
/// </summary>
public class CompetitionListItemVm
{
    /// <summary>Competition id.</summary>
    public long Id { get; set; }

    /// <summary>Code, e.g. CT2026-03.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Start date.</summary>
    public DateOnly StartDate { get; set; }

    /// <summary>End date (deadline).</summary>
    public DateOnly EndDate { get; set; }

    /// <summary>Status calculated for today (BR-07).</summary>
    public CompetitionStatus Status { get; set; }

    /// <summary>Number of entries.</summary>
    public int SubmissionCount { get; set; }

    /// <summary>Number of defined awards.</summary>
    public int AwardCount { get; set; }

    /// <summary>True when a cover image exists.</summary>
    public bool HasCover { get; set; }
}
