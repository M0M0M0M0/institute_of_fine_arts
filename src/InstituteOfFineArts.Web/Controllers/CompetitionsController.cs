using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Services.Interfaces;
using InstituteOfFineArts.Web.ViewModels.Public;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InstituteOfFineArts.Web.Controllers;

/// <summary>
/// Public competitions (task BE2-11; F-M02-05; screen SCR-02), open to guests and every role:
/// GET /competitions/search (search by title, status; header search box) · GET /competitions/{id} (description, rules,
/// awards, winners). GET /competitions redirects (main competition page: /gallery/competitions).
/// </summary>
[AllowAnonymous]
[Route("competitions")]
public class CompetitionsController : Controller
{
    /// <summary>Public queries.</summary>
    private readonly IPublicSiteService _site;

    /// <summary>Receives the service through Dependency Injection.</summary>
    public CompetitionsController(IPublicSiteService site)
    {
        _site = site;
    }

    /// <summary>Old list address /competitions: redirects to the search page (with a query) or to /gallery/competitions.</summary>
    [HttpGet("")]
    public IActionResult Index()
    {
        // Old address: with a search (Keyword, Status, page…) → the search page with the same query;
        // without → the main competition page /gallery/competitions
        return Request.QueryString.HasValue
            ? Redirect("/competitions/search" + Request.QueryString.Value)
            : Redirect("/gallery/competitions");
    }

    /// <summary>Competition search (header search box): filters and paging, summary only, no images.</summary>
    /// <param name="search">Filters from the query string</param>
    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] PublicCompetitionSearchVm search)
    {
        ViewBag.Search = search;
        // More rows per page than the old list: the search page is a short summary list
        return View(await _site.SearchCompetitionsAsync(search, pageSize: 20));
    }

    /// <summary>Public details; the view shows "Log in to submit" to guests and "Submit an entry" to students.</summary>
    /// <param name="id">Competition id</param>
    [HttpGet("{id:long}")]
    public async Task<IActionResult> Details(long id)
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
}
