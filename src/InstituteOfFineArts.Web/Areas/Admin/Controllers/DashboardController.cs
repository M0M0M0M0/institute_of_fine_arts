using Microsoft.AspNetCore.Mvc;

namespace InstituteOfFineArts.Web.Areas.Admin.Controllers;

/// <summary>
/// Home page of the Administrator area (/admin) – where the user lands after logging in.
/// Temporary page for now; the real content comes with the tasks for staff and student management (BE2-02, BE2-03).
/// </summary>
public class DashboardController : AdminBaseController
{
    /// <summary>Shows the home page of the area.</summary>
    public IActionResult Index()
    {
        return View();
    }
}
