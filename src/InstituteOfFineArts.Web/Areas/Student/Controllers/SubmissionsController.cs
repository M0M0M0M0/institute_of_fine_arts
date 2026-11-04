using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Infrastructure.Security;
using InstituteOfFineArts.Web.Services.Interfaces;
using InstituteOfFineArts.Web.ViewModels.Student.Submissions;
using Microsoft.AspNetCore.Mvc;

namespace InstituteOfFineArts.Web.Areas.Student.Controllers;

/// <summary>
/// Student – own competition entries (M06, tasks BE1-07..BE1-09; screens SCR-43..SCR-45):
/// GET/POST /student/submissions/new?competitionId={id} · GET /student/submissions (my entries)
/// GET /student/submissions/{id} · GET/POST /student/submissions/{id}/edit · POST /student/submissions/{id}/delete.
/// Other students' entries return 404 (not 403), so their ids are not revealed.
/// </summary>
[Route("student/submissions")]
public class SubmissionsController : StudentBaseController
{
    /// <summary>Largest accepted request: a 5 MB image plus 1 MB for the other fields (BR-17).</summary>
    private const long MaxRequestBytes = 6 * 1024 * 1024;

    /// <summary>Submission service.</summary>
    private readonly ISubmissionService _submissions;

    /// <summary>Receives the submission service through Dependency Injection.</summary>
    public SubmissionsController(ISubmissionService submissions)
    {
        _submissions = submissions;
    }

    /// <summary>My entries with filters (competition, title, submitted date range) and paging.</summary>
    /// <param name="search">Filters from the query string</param>
    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] MySubmissionSearchVm search)
    {
        var me = User.GetProfileId();
        ViewBag.Search = search;
        ViewBag.Competitions = await _submissions.GetMyCompetitionsAsync(me);
        return View(await _submissions.SearchMineAsync(me, search));
    }

    /// <summary>Empty submit form for an ongoing competition the student has not entered yet.</summary>
    /// <param name="competitionId">Competition id from the query string</param>
    [HttpGet("new")]
    public async Task<IActionResult> Create(long competitionId)
    {
        try
        {
            return View(await _submissions.GetNewFormAsync(competitionId, User.GetProfileId()));
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
        catch (BusinessException ex)
        {
            // Not open or already submitted: back to the competition with the reason
            TempData["Error"] = ex.Message;
            return RedirectToCompetition(competitionId);
        }
    }

    /// <summary>Submits the entry, then opens its details.</summary>
    /// <param name="form">Form data (competitionId also comes from the query string)</param>
    [HttpPost("new")]
    [RequestSizeLimit(MaxRequestBytes)]
    public async Task<IActionResult> Create(SubmissionFormVm form)
    {
        var me = User.GetProfileId();
        try
        {
            if (ModelState.IsValid)
            {
                var result = await _submissions.CreateAsync(form, me);
                TempData["Success"] = "Your entry has been submitted.";
                if (result.Warning != null)
                {
                    // Small image: accepted, but the student is told it may look blurry (BR-17)
                    TempData["Warning"] = result.Warning;
                }
                return RedirectToAction(nameof(Details), new { id = result.Id });
            }
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
        catch (BusinessException ex) when (ex.Field != null)
        {
            // Field problem (e.g. the image): show it next to the field
            ModelState.AddModelError(ex.Field, ex.Message);
        }
        catch (BusinessException ex)
        {
            // Deadline passed, not started or already submitted: the form cannot be used any more
            TempData["Error"] = ex.Message;
            return RedirectToCompetition(form.CompetitionId);
        }

        // Show the form again; the competition details come from the database, not from the browser
        try
        {
            var current = await _submissions.GetNewFormAsync(form.CompetitionId, me);
            form.CompetitionTitle = current.CompetitionTitle;
            form.Deadline = current.Deadline;
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
        catch (BusinessException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToCompetition(form.CompetitionId);
        }
        return View(form);
    }

    /// <summary>Details of an own entry: image, texts, deadline and the evaluation once results are published.</summary>
    /// <param name="id">Submission id</param>
    [HttpGet("{id:long}")]
    public async Task<IActionResult> Details(long id)
    {
        try
        {
            return View(await _submissions.GetDetailsAsync(id, User.GetProfileId()));
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>Edit form (texts + optional new image) while the competition is ongoing.</summary>
    /// <param name="id">Submission id</param>
    [HttpGet("{id:long}/edit")]
    public async Task<IActionResult> Edit(long id)
    {
        try
        {
            return View(await _submissions.GetForEditAsync(id, User.GetProfileId()));
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
        catch (BusinessException ex)
        {
            // After the deadline the entry is read only: back to the details with the reason
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    /// <summary>Saves the changes and replaces the image when a new one was chosen.</summary>
    /// <param name="id">Submission id</param>
    /// <param name="form">Form data</param>
    [HttpPost("{id:long}/edit")]
    [RequestSizeLimit(MaxRequestBytes)]
    public async Task<IActionResult> Edit(long id, SubmissionEditVm form)
    {
        var me = User.GetProfileId();
        try
        {
            if (ModelState.IsValid)
            {
                var result = await _submissions.UpdateAsync(id, form, me);
                TempData["Success"] = "Your entry has been updated.";
                if (result.Warning != null)
                {
                    // Small image: accepted, but the student is told it may look blurry (BR-17)
                    TempData["Warning"] = result.Warning;
                }
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

        // Show the form again; competition and current file come from the database
        try
        {
            var current = await _submissions.GetForEditAsync(id, me);
            form.Id = id;
            form.CompetitionId = current.CompetitionId;
            form.CompetitionTitle = current.CompetitionTitle;
            form.Deadline = current.Deadline;
            form.OriginalFileName = current.OriginalFileName;
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

    /// <summary>Deletes an own entry and its image while the competition is ongoing. POST only, with anti-forgery token.</summary>
    /// <param name="id">Submission id</param>
    [HttpPost("{id:long}/delete")]
    public async Task<IActionResult> Delete(long id)
    {
        try
        {
            await _submissions.DeleteAsync(id, User.GetProfileId());
            TempData["Success"] = "Your entry has been deleted.";
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

    /// <summary>Back to the competition details page (Student area).</summary>
    private RedirectToActionResult RedirectToCompetition(long competitionId)
        => RedirectToAction("Details", "Competitions", new { id = competitionId });
}
