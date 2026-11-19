using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.ViewModels.Admin.AuditLog;
using InstituteOfFineArts.Web.Infrastructure.Audit;
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

    /// <summary>History of the competition (audit log).</summary>
    private readonly IAuditLogService _audit;

    /// <summary>Receives the competition service through Dependency Injection.</summary>
    public CompetitionsController(ICompetitionService competitions, IAuditLogService audit)
    {
        _competitions = competitions;
        _audit = audit;
    }

    /// <summary>List with filters (keyword, status, date range) and paging.</summary>
    /// <param name="search">Filters from the query string</param>
    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] CompetitionSearchVm search)
    {
        ViewBag.Search = search;
        return View(await _competitions.SearchAsync(search));
    }

    /// <summary>Empty create form, with one empty award row to start with (rank 1, one winner).</summary>
    [HttpGet("new")]
    public IActionResult Create() => View(new CompetitionFormVm { Awards = [new CompetitionAwardRowVm { Rank = 1, Quantity = 1 }] });

    /// <summary>Creates the competition with the awards typed on the form, then opens its details.</summary>
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
            var awards = form.Awards.Count(a => !a.Remove && !a.IsBlank);
            // BR-09: a competition should have at least one award before it opens (a warning, not a block)
            TempData["Success"] = awards == 0
                ? "Competition created. It has no awards yet: add them before the competition opens."
                : $"Competition created with {awards} award{(awards == 1 ? "" : "s")}.";
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
            var details = await _competitions.GetDetailsAsync(id);
            ViewBag.History = new HistoryVm(
                await _audit.GetHistoryAsync(new AuditHistoryQuery { CompetitionId = id, PageSize = 8 }),
                "History", $"/staff/competitions/{id}/history");
            return View(details);
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Full history of the competition: everything done to it, its awards, its entries (submitted, opened, graded),
    /// awards given and the results – filtered by kind of work and user.
    /// </summary>
    /// <param name="id">Competition id</param>
    /// <param name="category">Kind of work (null = all)</param>
    /// <param name="user">Part of the user name</param>
    /// <param name="page">Page number</param>
    [HttpGet("{id:long}/history")]
    public async Task<IActionResult> History(long id, [FromQuery] AuditCategory? category, [FromQuery] string? user, [FromQuery] int page = 1)
    {
        try
        {
            var details = await _competitions.GetDetailsAsync(id);
            var rows = await _audit.GetHistoryAsync(new AuditHistoryQuery { CompetitionId = id, Category = category, User = user, Page = page });
            return View("~/Views/Shared/HistoryPage.cshtml", new HistoryPageVm
            {
                Title = $"History of {details.Title}", BackUrl = $"/staff/competitions/{id}", BackText = details.Title,
                Category = category, User = user,
                Categories = [AuditCategory.COMPETITION, AuditCategory.SUBMISSION, AuditCategory.GRADING, AuditCategory.AWARD, AuditCategory.EXHIBITION, AuditCategory.SALE],
                History = new HistoryVm(rows, "History"),
            });
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
                // The changed fields go into the competition's history (AuditActionFilter)
                HttpContext.Items[AuditItems.Changes] = await _competitions.UpdateAsync(id, form);
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
            // How many times each award was given comes from the database, never from the browser
            var given = current.Awards.ToDictionary(a => a.Id, a => a.IssuedCount);
            foreach (var row in form.Awards)
            {
                row.IssuedCount = given.GetValueOrDefault(row.Id);
            }
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
