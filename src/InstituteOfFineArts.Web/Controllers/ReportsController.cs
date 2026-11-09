using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Services.Implementations;
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
/// Export (task BE2-OPT 1; F-M14-01): GET /reports/rpt-0N/export?format=xlsx|pdf&amp;&lt;same filters&gt; runs the same
/// report service and returns the same rows and totals as an Excel / PDF file; invalid filters → back to the page.
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

    /// <summary>Excel / PDF writer of the reports.</summary>
    private readonly IReportExportService _export;

    /// <summary>Receives the services through Dependency Injection.</summary>
    public ReportsController(ICompetitionReportService reports, IStaffSubmissionService submissions, IAwardIssuedService awards,
        IReportExportService export)
    {
        _reports = reports;
        _submissions = submissions;
        _awards = awards;
        _export = export;
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

    // ------------------------------------------------------------------ Export (F-M14-01)

    /// <summary>RPT-01 as Excel / PDF (same filters and roles as <see cref="Rpt01"/>).</summary>
    /// <param name="filter">Filters from the query string</param>
    /// <param name="format">xlsx or pdf</param>
    [HttpGet("rpt-01/export")]
    [Authorize(Roles = ReportCatalog.StaffAndManager)]
    public async Task<IActionResult> Rpt01Export([FromQuery] Rpt01FilterVm filter, [FromQuery] string? format)
    {
        if (CheckExport(format, out var exportFormat) is { } refused)
        {
            return ExportRefused(nameof(Rpt01), refused);
        }
        try
        {
            var result = await _reports.GetSubmissionsByRatingAsync(filter);
            return ExportFile(_export.BuildRpt01(result, filter), exportFormat);
        }
        catch (BusinessException ex)
        {
            return ExportRefused(nameof(Rpt01), ex.Message);
        }
    }

    /// <summary>RPT-02 as Excel / PDF (same filters and roles as <see cref="Rpt02"/>).</summary>
    /// <param name="filter">Filters from the query string</param>
    /// <param name="format">xlsx or pdf</param>
    [HttpGet("rpt-02/export")]
    [Authorize(Roles = ReportCatalog.StaffAndManager)]
    public async Task<IActionResult> Rpt02Export([FromQuery] Rpt02FilterVm filter, [FromQuery] string? format)
    {
        if (CheckExport(format, out var exportFormat) is { } refused)
        {
            return ExportRefused(nameof(Rpt02), refused);
        }
        try
        {
            var result = await _reports.GetParticipationAsync(filter);
            return ExportFile(_export.BuildRpt02(result, filter), exportFormat);
        }
        catch (BusinessException ex)
        {
            return ExportRefused(nameof(Rpt02), ex.Message);
        }
    }

    /// <summary>RPT-03 as Excel / PDF (same filters and roles as <see cref="Rpt03"/>).</summary>
    /// <param name="filter">Filters from the query string</param>
    /// <param name="format">xlsx or pdf</param>
    [HttpGet("rpt-03/export")]
    [Authorize(Roles = ReportCatalog.StaffAndManager)]
    public async Task<IActionResult> Rpt03Export([FromQuery] Rpt03FilterVm filter, [FromQuery] string? format)
    {
        if (CheckExport(format, out var exportFormat) is { } refused)
        {
            return ExportRefused(nameof(Rpt03), refused);
        }
        try
        {
            var result = await _reports.GetAwardsIssuedAsync(filter);

            // Competition named as on the page ("all" when none or unknown)
            var competition = (await _awards.GetCompetitionOptionsAsync()).FirstOrDefault(c => c.Id == filter.CompetitionId);
            var label = competition == null ? null : $"{competition.Code} – {competition.Title}";
            return ExportFile(_export.BuildRpt03(result, filter, label), exportFormat);
        }
        catch (BusinessException ex)
        {
            return ExportRefused(nameof(Rpt03), ex.Message);
        }
    }

    /// <summary>RPT-06 as Excel / PDF (same filters and roles as <see cref="Rpt06"/>).</summary>
    /// <param name="filter">Filters from the query string</param>
    /// <param name="format">xlsx or pdf</param>
    [HttpGet("rpt-06/export")]
    [Authorize(Roles = ReportCatalog.StaffAndManager)]
    public async Task<IActionResult> Rpt06Export([FromQuery] Rpt06FilterVm filter, [FromQuery] string? format)
    {
        if (CheckExport(format, out var exportFormat) is { } refused)
        {
            return ExportRefused(nameof(Rpt06), refused);
        }
        try
        {
            var result = await _reports.GetStudentProfileAsync(filter);
            return ExportFile(_export.BuildRpt06(result, filter), exportFormat);
        }
        catch (BusinessException ex)
        {
            return ExportRefused(nameof(Rpt06), ex.Message);
        }
    }

    /// <summary>Checks the format and the filter validation; returns the error message, or null when the export may run.</summary>
    /// <param name="format">Query value of format</param>
    /// <param name="exportFormat">Parsed format</param>
    private string? CheckExport(string? format, out ReportExportFormat exportFormat)
    {
        if (!ReportExportService.TryParseFormat(format, out exportFormat))
        {
            return ReportExportService.UnknownFormatMessage;
        }

        // Same DataAnnotations as the HTML page (required competition / student, lengths, bad dates)
        return ModelState.IsValid
            ? null
            : ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).FirstOrDefault(m => m.Length > 0)
              ?? ReportExportService.InvalidFiltersMessage;
    }

    /// <summary>Invalid filters: back to the HTML report with the same filters and the error message.</summary>
    /// <param name="action">HTML report action</param>
    /// <param name="message">Why the export was refused</param>
    private RedirectResult ExportRefused(string action, string message)
    {
        TempData["Error"] = ReportExportService.ExportFailedMessage + message;
        return Redirect(ReportExportService.HtmlReportUrl(Url.Action(action)!, Request.Query));
    }

    /// <summary>Writes the document and returns it as a download.</summary>
    /// <param name="document">Report content</param>
    /// <param name="format">Wanted format</param>
    private FileContentResult ExportFile(ReportExportDocument document, ReportExportFormat format)
    {
        var file = _export.Render(document, format);
        return File(file.Content, file.ContentType, file.FileName);
    }
}
