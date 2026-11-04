using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Infrastructure.Security;
using InstituteOfFineArts.Web.Services.Interfaces;
using InstituteOfFineArts.Web.ViewModels.Staff.Evaluations;
using Microsoft.AspNetCore.Mvc;

namespace InstituteOfFineArts.Web.Areas.Staff.Controllers;

/// <summary>
/// Staff – evaluate one submission (F-M07-03..05, F-M07-08, SCR-27, task BE1-10), opened from the submission pages:
/// GET/POST /staff/evaluations/{submissionId} (create the first time, edit afterwards – BR-22)
/// POST /staff/evaluations/{submissionId}/delete. Students and other roles get 403 from the area (TC-M07-07).
/// </summary>
[Route("staff/evaluations")]
public class EvaluationsController : StaffBaseController
{
    /// <summary>Evaluation service.</summary>
    private readonly IEvaluationService _evaluations;

    /// <summary>Receives the evaluation service through Dependency Injection.</summary>
    public EvaluationsController(IEvaluationService evaluations)
    {
        _evaluations = evaluations;
    }

    /// <summary>Entry summary and the evaluation form (read only with the reason when it cannot be saved).</summary>
    /// <param name="submissionId">Submission id</param>
    [HttpGet("{submissionId:long}")]
    public async Task<IActionResult> Index(long submissionId)
    {
        try
        {
            return View(await _evaluations.GetPageAsync(submissionId));
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>Saves the evaluation as the signed-in staff member, then shows the page again (Post/Redirect/Get).</summary>
    /// <param name="submissionId">Submission id</param>
    /// <param name="form">Form data (fields are named Form.Rating, Form.PositivePoints… in the page)</param>
    [HttpPost("{submissionId:long}")]
    public async Task<IActionResult> Index(long submissionId, [Bind(Prefix = nameof(EvaluationPageVm.Form))] EvaluationFormVm form)
    {
        try
        {
            if (ModelState.IsValid)
            {
                var created = await _evaluations.SaveAsync(submissionId, User.GetProfileId(), form);
                TempData["Success"] = created ? "Evaluation saved." : "Evaluation updated.";
                return RedirectToAction(nameof(Index), new { submissionId });
            }
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
        catch (BusinessException ex) when (ex.Field != null)
        {
            // Error on a field → show the form again with the message next to the field
            ModelState.AddModelError($"{nameof(EvaluationPageVm.Form)}.{ex.Field}", ex.Message);
        }
        catch (BusinessException ex)
        {
            // General error (not CLOSED, GRADED, saved by someone else) → back to the page with the reason
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Index), new { submissionId });
        }

        // Show the form again: entry data comes from the database, the typed values from the browser
        try
        {
            var page = await _evaluations.GetPageAsync(submissionId);
            page.Form = form;
            return View(page);
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>Deletes the evaluation (not GRADED, no award, not exhibited). POST only, with anti-forgery token.</summary>
    /// <param name="submissionId">Submission id</param>
    [HttpPost("{submissionId:long}/delete")]
    public async Task<IActionResult> Delete(long submissionId)
    {
        try
        {
            await _evaluations.DeleteAsync(submissionId);
            TempData["Success"] = "Evaluation deleted.";
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
        catch (BusinessException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Index), new { submissionId });
    }
}
