using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Services.Interfaces;
using InstituteOfFineArts.Web.ViewModels.Public;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InstituteOfFineArts.Web.Controllers;

/// <summary>
/// Public browsing pages designed by FE101 (mega menu "Gallery" and "Competitions" showcase), with real data:
/// <list type="bullet">
/// <item>/gallery/all, /gallery/new – all public paintings, those of the latest published competitions</item>
/// <item>/gallery/{subject|style|medium}/{category} – paintings of a category (fixed list, anything else → 404)</item>
/// <item>/gallery/competitions – open competitions and latest results; /gallery/competitions/{id} – one competition;
///       /gallery/competitions/{id}/artworks – its public paintings</item>
/// <item>/gallery/artworks/{id} – one public painting</item>
/// </list>
/// Only public paintings are listed (BR-20); /gallery itself (awarded paintings) stays in HomeController.
/// Entries are submitted through the real student page (/student/submissions/new), which applies every rule.
/// </summary>
[AllowAnonymous]
[Route("gallery")]
public class GalleryController : Controller
{
    private readonly IArtworkCatalogService _catalog;
    private readonly IPublicSiteService _site;

    public GalleryController(IArtworkCatalogService catalog, IPublicSiteService site)
    {
        _catalog = catalog;
        _site = site;
    }

    /// <summary>All public paintings ("See All Artworks").</summary>
    [HttpGet("all")]
    public async Task<IActionResult> All(int page = 1)
        => View("Artworks", await _catalog.GetAllAsync(page));

    /// <summary>Paintings of the latest published competitions ("New Art This Month").</summary>
    [HttpGet("new")]
    public async Task<IActionResult> NewArrivals(int page = 1)
        => View("Artworks", await _catalog.GetNewArrivalsAsync(page));

    /// <summary>Paintings of one category; the group and category must be in the fixed list.</summary>
    /// <param name="group">subject, style or medium</param>
    /// <param name="category">e.g. Animals, Abstract, OilPaintings</param>
    /// <param name="page">Page number</param>
    [HttpGet("{group:regex(^(subject|style|medium)$)}/{category}")]
    public async Task<IActionResult> Category(string group, string category, int page = 1)
    {
        var found = _catalog.FindCategory(group, category);
        if (found == null)
        {
            return NotFound();
        }
        return View("Artworks", await _catalog.GetCategoryAsync(found, page));
    }

    /// <summary>
    /// "Open Art Competitions", the main competition page: tabs Open / Past / All, search by title or code, status filter.
    /// With no filter: ongoing and upcoming competitions, then the latest results (highlights).
    /// </summary>
    [HttpGet("competitions")]
    public async Task<IActionResult> Competitions([FromQuery] GalleryCompetitionSearchVm search)
    {
        if (!ModelState.IsValid)
        {
            search = new GalleryCompetitionSearchVm();
        }
        var vm = new GalleryCompetitionsPageVm { Search = search };
        if (search.IsDefault)
        {
            vm.Highlights = await _catalog.GetShowcaseAsync();
        }
        else
        {
            vm.Results = await _catalog.SearchShowcaseAsync(search);
        }
        return View(vm);
    }

    /// <summary>One competition: overview, rules, prizes, deadline and the way to enter.</summary>
    [HttpGet("competitions/{id:long}")]
    public async Task<IActionResult> Competition(long id)
    {
        try
        {
            return View(await _site.GetCompetitionDetailsAsync(id));
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>Public paintings of one competition (once its results are published).</summary>
    [HttpGet("competitions/{id:long}/artworks")]
    public async Task<IActionResult> CompetitionArtworks(long id, int page = 1)
    {
        try
        {
            return View("Artworks", await _catalog.GetCompetitionArtworksAsync(id, page));
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>One public painting (title, student, competition, award); private entries → 404.</summary>
    [HttpGet("artworks/{id:long}")]
    public async Task<IActionResult> Artwork(long id)
    {
        try
        {
            return View(await _catalog.GetArtworkAsync(id));
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
    }
}
