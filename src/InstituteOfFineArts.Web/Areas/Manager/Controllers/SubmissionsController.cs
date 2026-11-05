using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Services.Interfaces;
using InstituteOfFineArts.Web.ViewModels.Staff.Submissions;
using Microsoft.AspNetCore.Mvc;

namespace InstituteOfFineArts.Web.Areas.Manager.Controllers;

/// <summary>
/// Manager – submissions, read only (task BE2-13; F-M11-05, F-M11-06, SCR-53, TC-M11-02, TC-M11-03):
/// GET /manager/submissions (filters competition, evaluated, rating, student, date range; paged) ·
/// GET /manager/submissions/{id} (painting + download, texts, rating and staff comments, award, exhibitions).
/// Same read service as the Staff area; evaluating stays in the Staff area.
/// </summary>
[Route("manager/submissions")]
public class SubmissionsController : ManagerBaseController
{
    /// <summary>Submission queries.</summary>
    private readonly IStaffSubmissionService _submissions;

    /// <summary>Receives the submission service through Dependency Injection.</summary>
    public SubmissionsController(IStaffSubmissionService submissions)
    {
        _submissions = submissions;
    }

    /// <summary>List with filters and paging; with a competition chosen, also its grading progress.</summary>
    /// <param name="search">Filters from the query string</param>
    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] SubmissionSearchVm search)
    {
        ViewBag.Search = search;
        ViewBag.Competitions = await _submissions.GetCompetitionOptionsAsync();
        ViewBag.Progress = search.CompetitionId is { } competitionId ? await _submissions.GetProgressAsync(competitionId) : null;
        return View(await _submissions.SearchAsync(search));
    }

    /// <summary>Details: painting, texts, student, competition, evaluation, award, exhibition entries.</summary>
    /// <param name="id">Submission id</param>
    [HttpGet("{id:long}")]
    public async Task<IActionResult> Details(long id)
    {
        try
        {
            return View(await _submissions.GetDetailsAsync(id));
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
    }
}
