using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Infrastructure.Security;
using InstituteOfFineArts.Web.Services.Interfaces;
using InstituteOfFineArts.Web.ViewModels.Staff.Competitions;
using Microsoft.AspNetCore.Mvc;

namespace InstituteOfFineArts.Web.Areas.Staff.Controllers;

/// <summary>
/// Staff – competition management (M05, task BE1-05; screens SCR-23..25):
/// GET /staff/competitions (list + search) · GET/POST /staff/competitions/new · GET /staff/competitions/{id}
/// GET/POST /staff/competitions/{id}/edit · POST /staff/competitions/{id}/delete.
/// </summary>
[Route("staff/competitions")]
public class CompetitionsController : StaffBaseController
{
    /// <summary>Competition service.</summary>
    private readonly ICompetitionService _competitions;

    /// <summary>Receives the competition service through Dependency Injection.</summary>
    public CompetitionsController(ICompetitionService competitions)
    {
        _competitions = competitions;
    }

    /// <summary>List with filters (keyword, status, date range) and paging.</summary>
    /// <param name="search">Filters from the query string</param>
    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] CompetitionSearchVm search)
    {
        ViewBag.Search = search;
        return View(await _competitions.SearchAsync(search));
    }

    /// <summary>Empty create form.</summary>
    [HttpGet("new")]
    public IActionResult Create() => View(new CompetitionFormVm());

    /// <summary>Creates the competition, then opens its details (to add the awards next).</summary>
    /// <param name="form">Form data</param>
    [HttpPost("new")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> Create(CompetitionFormVm form)
    {
        if (!ModelState.IsValid)
        {
            return View(form);
        }
        try
        {
            var id = await _competitions.CreateAsync(form, User.GetProfileId());
            TempData["Success"] = "Competition created. Next, add its awards.";
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (BusinessException ex)
        {
            ModelState.AddModelError(ex.Field ?? string.Empty, ex.Message);
            return View(form);
        }
    }

    /// <summary>Details: information, rules, awards, number of entries, status.</summary>
    /// <param name="id">Competition id</param>
    [HttpGet("{id:long}")]
    public async Task<IActionResult> Details(long id)
    {
        try
        {
            return View(await _competitions.GetDetailsAsync(id));
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>Edit form with the current values (limited fields when entries exist).</summary>
    /// <param name="id">Competition id</param>
    [HttpGet("{id:long}/edit")]
    public async Task<IActionResult> Edit(long id)
    {
        try
        {
            return View(await _competitions.GetForEditAsync(id));
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
        catch (BusinessException ex)
        {
            // GRADED → read only: back to the details with the reason
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    /// <summary>Saves the changes allowed by BR-10.</summary>
    /// <param name="id">Competition id</param>
    /// <param name="form">Form data</param>
    [HttpPost("{id:long}/edit")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> Edit(long id, CompetitionFormVm form)
    {
        try
        {
            if (ModelState.IsValid)
            {
                await _competitions.UpdateAsync(id, form);
                TempData["Success"] = "Competition updated.";
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

        // Show the form again; code, cover and lock flags come from the database, not from the browser
        try
        {
            var current = await _competitions.GetForEditAsync(id);
            form.Id = id;
            form.Code = current.Code;
            form.HasCover = current.HasCover;
            form.HasSubmissions = current.HasSubmissions;
        }
        catch (BusinessException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Details), new { id });
        }
        return View(form);
    }

    /// <summary>Deletes a competition without entries (BR-11). POST only, with anti-forgery token.</summary>
    /// <param name="id">Competition id</param>
    [HttpPost("{id:long}/delete")]
    public async Task<IActionResult> Delete(long id)
    {
        try
        {
            await _competitions.DeleteAsync(id);
            TempData["Success"] = "Competition deleted.";
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
