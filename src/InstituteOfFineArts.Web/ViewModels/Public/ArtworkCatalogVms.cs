using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.ViewModels.Common;

namespace InstituteOfFineArts.Web.ViewModels.Public;

/// <summary>
/// One browse category of the public gallery (FE101 mega menu: Subject / Style / Medium). The paintings are the public
/// ones (BR-20) whose title or description mentions one of the keywords.
/// </summary>
/// <param name="Group">"subject", "style" or "medium" (first part of the URL)</param>
/// <param name="Key">Category in the URL, e.g. "Animals", "OilPaintings"</param>
/// <param name="Title">Page heading</param>
/// <param name="Intro">Introduction written by FE101</param>
/// <param name="Keywords">Words looked for in the painting's title and description (case-insensitive)</param>
public record ArtworkCategory(string Group, string Key, string Title, string Intro, string[] Keywords)
{
    /// <summary>Label of the category in menus ("Oil Paintings").</summary>
    public string Label => System.Text.RegularExpressions.Regex.Replace(Key, "(?<=[a-z])(?=[A-Z])", " ");
}

/// <summary>A public painting in a list (image, title, student, competition, award).</summary>
public record ArtworkCardVm(long SubmissionId, string Title, string StudentName, long CompetitionId, string CompetitionTitle,
    string? AwardName, bool HasThumbnail);

/// <summary>A page of public paintings: category, new arrivals, all artworks or one competition's artworks.</summary>
public class ArtworkListVm
{
    /// <summary>Page heading.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Text under the heading.</summary>
    public string? Intro { get; set; }

    /// <summary>Last breadcrumb item.</summary>
    public string Crumb { get; set; } = string.Empty;

    /// <summary>Category shown (null for new arrivals / all / a competition), to highlight it in the side menu.</summary>
    public ArtworkCategory? Category { get; set; }

    /// <summary>Competition whose artworks are shown (null otherwise).</summary>
    public long? CompetitionId { get; set; }

    /// <summary>The paintings, paged.</summary>
    public PagedResult<ArtworkCardVm> Items { get; set; } = new();

    /// <summary>Message when the list is empty.</summary>
    public string EmptyMessage { get; set; } = "No paintings here yet.";
}

/// <summary>A competition on the "Open Art Competitions" page (FE101 Commissions).</summary>
public record CompetitionShowcaseVm(long Id, string Title, string? Description, DateOnly StartDate, DateOnly EndDate,
    CompetitionStatus Status, bool HasCover, int SubmissionCount);

/// <summary>Competitions for the showcase page: open ones (ongoing, upcoming) and the latest published results.</summary>
public class CompetitionShowcaseListVm
{
    /// <summary>Ongoing then upcoming competitions.</summary>
    public List<CompetitionShowcaseVm> Open { get; set; } = new();

    /// <summary>Most recently published (GRADED) competitions, newest first.</summary>
    public List<CompetitionShowcaseVm> Results { get; set; } = new();
}

/// <summary>One public painting (FE101 artwork detail). Students' texts and evaluations stay private (BR-20).</summary>
public record ArtworkDetailVm(long SubmissionId, string Title, string StudentName, long CompetitionId, string CompetitionTitle,
    string? CompetitionDescription, string? AwardName, int? AwardRank, bool IsBest, DateTime SubmittedAt);
