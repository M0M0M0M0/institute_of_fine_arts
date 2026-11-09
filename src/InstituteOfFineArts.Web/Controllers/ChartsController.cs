using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.Services.Interfaces;
using InstituteOfFineArts.Web.ViewModels.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InstituteOfFineArts.Web.Controllers;

/// <summary>
/// JSON data of the dashboard charts (task BE2-OPT 2; F-M15-01), drawn with Chart.js by the FE:
/// GET /charts/submissions-by-competition?status= · /charts/ratings?competitionId=&amp;status= ·
/// /charts/sales-by-exhibition?status=. Shape <c>{ labels: [...], datasets: [{ label, data: [...] }] }</c>.
/// STAFF and MANAGER only (same roles as the competition and sales reports, role matrix §2.3); other roles → 403.
/// </summary>
[Route("charts")]
[Authorize(Roles = ReportCatalog.StaffAndManager)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class ChartsController : Controller
{
    /// <summary>Message of a 400 answer (e.g. an unknown status value).</summary>
    public const string InvalidFilterMessage = "Invalid chart filter.";

    /// <summary>Chart queries.</summary>
    private readonly IChartDataService _charts;

    /// <summary>Receives the service through Dependency Injection.</summary>
    public ChartsController(IChartDataService charts)
    {
        _charts = charts;
    }

    /// <summary>Submissions and evaluated submissions per competition.</summary>
    /// <param name="status">Only competitions with this actual status</param>
    [HttpGet("submissions-by-competition")]
    public async Task<IActionResult> SubmissionsByCompetition([FromQuery] CompetitionStatus? status)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new { error = InvalidFilterMessage });
        }
        return Json(await _charts.GetSubmissionsByCompetitionAsync(status));
    }

    /// <summary>Rating distribution (plus "Not graded") of all or one competition.</summary>
    /// <param name="competitionId">Only this competition</param>
    /// <param name="status">Only competitions with this actual status</param>
    [HttpGet("ratings")]
    public async Task<IActionResult> Ratings([FromQuery] long? competitionId, [FromQuery] CompetitionStatus? status)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new { error = InvalidFilterMessage });
        }
        try
        {
            return Json(await _charts.GetRatingsAsync(competitionId, status));
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>Revenue and paintings sold per exhibition.</summary>
    /// <param name="status">Only exhibitions with this actual status</param>
    [HttpGet("sales-by-exhibition")]
    public async Task<IActionResult> SalesByExhibition([FromQuery] ExhibitionStatus? status)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new { error = InvalidFilterMessage });
        }
        return Json(await _charts.GetSalesByExhibitionAsync(status));
    }
}
