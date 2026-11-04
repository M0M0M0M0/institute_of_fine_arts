using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Services.Implementations;
using InstituteOfFineArts.Web.Services.Interfaces;
using InstituteOfFineArts.Web.ViewModels.Staff.Submissions;
using Microsoft.AspNetCore.Mvc;

namespace InstituteOfFineArts.Web.Areas.Staff.Controllers;

/// <summary>
/// Staff – submissions, read only (M07, tasks BE2-06, BE2-07; screens SCR-26, SCR-27):
/// GET /staff/submissions (list + filters + evaluation progress) · GET /staff/submissions/{id} (details)
/// GET /staff/submissions/by-rating (rating search to pick paintings for exhibitions).
/// Evaluating is done at /staff/evaluations/{submissionId} (EvaluationsController, BE1-10).
/// </summary>
[Route("staff/submissions")]
public class SubmissionsController : StaffBaseController
{
    /// <summary>Submission queries.</summary>
    private readonly IStaffSubmissionService _submissions;

    /// <summary>Receives the submission service through Dependency Injection.</summary>
    public SubmissionsController(IStaffSubmissionService submissions)
    {
        _submissions = submissions;
    }

    /// <summary>List with filters and paging; with a competition chosen, also its evaluation progress.</summary>
    /// <param name="search">Filters from the query string</param>
    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] SubmissionSearchVm search)
    {
        ViewBag.Search = search;
        ViewBag.Competitions = await _submissions.GetCompetitionOptionsAsync();
        ViewBag.Progress = search.CompetitionId is { } competitionId ? await _submissions.GetProgressAsync(competitionId) : null;
        return View(await _submissions.SearchAsync(search));
    }

    /// <summary>Entries with one rating in CLOSED or GRADED competitions (F-M07-06).</summary>
    /// <param name="search">Rating, competition and page from the query string</param>
    [HttpGet("by-rating")]
    public async Task<IActionResult> ByRating([FromQuery] RatingSearchVm search)
    {
        // No rating chosen yet → search for BEST, the usual choice for exhibitions
        search.Rating ??= StaffSubmissionService.DefaultSearchRating;
        ViewBag.Search = search;
        ViewBag.Competitions = await _submissions.GetEvaluatedCompetitionOptionsAsync();
        return View(await _submissions.SearchByRatingAsync(search));
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
