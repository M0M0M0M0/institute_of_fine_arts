using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Infrastructure.Audit;
using InstituteOfFineArts.Web.Services.Interfaces;
using InstituteOfFineArts.Web.ViewModels.Admin.Grading;
using Microsoft.AspNetCore.Mvc;

namespace InstituteOfFineArts.Web.Areas.Admin.Controllers;

/// <summary>
/// Administrator – grading library (decided 16/11/2026): GET /admin/grading (criteria + thresholds),
/// GET/POST /admin/grading/criteria/new, GET/POST /admin/grading/criteria/{id}/edit,
/// POST /admin/grading/criteria/{id}/delete (deletes an unused criterion, hides a used one),
/// POST /admin/grading/thresholds.
/// </summary>
[Route("admin/grading")]
public class GradingController : AdminBaseController
{
    /// <summary>Library service.</summary>
    private readonly IGradingCriterionService _grading;

    /// <summary>Receives the service through Dependency Injection.</summary>
    public GradingController(IGradingCriterionService grading)
    {
        _grading = grading;
    }

    /// <summary>Criteria and thresholds.</summary>
    [HttpGet("")]
    public async Task<IActionResult> Index() => View(await _grading.GetLibraryAsync());

    /// <summary>New criterion form.</summary>
    [HttpGet("criteria/new")]
    public IActionResult Create() => View("Form", new CriterionFormVm());

    /// <summary>Creates a criterion.</summary>
    [HttpPost("criteria/new")]
    public Task<IActionResult> Create(CriterionFormVm form) => SaveAsync(form, 0);

    /// <summary>Edit form.</summary>
    [HttpGet("criteria/{id:long}/edit")]
    public async Task<IActionResult> Edit(long id)
    {
        try
        {
            return View("Form", await _grading.GetForEditAsync(id));
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>Saves a criterion.</summary>
    [HttpPost("criteria/{id:long}/edit")]
    public Task<IActionResult> Edit(long id, CriterionFormVm form) => SaveAsync(form, id);

    /// <summary>Deletes (unused) or hides (used) a criterion.</summary>
    [HttpPost("criteria/{id:long}/delete")]
    public async Task<IActionResult> Delete(long id)
    {
        try
        {
            TempData["Success"] = await _grading.DeleteOrHideAsync(id)
                ? "Criterion deleted."
                : "This criterion is used by a competition, so it was hidden instead of deleted.";
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Saves the rating thresholds.</summary>
    [HttpPost("thresholds")]
    public async Task<IActionResult> Thresholds([Bind(Prefix = nameof(GradingLibraryVm.Thresholds))] ThresholdsFormVm form)
    {
        try
        {
            HttpContext.Items[AuditItems.Changes] = await _grading.SaveThresholdsAsync(form);
            TempData["Success"] = "Rating thresholds saved. Published results keep their rating.";
        }
        catch (BusinessException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Validates and saves a criterion form.</summary>
    private async Task<IActionResult> SaveAsync(CriterionFormVm form, long id)
    {
        form.Id = id;
        if (!ModelState.IsValid)
        {
            return View("Form", form);
        }
        try
        {
            await _grading.SaveAsync(form);
            TempData["Success"] = id == 0 ? "Criterion added." : "Criterion saved.";
            return RedirectToAction(nameof(Index));
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
        catch (BusinessException ex)
        {
            ModelState.AddModelError(ex.Field ?? string.Empty, ex.Message);
            return View("Form", form);
        }
    }
}
