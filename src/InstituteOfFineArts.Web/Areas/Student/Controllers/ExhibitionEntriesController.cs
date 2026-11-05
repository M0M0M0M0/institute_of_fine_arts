using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Infrastructure.Security;
using InstituteOfFineArts.Web.Services.Interfaces;
using InstituteOfFineArts.Web.ViewModels.Student.ExhibitionEntries;
using Microsoft.AspNetCore.Mvc;

namespace InstituteOfFineArts.Web.Areas.Student.Controllers;

/// <summary>
/// Student – own exhibition entries (task BE1-12; F-M09-04, F-M09-10, F-M09-12; screen SCR-49):
/// GET /student/exhibition-entries (my entries) · GET/POST /student/exhibition-entries/new?exhibitionId={id}
/// GET /student/exhibition-entries/{id} · GET/POST /student/exhibition-entries/{id}/edit (asking price while PENDING)
/// POST /student/exhibition-entries/{id}/delete (withdraw while PENDING).
/// Other students' entries return 404 (not 403), so their ids are not revealed.
/// </summary>
[Route("student/exhibition-entries")]
public class ExhibitionEntriesController : StudentBaseController
{
    /// <summary>Exhibition entry service of the student.</summary>
    private readonly IStudentExhibitionEntryService _entries;

    /// <summary>Receives the entry service through Dependency Injection.</summary>
    public ExhibitionEntriesController(IStudentExhibitionEntryService entries)
    {
        _entries = entries;
    }

    /// <summary>My entries with filters (status, exhibition) and paging (US-13 AC5).</summary>
    /// <param name="search">Filters from the query string</param>
    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] MyEntrySearchVm search)
    {
        var me = User.GetProfileId();
        ViewBag.Search = search;
        ViewBag.Exhibitions = await _entries.GetMyExhibitionsAsync(me);
        return View(await _entries.SearchMineAsync(me, search));
    }

    /// <summary>Proposal form for an upcoming exhibition (opened from the exhibition page "Propose an artwork").</summary>
    /// <param name="exhibitionId">Exhibition id from the query string</param>
    [HttpGet("new")]
    public async Task<IActionResult> Create(long exhibitionId)
    {
        try
        {
            return View(await _entries.GetNewFormAsync(exhibitionId, User.GetProfileId()));
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
        catch (BusinessException ex)
        {
            // Exhibition already started or finished (BR-42): back to my entries with the reason
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>Creates the PENDING proposal, then opens its details.</summary>
    /// <param name="form">Form data (exhibitionId also comes from the query string)</param>
    [HttpPost("new")]
    public async Task<IActionResult> Create(ProposeEntryFormVm form)
    {
        var me = User.GetProfileId();
        try
        {
            if (ModelState.IsValid)
            {
                var id = await _entries.CreateAsync(form, me, User.GetAccountId());
                TempData["Success"] = "Your painting has been proposed. Staff will review it before the exhibition.";
                return RedirectToAction(nameof(Details), new { id });
            }
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
        catch (BusinessException ex) when (ex.Field != null)
        {
            // Painting not allowed or price invalid: show it next to the field
            ModelState.AddModelError(ex.Field, ex.Message);
        }
        catch (BusinessException ex)
        {
            // The exhibition is no longer upcoming: the form cannot be used any more
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Index));
        }

        // Show the form again; exhibition and paintings come from the database, not from the browser
        try
        {
            var current = await _entries.GetNewFormAsync(form.ExhibitionId, me);
            form.ExhibitionName = current.ExhibitionName;
            form.City = current.City;
            form.StartDate = current.StartDate;
            form.EndDate = current.EndDate;
            form.Candidates = current.Candidates;
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
        catch (BusinessException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Index));
        }
        return View(form);
    }

    /// <summary>Details of an own entry: exhibition, price, status, reject reason, sale and payment.</summary>
    /// <param name="id">Entry id</param>
    [HttpGet("{id:long}")]
    public async Task<IActionResult> Details(long id)
    {
        try
        {
            return View(await _entries.GetDetailsAsync(id, User.GetProfileId()));
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>Asking price form while the entry is PENDING (BR-46).</summary>
    /// <param name="id">Entry id</param>
    [HttpGet("{id:long}/edit")]
    public async Task<IActionResult> Edit(long id)
    {
        try
        {
            return View(await _entries.GetForEditAsync(id, User.GetProfileId()));
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
        catch (BusinessException ex)
        {
            // Already reviewed: back to the details with the reason
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    /// <summary>Saves the new asking price.</summary>
    /// <param name="id">Entry id</param>
    /// <param name="form">Form data</param>
    [HttpPost("{id:long}/edit")]
    public async Task<IActionResult> Edit(long id, EditPriceFormVm form)
    {
        var me = User.GetProfileId();
        try
        {
            if (ModelState.IsValid)
            {
                await _entries.UpdatePriceAsync(id, form, me);
                TempData["Success"] = "The asking price has been updated.";
                return RedirectToAction(nameof(Details), new { id });
            }
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
        catch (BusinessException ex) when (ex.Field != null)
        {
            ModelState.AddModelError(ex.Field, ex.Message);
        }
        catch (BusinessException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Details), new { id });
        }

        // Show the form again with the display data from the database
        try
        {
            var current = await _entries.GetForEditAsync(id, me);
            form.Id = id;
            form.Title = current.Title;
            form.ExhibitionName = current.ExhibitionName;
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
        return View(form);
    }

    /// <summary>Withdraws an own proposal while it is PENDING (F-M09-12). POST only, with anti-forgery token.</summary>
    /// <param name="id">Entry id</param>
    [HttpPost("{id:long}/delete")]
    public async Task<IActionResult> Delete(long id)
    {
        try
        {
            await _entries.WithdrawAsync(id, User.GetProfileId());
            TempData["Success"] = "Your proposal has been withdrawn.";
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
