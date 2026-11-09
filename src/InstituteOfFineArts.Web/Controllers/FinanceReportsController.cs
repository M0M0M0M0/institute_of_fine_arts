using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Helpers;
using InstituteOfFineArts.Web.Services.Implementations;
using InstituteOfFineArts.Web.Services.Interfaces;
using InstituteOfFineArts.Web.ViewModels.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InstituteOfFineArts.Web.Controllers;

/// <summary>
/// Financial and account reports (task BE2-12 part 2; F-M10-04, 05, 07, 08; BA §7; screen SCR-33), read-only:
/// GET /reports/rpt-04 (exhibition artworks &amp; sales revenue) · GET /reports/rpt-05 (money owed to students) –
/// STAFF and MANAGER; GET /reports/rpt-07 (account statistics) – ADMIN and MANAGER. Any other role → /error/403,
/// guests → login. Each page is a GET filter form, a printable table and the totals of §7.
/// Export (task BE2-OPT 1; F-M14-01): GET /reports/rpt-04|05|07/export?format=xlsx|pdf&amp;&lt;same filters&gt; returns
/// the same rows and totals as an Excel / PDF file (same roles); invalid filters → back to the page with the error.
/// </summary>
[Route("reports")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class FinanceReportsController : Controller
{
    /// <summary>Roles allowed to view RPT-01..06 (BA §7).</summary>
    private const string StaffAndManager = RoleNames.Staff + "," + RoleNames.Manager;

    /// <summary>Roles allowed to view RPT-07 (BA §7).</summary>
    private const string AdminAndManager = RoleNames.Admin + "," + RoleNames.Manager;

    /// <summary>RPT-04 / RPT-05 queries.</summary>
    private readonly IExhibitionReportService _exhibitionReports;

    /// <summary>RPT-07 queries.</summary>
    private readonly IAccountReportService _accountReports;

    /// <summary>Exhibition options of the filter (existing entry service).</summary>
    private readonly IExhibitionEntryService _entries;

    /// <summary>Excel / PDF writer of the reports.</summary>
    private readonly IReportExportService _export;

    /// <summary>Receives the services through Dependency Injection.</summary>
    public FinanceReportsController(IExhibitionReportService exhibitionReports, IAccountReportService accountReports,
        IExhibitionEntryService entries, IReportExportService export)
    {
        _exhibitionReports = exhibitionReports;
        _accountReports = accountReports;
        _entries = entries;
        _export = export;
    }

    /// <summary>RPT-04: exhibition artworks with asking / sale price, buyer, payment to the student and totals.</summary>
    /// <param name="search">Filters from the query string</param>
    [HttpGet("rpt-04")]
    [Authorize(Roles = StaffAndManager)]
    public async Task<IActionResult> Rpt04([FromQuery] ExhibitionSalesSearchVm search)
    {
        ViewBag.Search = search;
        ViewBag.Exhibitions = await _entries.GetExhibitionOptionsAsync();
        try
        {
            return View(await _exhibitionReports.GetSalesReportAsync(search));
        }
        catch (BusinessException ex)
        {
            // From > To (TC-M10-03): the form shows the error, no table
            ModelState.AddModelError(ex.Field ?? string.Empty, ex.Message);
            return View((ExhibitionSalesReportVm?)null);
        }
    }

    /// <summary>RPT-05: sold paintings whose money has not been paid to the student, with days unpaid and the total.</summary>
    /// <param name="search">Filters from the query string</param>
    [HttpGet("rpt-05")]
    [Authorize(Roles = StaffAndManager)]
    public async Task<IActionResult> Rpt05([FromQuery] StudentDebtSearchVm search)
    {
        ViewBag.Search = search;
        ViewBag.Exhibitions = await _entries.GetExhibitionOptionsAsync();
        try
        {
            return View(await _exhibitionReports.GetStudentDebtReportAsync(search));
        }
        catch (BusinessException ex)
        {
            // From > To (TC-M10-03): the form shows the error, no table
            ModelState.AddModelError(ex.Field ?? string.Empty, ex.Message);
            return View((StudentDebtReportVm?)null);
        }
    }

    /// <summary>RPT-07: accounts per role and status, students per course / class and enrollment status.</summary>
    /// <param name="search">Filters from the query string</param>
    [HttpGet("rpt-07")]
    [Authorize(Roles = AdminAndManager)]
    public async Task<IActionResult> Rpt07([FromQuery] AccountStatisticsSearchVm search)
    {
        ViewBag.Search = search;
        return View(await _accountReports.GetAccountStatisticsAsync(search));
    }

    // ------------------------------------------------------------------ Export (F-M14-01)

    /// <summary>RPT-04 as Excel / PDF (same filters and roles as <see cref="Rpt04"/>).</summary>
    /// <param name="search">Filters from the query string</param>
    /// <param name="format">xlsx or pdf</param>
    [HttpGet("rpt-04/export")]
    [Authorize(Roles = StaffAndManager)]
    public async Task<IActionResult> Rpt04Export([FromQuery] ExhibitionSalesSearchVm search, [FromQuery] string? format)
    {
        if (!ReportExportService.TryParseFormat(format, out var exportFormat))
        {
            return ExportRefused(nameof(Rpt04), ReportExportService.UnknownFormatMessage);
        }
        try
        {
            var result = await _exhibitionReports.GetSalesReportAsync(search);
            return ExportFile(_export.BuildRpt04(result, search, await ExhibitionNameAsync(search.ExhibitionId)), exportFormat);
        }
        catch (BusinessException ex)
        {
            // From > To (TC-M10-03)
            return ExportRefused(nameof(Rpt04), ex.Message);
        }
    }

    /// <summary>RPT-05 as Excel / PDF (same filters and roles as <see cref="Rpt05"/>).</summary>
    /// <param name="search">Filters from the query string</param>
    /// <param name="format">xlsx or pdf</param>
    [HttpGet("rpt-05/export")]
    [Authorize(Roles = StaffAndManager)]
    public async Task<IActionResult> Rpt05Export([FromQuery] StudentDebtSearchVm search, [FromQuery] string? format)
    {
        if (!ReportExportService.TryParseFormat(format, out var exportFormat))
        {
            return ExportRefused(nameof(Rpt05), ReportExportService.UnknownFormatMessage);
        }
        try
        {
            var result = await _exhibitionReports.GetStudentDebtReportAsync(search);
            return ExportFile(_export.BuildRpt05(result, search, await ExhibitionNameAsync(search.ExhibitionId)), exportFormat);
        }
        catch (BusinessException ex)
        {
            // From > To (TC-M10-03)
            return ExportRefused(nameof(Rpt05), ex.Message);
        }
    }

    /// <summary>RPT-07 as Excel / PDF (same filters and roles as <see cref="Rpt07"/>).</summary>
    /// <param name="search">Filters from the query string</param>
    /// <param name="format">xlsx or pdf</param>
    [HttpGet("rpt-07/export")]
    [Authorize(Roles = AdminAndManager)]
    public async Task<IActionResult> Rpt07Export([FromQuery] AccountStatisticsSearchVm search, [FromQuery] string? format)
    {
        if (!ReportExportService.TryParseFormat(format, out var exportFormat))
        {
            return ExportRefused(nameof(Rpt07), ReportExportService.UnknownFormatMessage);
        }
        var result = await _accountReports.GetAccountStatisticsAsync(search);
        return ExportFile(_export.BuildRpt07(result, search), exportFormat);
    }

    /// <summary>Name of the chosen exhibition as shown on the page (null = all exhibitions or unknown id).</summary>
    /// <param name="exhibitionId">Chosen exhibition</param>
    private async Task<string?> ExhibitionNameAsync(long? exhibitionId)
        => exhibitionId == null
            ? null
            : (await _entries.GetExhibitionOptionsAsync()).FirstOrDefault(x => x.Id == exhibitionId)?.Name;

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
