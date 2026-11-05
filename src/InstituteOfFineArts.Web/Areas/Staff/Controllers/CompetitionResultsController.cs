using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Infrastructure.Security;
using InstituteOfFineArts.Web.Services.Interfaces;
using InstituteOfFineArts.Web.ViewModels.Staff.Competitions;
using Microsoft.AspNetCore.Mvc;

namespace InstituteOfFineArts.Web.Areas.Staff.Controllers;

/// <summary>
/// Staff – publish the results of a competition (F-M05-08, UC-06 step 5, task BE1-11), from the details page:
/// POST /staff/competitions/{id}/publish. The user always goes back to the details page with the outcome.
/// </summary>
[Route("staff/competitions/{id:long}")]
public class CompetitionResultsController : StaffBaseController
{
    /// <summary>Result publishing service.</summary>
    private readonly ICompetitionResultService _results;

    /// <summary>Receives the result service through Dependency Injection.</summary>
    public CompetitionResultsController(ICompetitionResultService results)
    {
        _results = results;
    }

    /// <summary>
    /// CLOSED → GRADED when every entry is evaluated (BR-08); otherwise the reason, e.g.
    /// "2 submissions have not been graded yet." (US-10 AC1). POST only, with anti-forgery token.
    /// </summary>
    /// <param name="id">Competition id</param>
    /// <param name="form">Optional result remarks</param>
    [HttpPost("publish")]
    public async Task<IActionResult> Publish(long id, PublishResultsVm form)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                // Remarks too long → the first message, back on the details page
                TempData["Error"] = ModelState.Values.SelectMany(v => v.Errors).First().ErrorMessage;
            }
            else
            {
                var awardCount = await _results.PublishAsync(id, form.ResultRemarks, User.GetProfileId());
                TempData["Success"] = "Results published. The competition is now GRADED.";
                if (awardCount == 0)
                {
                    // Allowed (BR-08 does not require awards), but staff may have forgotten to issue them
                    TempData["Warning"] = "No award has been issued in this competition yet.";
                }
            }
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
        catch (BusinessException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(CompetitionsController.Details), "Competitions", new { id });
    }
}
