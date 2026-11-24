using InstituteOfFineArts.Web.Infrastructure.Security;
using InstituteOfFineArts.Web.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace InstituteOfFineArts.Web.Areas.Teacher.Controllers;

/// <summary>
/// Home page of the Teacher area (/teacher) – where a teacher lands after logging in: the closed competitions with
/// their grading progress (entries left to grade first) and the competitions that close soon.
/// </summary>
public class DashboardController : TeacherBaseController
{
    /// <summary>Dashboard service.</summary>
    private readonly ITeacherDashboardService _dashboard;

    /// <summary>Receives the dashboard service through Dependency Injection.</summary>
    public DashboardController(ITeacherDashboardService dashboard)
    {
        _dashboard = dashboard;
    }

    /// <summary>Shows the home page of the area.</summary>
    public async Task<IActionResult> Index()
    {
        return View(await _dashboard.GetAsync(User.GetProfileId()));
    }
}
