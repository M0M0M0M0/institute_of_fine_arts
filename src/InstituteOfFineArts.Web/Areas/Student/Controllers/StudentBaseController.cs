using InstituteOfFineArts.Web.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InstituteOfFineArts.Web.Areas.Student.Controllers;

/// <summary>
/// Base controller of the Student area: every controller in the Student area derives from this class,
/// so only users with role STUDENT can access it (wrong role → /error/403).
/// </summary>
/// <remarks>
/// Pages behind the login are never cached by the browser: after logging out, the Back button
/// cannot show the data again (TC-M01-08).
/// </remarks>
[Area("Student")]
[Authorize(Roles = RoleNames.Student)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public abstract class StudentBaseController : Controller
{
}
