using InstituteOfFineArts.Web.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InstituteOfFineArts.Web.Areas.Admin.Controllers;

/// <summary>
/// Base controller of the Administrator area: every controller in the Admin area derives from this class,
/// so only users with role ADMIN can access it (wrong role → /error/403).
/// </summary>
/// <remarks>
/// Pages behind the login are never cached by the browser: after logging out, the Back button
/// cannot show the data again (TC-M01-08).
/// </remarks>
[Area("Admin")]
[Authorize(Roles = RoleNames.Admin)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public abstract class AdminBaseController : Controller
{
}
