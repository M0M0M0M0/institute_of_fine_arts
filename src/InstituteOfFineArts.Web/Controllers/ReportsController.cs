using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Services.Interfaces;
using InstituteOfFineArts.Web.ViewModels.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InstituteOfFineArts.Web.Controllers;

/// <summary>
/// Reports of M10 (task BE2-12 part 1; F-M10-01/02/03/06/08; BA doc §7; US-14): GET /reports (index of the reports the
/// user's role may open) · /reports/rpt-01 submissions by rating · /reports/rpt-02 participation &amp; ratings ·
/// /reports/rpt-03 awards issued · /reports/rpt-06 student achievement profile. RPT-04, 05, 07 are served by
/// FinanceReportsController. Every report is a GET form + table + totals, read only, with a Print button.
/// </summary>
[Route("reports")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class ReportsController : Controller
{
    /// <summary>Report queries.</summary>
    private readonly ICompetitionReportService _reports;

    /// <summary>Competition dropdown of RPT-01 / RPT-02 (every competition).</summary>
    private readonly IStaffSubmissionService _submissions;

    /// <summary>Competition dropdown of RPT-03 (CLOSED / GRADED competitions, the only ones with awards).</summary>
    private readonly IAwardIssuedService _awards;

    /// <summary>Receives the services through Dependency Injection.</summary>
    public ReportsController(ICompetitionReportService reports, IStaffSubmissionService submissions, IAwardIssuedService awards)
    {
        _reports = reports;
        _submissions = submissions;
        _awards = awards;
    }

    /// <summary>Index: the reports the current role may open, with a one-line description (F-M10-08).</summary>
    [HttpGet("")]
    [Authorize(Roles = ReportCatalog.AnyReportViewer)]
    public IActionResult Index()
        => View(ReportCatalog.All.Where(r => r.Roles.Any(User.IsInRole)).ToList());

    /// <summary>RPT-01: submissions of one competition in a date range by rating (F-M10-01, TC-M10-01..04).</summary>
    /// <param name="filter">Filters from the query string</param>
    [HttpGet("rpt-01")]
    [Authorize(Roles = ReportCatalog.StaffAndManager)]
    public async Task<IActionResult> Rpt01([FromQuery] Rpt01FilterVm filter)
    {
        var vm = new Rpt01PageVm
        {
            Filter = filter,
            Competitions = await _submissions.GetCompetitionOptionsAsync(),
            Classes = await _reports.GetClassOptionsAsync(),
        };

        // First visit: only the form (the competition is required, so do not show its error yet)
        if (Request.Query.Count == 0)
        {
            ModelState.Clear();
            return View(vm);
        }
        if (ModelState.IsValid)
        {
            try
            {
                vm.Result = await _reports.GetSubmissionsByRatingAsync(filter);
            }
            catch (BusinessException ex)
            {
                ModelState.AddModelError(ex.Field ?? string.Empty, ex.Message);
            }
        }
        return View(vm);
    }

    /// <summary>RPT-02: participation and ratings per competition, plus the BR-12 list of non-submitters (F-M10-02).</summary>
    /// <param name="filter">Filters from the query string</param>
    [HttpGet("rpt-02")]
    [Authorize(Roles = ReportCatalog.StaffAndManager)]
    public async Task<IActionResult> Rpt02([FromQuery] Rpt02FilterVm filter)
    {
        var vm = new Rpt02PageVm { Filter = filter, Competitions = await _submissions.GetCompetitionOptionsAsync() };

        // No required filter: the report runs at once (every competition)
        if (ModelState.IsValid)
        {
            try
            {
                vm.Result = await _reports.GetParticipationAsync(filter);
            }
            catch (BusinessException ex)
            {
                ModelState.AddModelError(ex.Field ?? string.Empty, ex.Message);
            }
        }
        return View(vm);
    }

    /// <summary>RPT-03: awards issued with the total prize value (F-M10-03).</summary>
    /// <param name="filter">Filters from the query string</param>
    [HttpGet("rpt-03")]
    [Authorize(Roles = ReportCatalog.StaffAndManager)]
    public async Task<IActionResult> Rpt03([FromQuery] Rpt03FilterVm filter)
    {
        var vm = new Rpt03PageVm { Filter = filter, Competitions = await _awards.GetCompetitionOptionsAsync() };

        // No required filter: the report runs at once (every issued award)
        if (ModelState.IsValid)
        {
            try
            {
                vm.Result = await _reports.GetAwardsIssuedAsync(filter);
            }
            catch (BusinessException ex)
            {
                ModelState.AddModelError(ex.Field ?? string.Empty, ex.Message);
            }
        }
        return View(vm);
    }

    /// <summary>RPT-06: achievement profile of one student (F-M10-06).</summary>
    /// <param name="filter">Filters from the query string</param>
    [HttpGet("rpt-06")]
    [Authorize(Roles = ReportCatalog.StaffAndManager)]
    public async Task<IActionResult> Rpt06([FromQuery] Rpt06FilterVm filter)
    {
        var vm = new Rpt06PageVm { Filter = filter, Students = await _reports.GetStudentOptionsAsync() };

        // First visit: only the form (the student is required, so do not show its error yet)
        if (Request.Query.Count == 0)
        {
            ModelState.Clear();
            return View(vm);
        }
        if (ModelState.IsValid)
        {
            try
            {
                vm.Result = await _reports.GetStudentProfileAsync(filter);
            }
            catch (BusinessException ex)
            {
                ModelState.AddModelError(ex.Field ?? string.Empty, ex.Message);
            }
        }
        return View(vm);
    }
}
