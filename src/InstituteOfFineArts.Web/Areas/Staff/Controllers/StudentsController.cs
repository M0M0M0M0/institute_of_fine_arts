using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Services.Interfaces;
using InstituteOfFineArts.Web.ViewModels.Admin.Students;
using Microsoft.AspNetCore.Mvc;

namespace InstituteOfFineArts.Web.Areas.Staff.Controllers;

/// <summary>
/// Staff – students, read only (F-M04-06, task BE2-03; TC-M04-05):
/// GET /staff/students (list + search) · GET /staff/students/{id} (personal and study information).
/// No create / edit / delete / password actions: those stay in the Admin area.
/// </summary>
[Route("staff/students")]
public class StudentsController : StaffBaseController
{
    /// <summary>Student service.</summary>
    private readonly IStudentService _students;

    /// <summary>Receives the student service through Dependency Injection.</summary>
    public StudentsController(IStudentService students)
    {
        _students = students;
    }

    /// <summary>List with filters and paging.</summary>
    /// <param name="search">Filters from the query string</param>
    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] StudentSearchVm search)
    {
        ViewBag.Search = search;
        return View(await _students.SearchAsync(search));
    }

    /// <summary>Details: personal and study information plus related data counts.</summary>
    /// <param name="id">student_profile.id</param>
    [HttpGet("{id:long}")]
    public async Task<IActionResult> Details(long id)
    {
        try
        {
            return View(await _students.GetDetailsAsync(id));
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
    }
}
