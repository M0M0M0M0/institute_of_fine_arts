using InstituteOfFineArts.Web.Infrastructure.Audit;
using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Services.Interfaces;
using InstituteOfFineArts.Web.ViewModels.Staff.Submissions;
using Microsoft.AspNetCore.Mvc;

namespace InstituteOfFineArts.Web.Areas.Teacher.Controllers;

/// <summary>
/// Teacher – entries to grade: GET /teacher/submissions (filters competition, evaluated, rating, student, dates; paged)
/// · GET /teacher/submissions/{id} (painting, texts, evaluation, award) with the link to the grading form.
/// Only entries of competitions whose submission period is over (CLOSED or GRADED) are shown – even the jury cannot
/// see a painting before the deadline. The rule sits in the service (closedOnly), so a typed URL gets 404 too.
/// </summary>
[Route("teacher/submissions")]
public class SubmissionsController : TeacherBaseController
{
    /// <summary>Read service of the submissions (shared with the Staff area).</summary>
    private readonly IStaffSubmissionService _submissions;

    /// <summary>Receives the submission service through Dependency Injection.</summary>
    public SubmissionsController(IStaffSubmissionService submissions)
    {
        _submissions = submissions;
    }

    /// <summary>List with filters and paging, plus the grading progress when one competition is chosen.</summary>
    /// <param name="search">Filters from the query string</param>
    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] SubmissionSearchVm search)
    {
        ViewBag.Search = search;
        var competitions = await _submissions.GetCompetitionOptionsAsync(closedOnly: true);
        ViewBag.Competitions = competitions;
        // Progress only for a competition the teacher may see
        ViewBag.Progress = search.CompetitionId is { } competitionId && competitions.Any(c => c.Id == competitionId)
            ? await _submissions.GetProgressAsync(competitionId)
            : null;
        return View(await _submissions.SearchAsync(search, closedOnly: true));
    }

    /// <summary>Details of one entry (404 while its competition still takes entries).</summary>
    /// <param name="id">Submission id</param>
    [HttpGet("{id:long}")]
    [AuditView]
    public async Task<IActionResult> Details(long id)
    {
        try
        {
            return View(await _submissions.GetDetailsAsync(id, closedOnly: true));
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
    }
}
