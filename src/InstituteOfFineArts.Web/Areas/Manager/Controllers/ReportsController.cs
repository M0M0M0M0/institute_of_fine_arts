using Microsoft.AspNetCore.Mvc;

namespace InstituteOfFineArts.Web.Areas.Manager.Controllers;

/// <summary>
/// Manager – entry point to the reports (task BE2-13): GET /manager/reports redirects to the shared report list /reports
/// (BE2-12). Being in the Manager area, any other role gets 403 here (TC-M11-06).
/// </summary>
[Route("manager/reports")]
public class ReportsController : ManagerBaseController
{
    /// <summary>Redirects to the shared report list.</summary>
    [HttpGet("")]
    public IActionResult Index() => Redirect("/reports");
}
