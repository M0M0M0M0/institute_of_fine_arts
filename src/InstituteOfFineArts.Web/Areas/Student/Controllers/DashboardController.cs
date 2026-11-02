using Microsoft.AspNetCore.Mvc;

namespace InstituteOfFineArts.Web.Areas.Student.Controllers;

/// <summary>
/// Home page of the Student area (/student) – where the user lands after logging in.
/// Temporary page for now; the real content comes with the tasks for competitions, submissions, results.
/// </summary>
public class DashboardController : StudentBaseController
{
    /// <summary>Shows the home page of the area.</summary>
    public IActionResult Index()
    {
        return View();
    }
}
