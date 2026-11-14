using InstituteOfFineArts.Web.Controllers.Api.Dto;
using InstituteOfFineArts.Web.Data;
using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Helpers;
using InstituteOfFineArts.Web.Infrastructure.Api;
using InstituteOfFineArts.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InstituteOfFineArts.Web.Controllers.Api;

/// <summary>
/// Public data for the mobile app: GET /api/home, /api/competitions, /api/competitions/{id}, /api/competitions/{id}/awards
/// (anonymous) and /api/competitions/{id}/result-adjustment (signed in). Reads the same data as the public web pages.
/// </summary>
[Route("api")]
public class HomeAndCompetitionsApiController : ApiControllerBase
{
    private readonly IPublicSiteService _site;
    private readonly AppDbContext _db;

    public HomeAndCompetitionsApiController(IPublicSiteService site, AppDbContext db)
    {
        _site = site;
        _db = db;
    }

    /// <summary>Upcoming + ongoing competitions and the winners of the latest GRADED competition.</summary>
    [HttpGet("home")]
    [AllowAnonymous]
    public async Task<ActionResult<HomeDto>> Home()
        => PublicAndSubmissionQueries.Home(await _site.GetHomeAsync(), DateTimeHelper.Today);

    /// <summary>Every competition, newest first (status for today, days left, entry count).</summary>
    [HttpGet("competitions")]
    [AllowAnonymous]
    public async Task<ActionResult<List<CompetitionSummaryDto>>> List()
        => await PublicAndSubmissionQueries.CompetitionsAsync(_db);

    /// <summary>Details: description, rules (conditions), deadline and award definitions.</summary>
    [HttpGet("competitions/{id:long}")]
    [AllowAnonymous]
    public async Task<ActionResult<CompetitionDetailDto>> Details(long id)
        => await PublicAndSubmissionQueries.CompetitionAsync(_db, id);

    /// <summary>Issued awards; empty until the results are published.</summary>
    [HttpGet("competitions/{id:long}/awards")]
    [AllowAnonymous]
    public async Task<ActionResult<List<AwardResultDto>>> Awards(long id)
        => await PublicAndSubmissionQueries.AwardsAsync(_db, id);

    /// <summary>This system has no "result adjustment": adjustedAt is always null.</summary>
    [HttpGet("competitions/{id:long}/result-adjustment")]
    [Authorize(Roles = $"{RoleNames.Student},{RoleNames.Staff},{RoleNames.Manager},{RoleNames.Admin}")]
    public async Task<IActionResult> ResultAdjustment(long id)
    {
        if (!await _db.Competitions.AnyAsync(c => c.Id == id))
        {
            throw new NotFoundException($"Competition {id} was not found.");
        }
        return Ok(new { AdjustedAt = (DateTime?)null });
    }
}
