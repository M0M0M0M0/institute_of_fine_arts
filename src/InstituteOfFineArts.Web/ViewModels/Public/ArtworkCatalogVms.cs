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

/// <summary>Tab of the "Open Art Competitions" page (?Tab=open|past|all).</summary>
public enum GalleryCompetitionTab
{
    /// <summary>Ongoing and upcoming competitions (default).</summary>
    Open,

    /// <summary>Competitions past their deadline: closed (results coming) and graded (results published).</summary>
    Past,

    /// <summary>Every competition.</summary>
    All,
}

/// <summary>
/// Filters of /gallery/competitions: tab, part of the title or code, status (calculated for today) and page.
/// With no filter at all the page shows its highlights (open competitions + the latest results).
/// </summary>
public class GalleryCompetitionSearchVm
{
    /// <summary>Which competitions the page lists.</summary>
    public GalleryCompetitionTab Tab { get; set; } = GalleryCompetitionTab.Open;

    /// <summary>Part of the title or the code.</summary>
    [System.ComponentModel.DataAnnotations.StringLength(100)]
    public string? Keyword { get; set; }

    /// <summary>Only this status (BR-07, calculated for today). A status of another tab switches to that tab.</summary>
    public CompetitionStatus? Status { get; set; }

    /// <summary>Page number (1-based).</summary>
    public int Page { get; set; } = 1;

    /// <summary>True when nothing is filtered: the page shows its highlights instead of a paged list.</summary>
    public bool IsDefault => Tab == GalleryCompetitionTab.Open && string.IsNullOrWhiteSpace(Keyword) && Status == null && Page <= 1;

    /// <summary>Statuses listed by a tab.</summary>
    public static CompetitionStatus[] StatusesOf(GalleryCompetitionTab tab) => tab switch
    {
        GalleryCompetitionTab.Open => [CompetitionStatus.ONGOING, CompetitionStatus.UPCOMING],
        GalleryCompetitionTab.Past => [CompetitionStatus.CLOSED, CompetitionStatus.GRADED],
        _ => Enum.GetValues<CompetitionStatus>(),
    };

    /// <summary>The tab that lists a status (used when the status filter does not belong to the chosen tab).</summary>
    public static GalleryCompetitionTab TabOf(CompetitionStatus status)
        => status is CompetitionStatus.ONGOING or CompetitionStatus.UPCOMING ? GalleryCompetitionTab.Open : GalleryCompetitionTab.Past;
}

/// <summary>/gallery/competitions: the filters, then either the highlights (no filter) or a paged list.</summary>
public class GalleryCompetitionsPageVm
{
    /// <summary>Current filters (the tab may have been switched by the status filter).</summary>
    public GalleryCompetitionSearchVm Search { get; set; } = new();

    /// <summary>Open competitions + latest results, when nothing is filtered.</summary>
    public CompetitionShowcaseListVm? Highlights { get; set; }

    /// <summary>Paged competitions of the tab / filters, when something is filtered.</summary>
    public PagedResult<CompetitionShowcaseVm>? Results { get; set; }
}

/// <summary>Competitions for the showcase page: open ones (ongoing, upcoming) and the latest published results.</summary>
public class CompetitionShowcaseListVm
{
    /// <summary>Ongoing then upcoming competitions.</summary>
    public List<CompetitionShowcaseVm> Open { get; set; } = new();

    /// <summary>Most recently published (GRADED) competitions, newest first.</summary>
    public List<CompetitionShowcaseVm> Results { get; set; } = new();
}

/// <summary>
/// One public painting (FE101 artwork detail). Students' texts and evaluations stay private (BR-20).
/// <see cref="ImageIds"/>: its pictures (submission_image ids, cover first; BR-18), empty for older entries.
/// </summary>
public record ArtworkDetailVm(long SubmissionId, string Title, string StudentName, long CompetitionId, string CompetitionTitle,
    string? CompetitionDescription, string? AwardName, int? AwardRank, bool IsBest, DateTime SubmittedAt)
{
    /// <summary>Pictures of the artwork, cover first.</summary>
    public IReadOnlyList<long> ImageIds { get; init; } = [];
}
