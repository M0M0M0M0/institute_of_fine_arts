using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Infrastructure.Security;
using InstituteOfFineArts.Web.Services.Interfaces;
using InstituteOfFineArts.Web.ViewModels.Common;
using InstituteOfFineArts.Web.ViewModels.Staff.Awards;
using Microsoft.AspNetCore.Mvc;

namespace InstituteOfFineArts.Web.Areas.Staff.Controllers;

/// <summary>
/// Staff – awards given to submissions (F-M08-01..04, SCR-28, task BE1-11):
/// GET /staff/awards (list + search) · GET/POST /staff/awards/new?competitionId= (issue)
/// GET/POST /staff/awards/{id}/edit · POST /staff/awards/{id}/delete (revoke with a reason, BR-33).
/// </summary>
[Route("staff/awards")]
public class AwardsController : StaffBaseController
{
    /// <summary>Issued award service.</summary>
    private readonly IAwardIssuedService _awards;

    /// <summary>Receives the issued award service through Dependency Injection.</summary>
    public AwardsController(IAwardIssuedService awards)
    {
        _awards = awards;
    }

    /// <summary>List with filters (competition, student, award, issued date range) and paging.</summary>
    /// <param name="search">Filters from the query string</param>
    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] AwardIssuedSearchVm search)
    {
        ViewBag.Search = search;
        ViewBag.Competitions = await _awards.GetCompetitionOptionsAsync();
        try
        {
            return View(await _awards.SearchAsync(search));
        }
        catch (BusinessException ex)
        {
            // Invalid filter (e.g. From after To): show the message and an empty list
            TempData["Error"] = ex.Message;
            return View(new PagedResult<AwardIssuedListItemVm> { Page = 1, PageSize = 10 });
        }
    }

    /// <summary>Issue form: choose a CLOSED / GRADED competition first, then its award and an eligible entry.</summary>
    /// <param name="competitionId">Chosen competition (optional)</param>
    /// <param name="competitionAwardId">Award to pre-select (optional, from the competition details page)</param>
    [HttpGet("new")]
    public async Task<IActionResult> Create(long? competitionId, long? competitionAwardId)
    {
        try
        {
            return View(await _awards.GetForCreateAsync(competitionId, competitionAwardId));
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
        catch (BusinessException ex)
        {
            // Competition not closed yet (TC-M08-02) → choose another one, with the reason
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Create), new { competitionId = (long?)null });
        }
    }

    /// <summary>Issues the award, then opens the competition details where the winner is listed.</summary>
    /// <param name="form">Form data</param>
    [HttpPost("new")]
    public async Task<IActionResult> Create(AwardIssuedFormVm form)
    {
        form.Id = 0;
        try
        {
            if (ModelState.IsValid)
            {
                await _awards.CreateAsync(form, User.GetProfileId());
                TempData["Success"] = "Award issued.";
                return RedirectToAction(nameof(CompetitionsController.Details), "Competitions", new { id = form.CompetitionId });
            }
        }
        catch (BusinessException ex)
        {
            // Field error next to the field; a general one (competition not closed) above the form
            ModelState.AddModelError(ex.Field ?? string.Empty, ex.Message);
        }

        // Show the form again: options come from the database, the typed values from the browser
        await _awards.FillOptionsAsync(form);
        return View(form);
    }

    /// <summary>Edit form with the current values.</summary>
    /// <param name="id">award_issued.id</param>
    [HttpGet("{id:long}/edit")]
    public async Task<IActionResult> Edit(long id)
    {
        try
        {
            return View(await _awards.GetForEditAsync(id));
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>Saves the changes with the same rules as issuing, then opens the competition details.</summary>
    /// <param name="id">award_issued.id</param>
    /// <param name="form">Form data</param>
    [HttpPost("{id:long}/edit")]
    public async Task<IActionResult> Edit(long id, AwardIssuedFormVm form)
    {
        form.Id = id;
        try
        {
            if (ModelState.IsValid)
            {
                var competitionId = await _awards.UpdateAsync(id, form);
                TempData["Success"] = "Award updated.";
                return RedirectToAction(nameof(CompetitionsController.Details), "Competitions", new { id = competitionId });
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

        // Show the form again with the display data and options from the database
        try
        {
            await _awards.FillOptionsAsync(form);
            return View(form);
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>Revokes the award; the reason is required and logged (BR-33). POST only, with anti-forgery token.</summary>
    /// <param name="id">award_issued.id</param>
    /// <param name="revoke">Reason typed on the edit page</param>
    [HttpPost("{id:long}/delete")]
    public async Task<IActionResult> Delete(long id, RevokeAwardVm revoke)
    {
        try
        {
            var competitionId = await _awards.RevokeAsync(id, revoke.Reason, User.GetProfileId());
            TempData["Success"] = "Award revoked.";
            return RedirectToAction(nameof(CompetitionsController.Details), "Competitions", new { id = competitionId });
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
        catch (BusinessException ex)
        {
            // Missing reason → back to the edit page with the message
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Edit), new { id });
        }
    }
}
