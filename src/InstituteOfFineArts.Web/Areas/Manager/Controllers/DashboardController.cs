using InstituteOfFineArts.Web.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace InstituteOfFineArts.Web.Areas.Manager.Controllers;

/// <summary>
/// Home page of the Manager area (/manager) – where the user lands after logging in.
/// Shows the dashboard numbers (task BE2-13; F-M11-01, US-15 AC3, SCR-50): students, staff, competitions by status,
/// submissions, awards issued, paintings sold and total sales. Read only.
/// </summary>
public class DashboardController : ManagerBaseController
{
    /// <summary>Dashboard service.</summary>
    private readonly IManagerDashboardService _dashboard;

    /// <summary>Receives the dashboard service through Dependency Injection.</summary>
    public DashboardController(IManagerDashboardService dashboard)
    {
        _dashboard = dashboard;
    }

    /// <summary>Shows the dashboard.</summary>
    public async Task<IActionResult> Index()
    {
        return View(await _dashboard.GetAsync());
    }
}
