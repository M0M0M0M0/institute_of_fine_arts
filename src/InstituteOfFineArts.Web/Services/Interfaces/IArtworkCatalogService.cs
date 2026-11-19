using InstituteOfFineArts.Web.ViewModels.Public;

namespace InstituteOfFineArts.Web.Services.Interfaces;

/// <summary>
/// Public browsing pages designed by FE101 (/gallery/…): paintings by subject / style / medium, new arrivals, all
/// artworks, the competition showcase and one painting's page. Only public paintings are shown (BR-20): BEST or awarded
/// entries of GRADED competitions; their images are served watermarked by FilesController.
/// </summary>
public interface IArtworkCatalogService
{
    /// <summary>All browse categories, in menu order.</summary>
    IReadOnlyList<ArtworkCategory> Categories { get; }

    /// <summary>Category by URL group and key (case-insensitive); null when unknown.</summary>
    ArtworkCategory? FindCategory(string group, string key);

    /// <summary>Public paintings of a category, newest results first.</summary>
    Task<ArtworkListVm> GetCategoryAsync(ArtworkCategory category, int page, int pageSize = 12);

    /// <summary>Public paintings of the most recently published competitions ("New Art This Month").</summary>
    Task<ArtworkListVm> GetNewArrivalsAsync(int page, int pageSize = 12);

    /// <summary>All public paintings.</summary>
    Task<ArtworkListVm> GetAllAsync(int page, int pageSize = 12);

    /// <summary>Public paintings of one competition (empty until its results are published).</summary>
    Task<ArtworkListVm> GetCompetitionArtworksAsync(long competitionId, int page, int pageSize = 12);

    /// <summary>Open competitions and the latest published results.</summary>
    Task<CompetitionShowcaseListVm> GetShowcaseAsync();

    /// <summary>
    /// Competitions of one tab of /gallery/competitions, filtered by title / code and status, paged.
    /// Order: ongoing (nearest deadline first), upcoming (soonest start), closed (latest deadline), graded (latest results).
    /// A status that belongs to another tab switches the tab (search.Tab is updated).
    /// </summary>
    /// <param name="search">Tab, keyword, status, page</param>
    /// <param name="pageSize">Competitions per page (large image rows: 6 by default)</param>
    Task<ViewModels.Common.PagedResult<CompetitionShowcaseVm>> SearchShowcaseAsync(GalleryCompetitionSearchVm search, int pageSize = 6);

    /// <summary>One public painting; NotFoundException when it does not exist or is not public.</summary>
    Task<ArtworkDetailVm> GetArtworkAsync(long submissionId);
}
