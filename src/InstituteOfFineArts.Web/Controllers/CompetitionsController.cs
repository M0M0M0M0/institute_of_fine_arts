using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Services.Interfaces;
using InstituteOfFineArts.Web.ViewModels.Public;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InstituteOfFineArts.Web.Controllers;

/// <summary>
/// Public competitions (task BE2-11; F-M02-05; screen SCR-02), open to guests and every role:
/// GET /competitions (search by title, status) · GET /competitions/{id} (description, rules, awards, winners).
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

    /// <summary>Competition list with filters and paging (summary only).</summary>
    /// <param name="search">Filters from the query string</param>
    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] PublicCompetitionSearchVm search)
    {
        ViewBag.Search = search;
        return View(await _site.SearchCompetitionsAsync(search));
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
