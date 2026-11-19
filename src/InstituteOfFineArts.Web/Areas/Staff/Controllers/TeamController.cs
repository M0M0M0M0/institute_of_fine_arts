using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Infrastructure.Security;
using InstituteOfFineArts.Web.Services.Interfaces;
using InstituteOfFineArts.Web.ViewModels.Admin.Staff;
using Microsoft.AspNetCore.Mvc;

namespace InstituteOfFineArts.Web.Areas.Staff.Controllers;

/// <summary>
/// Staff – the institute team (staff, teachers, administrators), read only (task BE2-13; F-M11-02, SCR-51, TC-M11-01):
/// GET /staff/team (list + search) · GET /staff/team/{id} (details). Useful to find the teachers of a jury.
/// No create / edit / lock / delete actions: those stay in the Admin area (POSTs there return 403 for STAFF).
/// </summary>
[Route("staff/team")]
public class TeamController : StaffBaseController
{
    /// <summary>Staff service.</summary>
    private readonly IStaffService _staff;

    /// <summary>Receives the staff service through Dependency Injection.</summary>
    public TeamController(IStaffService staff)
    {
        _staff = staff;
    }

    /// <summary>List with filters (keyword, role, subject, class, status) and paging.</summary>
    /// <param name="search">Filters from the query string</param>
    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] StaffSearchVm search)
    {
        ViewBag.Search = search;
        return View(await _staff.SearchAsync(search));
    }

    /// <summary>Details: login, personal and job information plus related data counts.</summary>
    /// <param name="id">staff_profile.id</param>
    [HttpGet("{id:long}")]
    public async Task<IActionResult> Details(long id)
    {
        try
        {
            return View(await _staff.GetDetailsAsync(id, User.GetAccountId()));
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
    }
}
