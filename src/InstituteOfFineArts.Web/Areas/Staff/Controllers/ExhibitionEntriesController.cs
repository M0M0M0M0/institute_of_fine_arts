using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Infrastructure.Security;
using InstituteOfFineArts.Web.Services.Interfaces;
using InstituteOfFineArts.Web.ViewModels.Staff.ExhibitionEntries;
using Microsoft.AspNetCore.Mvc;

namespace InstituteOfFineArts.Web.Areas.Staff.Controllers;

/// <summary>
/// Staff – exhibition entries (tasks BE1-12, BE1-13; F-M09-05, 06, 08, 09; screens SCR-30, SCR-31):
/// GET /staff/exhibition-entries (list + filters, ?exhibitionId= from the exhibition page) ·
/// GET/POST /staff/exhibition-entries/new?exhibitionId={id} (direct create) · GET /staff/exhibition-entries/{id} (details + actions)
/// POST /staff/exhibition-entries/{id}/accept | reject | sold | unsold | paid | delete.
/// Every action posts back to the details page (Post/Redirect/Get); a field error shows the page again with the
/// message next to the field.
/// </summary>
[Route("staff/exhibition-entries")]
public class ExhibitionEntriesController : StaffBaseController
{
    /// <summary>Exhibition entry service.</summary>
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

    /// <summary>Direct-create form for an upcoming exhibition (UC-07 3b).</summary>
    /// <param name="exhibitionId">Exhibition id from the query string</param>
    [HttpGet("new")]
    public async Task<IActionResult> Create(long exhibitionId)
    {
        try
        {
            return View(await _entries.GetNewFormAsync(exhibitionId));
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
        catch (BusinessException ex)
        {
            // Exhibition already started (BR-42): back to its entries with the reason
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Index), new { exhibitionId });
        }
    }

    /// <summary>Creates the ACCEPTED entry, then opens its details.</summary>
    /// <param name="form">Form data (exhibitionId also comes from the query string)</param>
    [HttpPost("new")]
    public async Task<IActionResult> Create(EntryCreateFormVm form)
    {
        try
        {
            if (ModelState.IsValid)
            {
                var id = await _entries.CreateAsync(form, User.GetProfileId(), User.GetAccountId());
                TempData["Success"] = "The painting has been added to the exhibition (accepted).";
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
            return RedirectToAction(nameof(Index), new { exhibitionId = form.ExhibitionId });
        }

        // Show the form again; exhibition and paintings come from the database, not from the browser
        try
        {
            var current = await _entries.GetNewFormAsync(form.ExhibitionId);
            form.ExhibitionName = current.ExhibitionName;
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
            return RedirectToAction(nameof(Index), new { exhibitionId = form.ExhibitionId });
        }
        return View(form);
    }

    /// <summary>Details with the action forms allowed in the current state.</summary>
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

    /// <summary>Accepts a pending proposal (PENDING → ACCEPTED).</summary>
    /// <param name="id">Entry id</param>
    [HttpPost("{id:long}/accept")]
    public Task<IActionResult> Accept(long id)
        => RunAsync(id, () => _entries.AcceptAsync(id, User.GetProfileId()), "The proposal has been accepted.");

    /// <summary>Rejects a pending proposal with a reason (PENDING → REJECTED).</summary>
    /// <param name="id">Entry id</param>
    /// <param name="form">Reason (fields named RejectForm.*)</param>
    [HttpPost("{id:long}/reject")]
    public async Task<IActionResult> Reject(long id, [Bind(Prefix = nameof(EntryDetailsVm.RejectForm))] RejectFormVm form)
    {
        if (!ModelState.IsValid)
        {
            return await DetailsAgainAsync(id, vm => vm.RejectForm = form);
        }
        return await RunAsync(id, () => _entries.RejectAsync(id, form, User.GetProfileId()), "The proposal has been rejected.",
            nameof(EntryDetailsVm.RejectForm), vm => vm.RejectForm = form);
    }

    /// <summary>Records the sale (ACCEPTED → SOLD).</summary>
    /// <param name="id">Entry id</param>
    /// <param name="form">Sold price, sale date, customer (fields named SoldForm.*)</param>
    [HttpPost("{id:long}/sold")]
    public async Task<IActionResult> Sold(long id, [Bind(Prefix = nameof(EntryDetailsVm.SoldForm))] SoldFormVm form)
    {
        if (!ModelState.IsValid)
        {
            return await DetailsAgainAsync(id, vm => vm.SoldForm = form);
        }
        return await RunAsync(id, () => _entries.MarkSoldAsync(id, form, User.GetProfileId()), "The sale has been recorded.",
            nameof(EntryDetailsVm.SoldForm), vm => vm.SoldForm = form);
    }

    /// <summary>Marks an accepted painting as unsold after the exhibition (ACCEPTED → UNSOLD).</summary>
    /// <param name="id">Entry id</param>
    [HttpPost("{id:long}/unsold")]
    public Task<IActionResult> Unsold(long id)
        => RunAsync(id, () => _entries.MarkUnsoldAsync(id, User.GetProfileId()), "The painting has been marked as unsold.");

    /// <summary>Records the payment of the sale money to the student.</summary>
    /// <param name="id">Entry id</param>
    /// <param name="form">Amount and payment date (fields named PaidForm.*)</param>
    [HttpPost("{id:long}/paid")]
    public async Task<IActionResult> Paid(long id, [Bind(Prefix = nameof(EntryDetailsVm.PaidForm))] PaidFormVm form)
    {
        if (!ModelState.IsValid)
        {
            return await DetailsAgainAsync(id, vm => vm.PaidForm = form);
        }
        return await RunAsync(id, () => _entries.MarkPaidAsync(id, form, User.GetProfileId()), "The payment to the student has been recorded.",
            nameof(EntryDetailsVm.PaidForm), vm => vm.PaidForm = form);
    }

    /// <summary>Deletes an entry before its exhibition starts, then goes back to the exhibition's entries.</summary>
    /// <param name="id">Entry id</param>
    [HttpPost("{id:long}/delete")]
    public async Task<IActionResult> Delete(long id)
    {
        try
        {
            var exhibitionId = await _entries.DeleteAsync(id);
            TempData["Success"] = "The entry has been deleted.";
            return RedirectToAction(nameof(Index), new { exhibitionId });
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

    /// <summary>
    /// Runs one state change and redirects to the details page with the result. A field error (when
    /// <paramref name="formPrefix"/> is given) shows the details page again with the posted values and the message
    /// next to the field; any other rule violation is shown as an error message after the redirect.
    /// </summary>
    /// <param name="id">Entry id</param>
    /// <param name="action">Service call</param>
    /// <param name="success">Success message</param>
    /// <param name="formPrefix">Name of the form on the details page (RejectForm, SoldForm, PaidForm) or null</param>
    /// <param name="keepForm">Puts the posted values back into the page model</param>
    private async Task<IActionResult> RunAsync(long id, Func<Task> action, string success,
        string? formPrefix = null, Action<EntryDetailsVm>? keepForm = null)
    {
        try
        {
            await action();
            TempData["Success"] = success;
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
        catch (BusinessException ex) when (ex.Field != null && formPrefix != null)
        {
            ModelState.AddModelError($"{formPrefix}.{ex.Field}", ex.Message);
            return await DetailsAgainAsync(id, keepForm);
        }
        catch (BusinessException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    /// <summary>Shows the details page again (after a field error) with the posted form values.</summary>
    /// <param name="id">Entry id</param>
    /// <param name="keepForm">Puts the posted values back into the page model</param>
    private async Task<IActionResult> DetailsAgainAsync(long id, Action<EntryDetailsVm>? keepForm)
    {
        try
        {
            var vm = await _entries.GetDetailsAsync(id);
            keepForm?.Invoke(vm);
            return View(nameof(Details), vm);
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
    }
}
