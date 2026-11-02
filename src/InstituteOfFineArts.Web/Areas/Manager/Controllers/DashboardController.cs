using Microsoft.AspNetCore.Mvc;

namespace InstituteOfFineArts.Web.Areas.Manager.Controllers;

/// <summary>
/// Home page of the Manager area (/manager) – where the user lands after logging in.
/// Temporary page for now; the real content comes with the tasks for overview pages, exhibition management (BE2-13).
/// </summary>
public class DashboardController : ManagerBaseController
{
    /// <summary>Shows the home page of the area.</summary>
    public IActionResult Index()
    {
        return View();
    }
}
