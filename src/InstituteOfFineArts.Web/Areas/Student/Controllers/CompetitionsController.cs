using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Infrastructure.Security;
using InstituteOfFineArts.Web.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace InstituteOfFineArts.Web.Areas.Student.Controllers;

/// <summary>
/// Student – competitions (F-M05-09, task BE1-06; screens SCR-41, SCR-42):
/// GET /student/competitions (upcoming + ongoing) · GET /student/competitions/{id} (details, awards, rules, own entry).
/// </summary>
[Route("student/competitions")]
public class CompetitionsController : StudentBaseController
{
    /// <summary>Student competition queries.</summary>
    private readonly IStudentCompetitionService _competitions;

    /// <summary>Receives the service through Dependency Injection.</summary>
    public CompetitionsController(IStudentCompetitionService competitions)
    {
        _competitions = competitions;
    }

    /// <summary>Upcoming and ongoing competitions with award summary and whether the student has submitted.</summary>
    [HttpGet("")]
    public async Task<IActionResult> Index()
        => View(await _competitions.GetOpenCompetitionsAsync(User.GetProfileId()));

    /// <summary>Details of one competition (closed / graded ones are read only).</summary>
    /// <param name="id">Competition id</param>
    [HttpGet("{id:long}")]
    public async Task<IActionResult> Details(long id)
    {
        try
        {
            return View(await _competitions.GetDetailsAsync(id, User.GetProfileId()));
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
    }
}
