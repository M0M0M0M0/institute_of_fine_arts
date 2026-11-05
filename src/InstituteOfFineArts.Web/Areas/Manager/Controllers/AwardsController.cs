using InstituteOfFineArts.Web.Services.Interfaces;
using InstituteOfFineArts.Web.ViewModels.Staff.Awards;
using Microsoft.AspNetCore.Mvc;

namespace InstituteOfFineArts.Web.Areas.Manager.Controllers;

/// <summary>
/// Manager – issued awards, read only (F-M11-04, task BE2-13; role matrix: Manager R,S on AwardIssued):
/// GET /manager/awards (list + search). Same filters and service as /staff/awards, without issue / edit / revoke.
/// </summary>
[Route("manager/awards")]
public class AwardsController : ManagerBaseController
{
    /// <summary>Issued award service (only its read methods are used here).</summary>
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
        return View(await _awards.SearchAsync(search));
    }
}
