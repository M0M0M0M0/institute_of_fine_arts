using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Services.Interfaces;
using InstituteOfFineArts.Web.ViewModels.Admin.AuditLog;
using InstituteOfFineArts.Web.ViewModels.Common;
using Microsoft.AspNetCore.Mvc;

namespace InstituteOfFineArts.Web.Areas.Admin.Controllers;

/// <summary>
/// Admin – audit log, read only (F-M17-01, task BE1-OPT): GET /admin/audit-log with filters user, action,
/// date range and paging, newest first.
/// </summary>
[Route("admin/audit-log")]
public class AuditLogController : AdminBaseController
{
    /// <summary>Audit log service.</summary>
    private readonly IAuditLogService _audit;

    /// <summary>Receives the audit log service through Dependency Injection.</summary>
    public AuditLogController(IAuditLogService audit)
    {
        _audit = audit;
    }

    /// <summary>List with filters and paging.</summary>
    /// <param name="search">Filters from the query string</param>
    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] AuditLogSearchVm search)
    {
        ViewBag.Search = search;
        try
        {
            return View(await _audit.SearchAsync(search));
        }
        catch (BusinessException ex)
        {
            // Invalid filter (From after To): show the message and an empty list
            TempData["Error"] = ex.Message;
            return View(new PagedResult<AuditLogListItemVm> { Page = 1, PageSize = 10 });
        }
    }
}
