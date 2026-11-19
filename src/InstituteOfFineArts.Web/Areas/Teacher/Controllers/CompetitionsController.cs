using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Services.Interfaces;
using InstituteOfFineArts.Web.ViewModels.Staff.Competitions;
using Microsoft.AspNetCore.Mvc;

namespace InstituteOfFineArts.Web.Areas.Teacher.Controllers;

/// <summary>
/// Teacher – competitions, read only: GET /teacher/competitions (list + search) · GET /teacher/competitions/{id}
/// (information, rules, awards, winners, grading progress). Creating and editing competitions is the Staff's job.
/// The entries themselves open only once the competition is closed (see <see cref="SubmissionsController"/>).
/// </summary>
[Route("teacher/competitions")]
public class CompetitionsController : TeacherBaseController
{
    /// <summary>Competition service.</summary>
    private readonly ICompetitionService _competitions;

    /// <summary>Receives the competition service through Dependency Injection.</summary>
    public CompetitionsController(ICompetitionService competitions)
    {
        _competitions = competitions;
    }

    /// <summary>List with filters (keyword, status) and paging.</summary>
    /// <param name="search">Filters from the query string</param>
    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] CompetitionSearchVm search)
    {
        ViewBag.Search = search;
        return View(await _competitions.SearchAsync(search));
    }

    /// <summary>Details of one competition.</summary>
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
