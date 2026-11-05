using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Infrastructure.Security;
using InstituteOfFineArts.Web.Services.Interfaces;
using InstituteOfFineArts.Web.ViewModels.Admin.Staff;
using Microsoft.AspNetCore.Mvc;

namespace InstituteOfFineArts.Web.Areas.Manager.Controllers;

/// <summary>
/// Manager – staff members, read only (task BE2-13; F-M11-02, SCR-51, TC-M11-01):
/// GET /manager/staff (list + search) · GET /manager/staff/{id} (details).
/// No create / edit / lock / delete actions: those stay in the Admin area (POSTs there return 403 for MANAGER).
/// </summary>
[Route("manager/staff")]
public class StaffController : ManagerBaseController
{
    /// <summary>Staff service.</summary>
    private readonly IStaffService _staff;

    /// <summary>Receives the staff service through Dependency Injection.</summary>
    public StaffController(IStaffService staff)
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
