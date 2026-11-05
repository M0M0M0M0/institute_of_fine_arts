using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Helpers;
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

    /// <summary>Receives the services through Dependency Injection.</summary>
    public FinanceReportsController(IExhibitionReportService exhibitionReports, IAccountReportService accountReports,
        IExhibitionEntryService entries)
    {
        _exhibitionReports = exhibitionReports;
        _accountReports = accountReports;
        _entries = entries;
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
}
