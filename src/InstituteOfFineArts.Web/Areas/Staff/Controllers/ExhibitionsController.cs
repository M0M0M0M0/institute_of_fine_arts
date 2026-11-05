using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Infrastructure.Security;
using InstituteOfFineArts.Web.Services.Interfaces;
using InstituteOfFineArts.Web.ViewModels.Staff.Exhibitions;
using Microsoft.AspNetCore.Mvc;

namespace InstituteOfFineArts.Web.Areas.Staff.Controllers;

/// <summary>
/// Staff – exhibition management (M09, task BE2-09; F-M09-01, F-M09-02, screen SCR-29):
/// GET /staff/exhibitions (list + search) · GET/POST /staff/exhibitions/new · GET /staff/exhibitions/{id}
/// GET/POST /staff/exhibitions/{id}/edit · POST /staff/exhibitions/{id}/delete.
/// The Manager area has the same pages (/manager/exhibitions) on the same service and shared views.
/// </summary>
[Route("staff/exhibitions")]
public class ExhibitionsController : StaffBaseController
{
    /// <summary>Exhibition service.</summary>
    private readonly IExhibitionService _exhibitions;

    /// <summary>Receives the exhibition service through Dependency Injection.</summary>
    public ExhibitionsController(IExhibitionService exhibitions)
    {
        _exhibitions = exhibitions;
    }

    /// <summary>List with filters (name, city, status, date range) and paging.</summary>
    /// <param name="search">Filters from the query string</param>
    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] ExhibitionSearchVm search)
    {
        ViewBag.Search = search;
        return View(await _exhibitions.SearchAsync(search));
    }

    /// <summary>Empty create form.</summary>
    [HttpGet("new")]
    public IActionResult Create() => View(new ExhibitionFormVm());

    /// <summary>Creates the exhibition, then opens its details.</summary>
    /// <param name="form">Form data</param>
    [HttpPost("new")]
    public async Task<IActionResult> Create(ExhibitionFormVm form)
    {
        if (!ModelState.IsValid)
        {
            return View(form);
        }
        try
        {
            var id = await _exhibitions.CreateAsync(form, User.GetProfileId());
            TempData["Success"] = "Exhibition created.";
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (BusinessException ex)
        {
            ModelState.AddModelError(ex.Field ?? string.Empty, ex.Message);
            return View(form);
        }
    }

    /// <summary>Details: information, status and entry counts.</summary>
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

    /// <summary>Edit form with the current values.</summary>
    /// <param name="id">Exhibition id</param>
    [HttpGet("{id:long}/edit")]
    public async Task<IActionResult> Edit(long id)
    {
        try
        {
            return View(await _exhibitions.GetForEditAsync(id));
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>Saves the changes (dates limited once artworks have been sent).</summary>
    /// <param name="id">Exhibition id</param>
    /// <param name="form">Form data</param>
    [HttpPost("{id:long}/edit")]
    public async Task<IActionResult> Edit(long id, ExhibitionFormVm form)
    {
        try
        {
            if (ModelState.IsValid)
            {
                await _exhibitions.UpdateAsync(id, form);
                TempData["Success"] = "Exhibition updated.";
                return RedirectToAction(nameof(Details), new { id });
            }
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
        catch (BusinessException ex)
        {
            ModelState.AddModelError(ex.Field ?? string.Empty, ex.Message);
        }

        // Show the form again; the lock flags come from the database, not from the browser
        try
        {
            var current = await _exhibitions.GetForEditAsync(id);
            form.Id = id;
            form.HasEntries = current.HasEntries;
            form.DatesLocked = current.DatesLocked;
            return View(form);
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>Deletes an exhibition without entries (BR-42). POST only, with anti-forgery token.</summary>
    /// <param name="id">Exhibition id</param>
    [HttpPost("{id:long}/delete")]
    public async Task<IActionResult> Delete(long id)
    {
        try
        {
            await _exhibitions.DeleteAsync(id);
            TempData["Success"] = "Exhibition deleted.";
            return RedirectToAction(nameof(Index));
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
        catch (BusinessException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Details), new { id });
        }
    }
}
