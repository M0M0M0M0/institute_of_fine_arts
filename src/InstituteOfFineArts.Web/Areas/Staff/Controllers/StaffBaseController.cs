using InstituteOfFineArts.Web.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InstituteOfFineArts.Web.Areas.Staff.Controllers;

/// <summary>
/// Base controller of the Staff area: every controller in the Staff area derives from this class,
/// so only users with role STAFF can access it (wrong role → /error/403).
/// </summary>
/// <remarks>
/// Pages behind the login are never cached by the browser: after logging out, the Back button
/// cannot show the data again (TC-M01-08).
/// </remarks>
[Area("Staff")]
[Authorize(Roles = RoleNames.Staff)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public abstract class StaffBaseController : Controller
{
}
