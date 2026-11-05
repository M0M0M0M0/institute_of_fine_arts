using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Infrastructure.Security;
using InstituteOfFineArts.Web.Services.Interfaces;
using InstituteOfFineArts.Web.ViewModels.Staff.Exhibitions;
using Microsoft.AspNetCore.Mvc;

namespace InstituteOfFineArts.Web.Areas.Student.Controllers;

/// <summary>
/// Student – exhibitions (task BE2-09; F-M09-02, F-M09-03, screen SCR-48):
/// GET /student/exhibitions (list + search, upcoming first) · GET /student/exhibitions/{id} (details, own entries,
/// "Propose an artwork" link while UPCOMING → /student/exhibition-entries/new?exhibitionId={id}, built by BE1-12).
/// </summary>
[Route("student/exhibitions")]
public class ExhibitionsController : StudentBaseController
{
    /// <summary>Student exhibition queries.</summary>
    private readonly IStudentExhibitionService _exhibitions;

    /// <summary>Receives the service through Dependency Injection.</summary>
    public ExhibitionsController(IStudentExhibitionService exhibitions)
    {
        _exhibitions = exhibitions;
    }

    /// <summary>List with filters (name, city, status, date range) and paging; upcoming exhibitions first.</summary>
    /// <param name="search">Filters from the query string</param>
    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] ExhibitionSearchVm search)
    {
        ViewBag.Search = search;
        return View(await _exhibitions.SearchAsync(search, User.GetProfileId()));
    }

    /// <summary>Details of one exhibition with the student's own entries.</summary>
    /// <param name="id">Exhibition id</param>
    [HttpGet("{id:long}")]
    public async Task<IActionResult> Details(long id)
    {
        try
        {
            return View(await _exhibitions.GetDetailsAsync(id, User.GetProfileId()));
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
    }
}
