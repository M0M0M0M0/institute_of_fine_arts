namespace InstituteOfFineArts.Web.ViewModels.Staff.Competitions;

/// <summary>
/// One award row of the competition create / edit form (CompetitionFormVm.Awards). No data annotations on purpose:
/// empty rows are allowed (and ignored), the service checks the filled ones with the same rules as the award form
/// (CompetitionAwardService: name, rank ≥ 1, prize ≥ 0 with 2 decimals, quantity ≥ 1, unique name).
/// </summary>
public class CompetitionAwardRowVm
{
    /// <summary>Id of an existing award (0 = new row).</summary>
    public long Id { get; set; }

    /// <summary>Award name, e.g. "First Prize".</summary>
    public string? AwardName { get; set; }

    /// <summary>Rank (1 = highest).</summary>
    public int? Rank { get; set; }

    /// <summary>Prize value in USD.</summary>
    public decimal? PrizeValue { get; set; }

    /// <summary>Number of winners of this award.</summary>
    public int? Quantity { get; set; }

    /// <summary>Optional text, e.g. "Certificate and art supplies".</summary>
    public string? PrizeDescription { get; set; }

    /// <summary>True when the user removed an existing award from the form.</summary>
    public bool Remove { get; set; }

    /// <summary>How many times the award has been given (from the database, never from the browser).</summary>
    public int IssuedCount { get; set; }

    /// <summary>True for a new row with nothing typed in (rank and quantity have defaults, so they do not count).</summary>
    public bool IsBlank => Id == 0 && string.IsNullOrWhiteSpace(AwardName) && PrizeValue == null && string.IsNullOrWhiteSpace(PrizeDescription);
}
