using InstituteOfFineArts.Web.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InstituteOfFineArts.Web.Areas.Teacher.Controllers;

/// <summary>
/// Base controller of the Teacher area: every controller in the Teacher area derives from this class,
/// so only users with role TEACHER can access it (wrong role → /error/403).
/// Teachers are the subject experts: they grade the entries once a competition is closed and see the rest read only.
/// </summary>
/// <remarks>
/// Pages behind the login are never cached by the browser: after logging out, the Back button
/// cannot show the data again (TC-M01-08).
/// </remarks>
[Area("Teacher")]
[Authorize(Roles = RoleNames.Teacher)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public abstract class TeacherBaseController : Controller
{
}
