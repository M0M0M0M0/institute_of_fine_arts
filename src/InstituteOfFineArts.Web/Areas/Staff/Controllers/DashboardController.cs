using Microsoft.AspNetCore.Mvc;

namespace InstituteOfFineArts.Web.Areas.Staff.Controllers;

/// <summary>
/// Home page of the Staff area (/staff) – where the user lands after logging in.
/// Temporary page for now; the real content comes with the tasks for competitions, grading, awards, exhibitions.
/// </summary>
public class DashboardController : StaffBaseController
{
    /// <summary>Shows the home page of the area.</summary>
    public IActionResult Index()
    {
        return View();
    }
}
