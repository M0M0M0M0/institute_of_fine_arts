using InstituteOfFineArts.Web.Models.Enums;

namespace InstituteOfFineArts.Web.ViewModels.Staff.Competitions;

/// <summary>
/// Competition details page for staff (F-M05-05): information, rules, awards, number of entries, status.
/// </summary>
public class CompetitionDetailsVm
{
    /// <summary>Competition id.</summary>
    public long Id { get; set; }

    /// <summary>Code.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Description.</summary>
    public string? Description { get; set; }

    /// <summary>Rules and conditions.</summary>
    public string? Rules { get; set; }

    /// <summary>Start date.</summary>
    public DateOnly StartDate { get; set; }

    /// <summary>End date (deadline 23:59:59).</summary>
    public DateOnly EndDate { get; set; }

    /// <summary>Status calculated for today.</summary>
    public CompetitionStatus Status { get; set; }

    /// <summary>True when a cover image exists (shown from /files/competitions/{id}/cover).</summary>
    public bool HasCover { get; set; }

    /// <summary>Name of the staff member who created it.</summary>
    public string CreatedByName { get; set; } = string.Empty;

    /// <summary>When it was created.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>When results were published (GRADED), if they were.</summary>
    public DateTime? PublishedAt { get; set; }

    /// <summary>Number of entries.</summary>
    public int SubmissionCount { get; set; }

    /// <summary>Defined awards, best rank first.</summary>
    public List<AwardRowVm> Awards { get; set; } = new();

    /// <summary>Edit allowed (not GRADED).</summary>
    public bool CanEdit { get; set; }

    /// <summary>Delete allowed (no entries, BR-11).</summary>
    public bool CanDelete { get; set; }

    /// <summary>Advice for staff, e.g. missing awards before the start (BR-09).</summary>
    public List<string> Warnings { get; set; } = new();
}

/// <summary>One defined award of a competition.</summary>
/// <param name="Id">Award id</param>
/// <param name="Name">Award name, e.g. First Prize</param>
/// <param name="Rank">1 = highest</param>
/// <param name="PrizeValue">Prize in USD</param>
/// <param name="Quantity">How many can be given</param>
/// <param name="Description">Prize description</param>
public record AwardRowVm(long Id, string Name, int Rank, decimal PrizeValue, int Quantity, string? Description);
