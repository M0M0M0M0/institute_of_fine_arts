using InstituteOfFineArts.Web.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace InstituteOfFineArts.Web.Areas.Admin.Controllers;

/// <summary>
/// Home page of the Administrator area (/admin) – where the user lands after logging in.
/// Shows the dashboard numbers (task BE2-04): accounts, students, competitions, submissions, exhibitions, recent logins.
/// </summary>
public class DashboardController : AdminBaseController
{
    /// <summary>Dashboard service.</summary>
    private readonly IAdminDashboardService _dashboard;

    /// <summary>Receives the dashboard service through Dependency Injection.</summary>
    public DashboardController(IAdminDashboardService dashboard)
    {
        _dashboard = dashboard;
    }

    /// <summary>Shows the dashboard.</summary>
    public async Task<IActionResult> Index()
    {
        return View(await _dashboard.GetAsync());
    }
}
