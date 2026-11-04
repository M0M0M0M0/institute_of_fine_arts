using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Services.Interfaces;
using InstituteOfFineArts.Web.ViewModels.Staff.Competitions;
using Microsoft.AspNetCore.Mvc;

namespace InstituteOfFineArts.Web.Areas.Staff.Controllers;

/// <summary>
/// Staff – defined awards of a competition (F-M05-06, task BE1-06), opened from the competition details page:
/// GET/POST /staff/competitions/{competitionId}/awards/new · GET/POST /staff/competitions/{competitionId}/awards/{id}/edit
/// POST /staff/competitions/{competitionId}/awards/{id}/delete. After every change the user goes back to the details page.
/// </summary>
[Route("staff/competitions/{competitionId:long}/awards")]
public class CompetitionAwardsController : StaffBaseController
{
    /// <summary>Award service.</summary>
    private readonly ICompetitionAwardService _awards;

    /// <summary>Receives the award service through Dependency Injection.</summary>
    public CompetitionAwardsController(ICompetitionAwardService awards)
    {
        _awards = awards;
    }

    /// <summary>Empty add form (redirects back with the reason when the competition is GRADED).</summary>
    /// <param name="competitionId">Competition id</param>
    [HttpGet("new")]
    public async Task<IActionResult> Create(long competitionId)
    {
        try
        {
            return View(await _awards.GetForCreateAsync(competitionId));
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
        catch (BusinessException ex)
        {
            return BackToDetails(competitionId, ex.Message);
        }
    }

    /// <summary>Adds the award, then goes back to the competition details.</summary>
    /// <param name="competitionId">Competition id</param>
    /// <param name="form">Form data</param>
    [HttpPost("new")]
    public async Task<IActionResult> Create(long competitionId, CompetitionAwardFormVm form)
    {
        try
        {
            if (ModelState.IsValid)
            {
                await _awards.CreateAsync(competitionId, form);
                TempData["Success"] = $"Award \"{form.AwardName.Trim()}\" added.";
                return BackToDetails(competitionId);
            }
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
        catch (BusinessException ex) when (ex.Field != null)
        {
            // Error on a field → show the form again with the message next to the field
            ModelState.AddModelError(ex.Field, ex.Message);
        }
        catch (BusinessException ex)
        {
            // General error (GRADED) → back to the details page with the reason
            return BackToDetails(competitionId, ex.Message);
        }

        // Show the form again; competition data comes from the database, not from the browser
        try
        {
            var current = await _awards.GetForCreateAsync(competitionId);
            form.Id = 0;
            form.CompetitionId = competitionId;
            form.CompetitionTitle = current.CompetitionTitle;
            form.CompetitionCode = current.CompetitionCode;
            return View(form);
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
        catch (BusinessException ex)
        {
            return BackToDetails(competitionId, ex.Message);
        }
    }

    /// <summary>Edit form with the current values.</summary>
    /// <param name="competitionId">Competition id</param>
    /// <param name="id">Award id (an award of another competition → 404)</param>
    [HttpGet("{id:long}/edit")]
    public async Task<IActionResult> Edit(long competitionId, long id)
    {
        try
        {
            return View(await _awards.GetForEditAsync(competitionId, id));
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
        catch (BusinessException ex)
        {
            return BackToDetails(competitionId, ex.Message);
        }
    }

    /// <summary>Saves the award, then goes back to the competition details.</summary>
    /// <param name="competitionId">Competition id</param>
    /// <param name="id">Award id</param>
    /// <param name="form">Form data</param>
    [HttpPost("{id:long}/edit")]
    public async Task<IActionResult> Edit(long competitionId, long id, CompetitionAwardFormVm form)
    {
        try
        {
            if (ModelState.IsValid)
            {
                await _awards.UpdateAsync(competitionId, id, form);
                TempData["Success"] = "Award updated.";
                return BackToDetails(competitionId);
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
            return BackToDetails(competitionId, ex.Message);
        }

        // Show the form again with the display data from the database
        try
        {
            var current = await _awards.GetForEditAsync(competitionId, id);
            form.Id = id;
            form.CompetitionId = competitionId;
            form.CompetitionTitle = current.CompetitionTitle;
            form.CompetitionCode = current.CompetitionCode;
            form.IssuedCount = current.IssuedCount;
            return View(form);
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
        catch (BusinessException ex)
        {
            return BackToDetails(competitionId, ex.Message);
        }
    }

    /// <summary>Deletes an award that has never been issued. POST only, with anti-forgery token.</summary>
    /// <param name="competitionId">Competition id</param>
    /// <param name="id">Award id</param>
    [HttpPost("{id:long}/delete")]
    public async Task<IActionResult> Delete(long competitionId, long id)
    {
        try
        {
            await _awards.DeleteAsync(competitionId, id);
            TempData["Success"] = "Award deleted.";
            return BackToDetails(competitionId);
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
        catch (BusinessException ex)
        {
            return BackToDetails(competitionId, ex.Message);
        }
    }

    /// <summary>Redirects to the competition details page, optionally with an error message.</summary>
    /// <param name="competitionId">Competition id</param>
    /// <param name="error">Error to show there (null = none)</param>
    private IActionResult BackToDetails(long competitionId, string? error = null)
    {
        if (error != null)
        {
            TempData["Error"] = error;
        }
        return RedirectToAction(nameof(CompetitionsController.Details), "Competitions", new { id = competitionId });
    }
}
