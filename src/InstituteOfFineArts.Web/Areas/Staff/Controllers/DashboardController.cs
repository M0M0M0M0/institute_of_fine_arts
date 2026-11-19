using InstituteOfFineArts.Web.Services.Interfaces;
using InstituteOfFineArts.Web.ViewModels.Staff;
using Microsoft.AspNetCore.Mvc;

namespace InstituteOfFineArts.Web.Areas.Staff.Controllers;

/// <summary>
/// Home page of the Staff area (/staff) – where the user lands after logging in. Staff run the institute, so this is
/// the overview of the former Manager area (task BE2-13; F-M11-01, US-15 AC3, SCR-50): the work waiting ("Needs
/// attention"), key figures with a period filter and the charts.
/// </summary>
public class DashboardController : StaffBaseController
{
    /// <summary>Dashboard service.</summary>
    private readonly IStaffDashboardService _dashboard;

    /// <summary>Receives the dashboard service through Dependency Injection.</summary>
    public DashboardController(IStaffDashboardService dashboard)
    {
        _dashboard = dashboard;
    }

    /// <summary>Shows the dashboard; ?period=month|year|all chooses the range of the activity figures (default all).</summary>
    public async Task<IActionResult> Index([FromQuery] DashboardPeriod period = DashboardPeriod.All)
    {
        if (!ModelState.IsValid)
        {
            period = DashboardPeriod.All;
        }
        return View(await _dashboard.GetAsync(period: period));
    }
}
