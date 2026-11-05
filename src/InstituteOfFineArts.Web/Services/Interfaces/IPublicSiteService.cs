using InstituteOfFineArts.Web.ViewModels.Common;
using InstituteOfFineArts.Web.ViewModels.Public;

namespace InstituteOfFineArts.Web.Services.Interfaces;

/// <summary>
/// Public pages open to guests and every role (task BE2-11; M02, SCR-01/02/06, US-02): home page, competition
/// list and details, gallery. Read only; returns only public data – never an evaluation or the text /
/// description of a submission (role matrix §2.3). Unknown ids throw NotFoundException.
/// </summary>
public interface IPublicSiteService
{
    /// <summary>
    /// Home page blocks: upcoming competitions, ongoing competitions with days left and entry count, winners of the
    /// most recent GRADED competition, upcoming exhibitions (F-M02-01..04, 06).
    /// </summary>
    Task<HomeVm> GetHomeAsync();

    /// <summary>Competitions filtered by title / code and status calculated for today, paged (F-M02-05, TC-M02-05).</summary>
    /// <param name="search">Filters</param>
    /// <param name="pageSize">Rows per page</param>
    Task<PagedResult<PublicCompetitionListItemVm>> SearchCompetitionsAsync(PublicCompetitionSearchVm search, int pageSize = 10);

    /// <summary>Public details: description, rules, dates, awards and, when GRADED, the winners (F-M02-05).</summary>
    /// <param name="id">Competition id</param>
    Task<PublicCompetitionDetailsVm> GetCompetitionDetailsAsync(long id);

    /// <summary>Awarded paintings of GRADED competitions, latest results first (F-M02-07).</summary>
    /// <param name="page">Page number (1-based)</param>
    /// <param name="pageSize">Paintings per page</param>
    Task<PagedResult<GalleryItemVm>> GetGalleryAsync(int page, int pageSize = 12);
}
