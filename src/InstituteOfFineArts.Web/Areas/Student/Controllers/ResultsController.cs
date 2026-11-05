using InstituteOfFineArts.Web.Infrastructure.Security;
using InstituteOfFineArts.Web.Services.Interfaces;
using InstituteOfFineArts.Web.ViewModels.Student.Results;
using Microsoft.AspNetCore.Mvc;

namespace InstituteOfFineArts.Web.Areas.Student.Controllers;

/// <summary>
/// Student – results (task BE2-08; F-M06-07..09, F-M08-04 pub; screens SCR-46, SCR-47), read only:
/// GET /student/results (my results) · GET /student/best (BEST works of every student with staff comments)
/// GET /student/awards (awards issued in GRADED competitions).
/// </summary>
[Route("student")]
public class ResultsController : StudentBaseController
{
    /// <summary>Result queries.</summary>
    private readonly IStudentResultService _results;

    /// <summary>The student's own entries (competition filter options).</summary>
    private readonly ISubmissionService _submissions;

    /// <summary>Receives the services through Dependency Injection.</summary>
    public ResultsController(IStudentResultService results, ISubmissionService submissions)
    {
        _results = results;
        _submissions = submissions;
    }

    /// <summary>Every own entry with rating, comments and award once published; "Awaiting results" before (BR-24).</summary>
    /// <param name="search">Filters from the query string</param>
    [HttpGet("results")]
    public async Task<IActionResult> Index([FromQuery] MyResultSearchVm search)
    {
        var me = User.GetProfileId();
        ViewBag.Search = search;
        ViewBag.Competitions = await _submissions.GetMyCompetitionsAsync(me);
        return View(await _results.SearchMyResultsAsync(me, search));
    }

    /// <summary>BEST entries of GRADED competitions with the staff comments.</summary>
    /// <param name="search">Filters from the query string</param>
    [HttpGet("best")]
    public async Task<IActionResult> Best([FromQuery] BestWorkSearchVm search)
    {
        ViewBag.Search = search;
        ViewBag.Competitions = await _results.GetGradedCompetitionsAsync();
        return View(await _results.SearchBestWorksAsync(search));
    }

    /// <summary>Awards issued in GRADED competitions, searchable by competition, student, award and date.</summary>
    /// <param name="search">Filters from the query string</param>
    [HttpGet("awards")]
    public async Task<IActionResult> Awards([FromQuery] IssuedAwardSearchVm search)
    {
        ViewBag.Search = search;
        ViewBag.Competitions = await _results.GetGradedCompetitionsAsync();
        return View(await _results.SearchAwardsAsync(search));
    }
}
