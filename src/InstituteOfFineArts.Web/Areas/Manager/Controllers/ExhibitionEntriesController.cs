using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Services.Interfaces;
using InstituteOfFineArts.Web.ViewModels.Staff.ExhibitionEntries;
using Microsoft.AspNetCore.Mvc;

namespace InstituteOfFineArts.Web.Areas.Manager.Controllers;

/// <summary>
/// Manager – exhibition entries, read only (F-M09-11 = F-M11-07, task BE2-13; role matrix: Manager R,S on
/// ExhibitionEntry; SCR-54/55): GET /manager/exhibition-entries (list + filters, ?exhibitionId= from the exhibition
/// page) · GET /manager/exhibition-entries/{id} (details: painting, student, exhibition, prices, customer, payment).
/// No accept / reject / sold / paid actions: those stay in the Staff area (POST there → 403 for a manager).
/// </summary>
[Route("manager/exhibition-entries")]
public class ExhibitionEntriesController : ManagerBaseController
{
    /// <summary>Exhibition entry service (only its read methods are used here).</summary>
    private readonly IExhibitionEntryService _entries;

    /// <summary>Receives the entry service through Dependency Injection.</summary>
    public ExhibitionEntriesController(IExhibitionEntryService entries)
    {
        _entries = entries;
    }

    /// <summary>List with filters (exhibition, status, paid, student) and paging.</summary>
    /// <param name="search">Filters from the query string</param>
    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] EntrySearchVm search)
    {
        ViewBag.Search = search;
        ViewBag.Exhibitions = await _entries.GetExhibitionOptionsAsync();
        return View(await _entries.SearchAsync(search));
    }

    /// <summary>Details of one entry (no action forms).</summary>
    /// <param name="id">Entry id</param>
    [HttpGet("{id:long}")]
    public async Task<IActionResult> Details(long id)
    {
        try
        {
            return View(await _entries.GetDetailsAsync(id));
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
    }
}
