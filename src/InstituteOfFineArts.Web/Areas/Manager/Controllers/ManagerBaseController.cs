using InstituteOfFineArts.Web.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InstituteOfFineArts.Web.Areas.Manager.Controllers;

/// <summary>
/// Base controller of the Manager area: every controller in the Manager area derives from this class,
/// so only users with role MANAGER can access it (wrong role → /error/403).
/// </summary>
/// <remarks>
/// Pages behind the login are never cached by the browser: after logging out, the Back button
/// cannot show the data again (TC-M01-08).
/// </remarks>
[Area("Manager")]
[Authorize(Roles = RoleNames.Manager)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public abstract class ManagerBaseController : Controller
{
}
