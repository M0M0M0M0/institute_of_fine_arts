using InstituteOfFineArts.Web.ViewModels.Admin.AuditLog;
using InstituteOfFineArts.Web.Infrastructure.Audit;
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
/// Staff do not grade: teachers do it in the Teacher area (/teacher/evaluations/{submissionId}).
/// </summary>
[Route("staff/submissions")]
public class SubmissionsController : StaffBaseController
{
    /// <summary>Submission queries.</summary>
    private readonly IStaffSubmissionService _submissions;

    /// <summary>History of the entry (audit log).</summary>
    private readonly IAuditLogService _audit;

    /// <summary>Receives the services through Dependency Injection.</summary>
    public SubmissionsController(IStaffSubmissionService submissions, IAuditLogService audit)
    {
        _submissions = submissions;
        _audit = audit;
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
    [AuditView]
    public async Task<IActionResult> Details(long id, [FromQuery] int page = 1)
    {
        try
        {
            var details = await _submissions.GetDetailsAsync(id);
            // The entry's own history: submitted, edited, opened by staff / teachers, graded, awarded, exhibited, sold
            ViewBag.History = new HistoryVm(
                await _audit.GetHistoryAsync(new AuditHistoryQuery { SubmissionId = id, Page = page, PageSize = 10 }),
                "History of this entry");
            return View(details);
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
    }
}
