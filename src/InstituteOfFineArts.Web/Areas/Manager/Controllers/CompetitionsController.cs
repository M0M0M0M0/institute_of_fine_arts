using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Services.Interfaces;
using InstituteOfFineArts.Web.ViewModels.Staff.Competitions;
using Microsoft.AspNetCore.Mvc;

namespace InstituteOfFineArts.Web.Areas.Manager.Controllers;

/// <summary>
/// Manager – competitions, read only (task BE2-13; F-M11-03, SCR-52, TC-M11-02):
/// GET /manager/competitions (list + search) · GET /manager/competitions/{id} (information, awards, winners,
/// number of entries, grading progress). Editing stays in the Staff area (POSTs there return 403 for MANAGER, US-15 AC2).
/// </summary>
[Route("manager/competitions")]
public class CompetitionsController : ManagerBaseController
{
    /// <summary>Competition service.</summary>
    private readonly ICompetitionService _competitions;

    /// <summary>Receives the competition service through Dependency Injection.</summary>
    public CompetitionsController(ICompetitionService competitions)
    {
        _competitions = competitions;
    }

    /// <summary>List with filters (keyword, calculated status, date range) and paging.</summary>
    /// <param name="search">Filters from the query string</param>
    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] CompetitionSearchVm search)
    {
        ViewBag.Search = search;
        return View(await _competitions.SearchAsync(search));
    }

    /// <summary>Details: information, rules, awards with winners, entries and grading progress.</summary>
    /// <param name="id">Competition id</param>
    [HttpGet("{id:long}")]
    public async Task<IActionResult> Details(long id)
    {
        try
        {
            return View(await _competitions.GetDetailsAsync(id));
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
    }
}
