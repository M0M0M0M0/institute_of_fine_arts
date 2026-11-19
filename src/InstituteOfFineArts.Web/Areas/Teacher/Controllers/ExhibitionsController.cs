using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Services.Interfaces;
using InstituteOfFineArts.Web.ViewModels.Staff.Exhibitions;
using Microsoft.AspNetCore.Mvc;

namespace InstituteOfFineArts.Web.Areas.Teacher.Controllers;

/// <summary>
/// Teacher – exhibitions, read only: GET /teacher/exhibitions (list + search) · GET /teacher/exhibitions/{id}.
/// Same shared views as the Staff area with ViewData["ReadOnly"] = true (no create / edit / delete buttons).
/// </summary>
[Route("teacher/exhibitions")]
public class ExhibitionsController : TeacherBaseController
{
    /// <summary>Exhibition service.</summary>
    private readonly IExhibitionService _exhibitions;

    /// <summary>Receives the exhibition service through Dependency Injection.</summary>
    public ExhibitionsController(IExhibitionService exhibitions)
    {
        _exhibitions = exhibitions;
    }

    /// <summary>List with filters and paging.</summary>
    /// <param name="search">Filters from the query string</param>
    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] ExhibitionSearchVm search)
    {
        ViewBag.Search = search;
        return View(await _exhibitions.SearchAsync(search));
    }

    /// <summary>Details of one exhibition.</summary>
    /// <param name="id">Exhibition id</param>
    [HttpGet("{id:long}")]
    public async Task<IActionResult> Details(long id)
    {
        try
        {
            return View(await _exhibitions.GetDetailsAsync(id));
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
    }
}
