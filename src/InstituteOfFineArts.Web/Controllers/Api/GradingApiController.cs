using InstituteOfFineArts.Web.Controllers.Api.Dto;
using InstituteOfFineArts.Web.Data;
using InstituteOfFineArts.Web.Helpers;
using InstituteOfFineArts.Web.Infrastructure.Api;
using InstituteOfFineArts.Web.Infrastructure.Security;
using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.Services.Interfaces;
using InstituteOfFineArts.Web.ViewModels.Staff.Evaluations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InstituteOfFineArts.Web.Controllers.Api;

/// <summary>
/// Staff grading for the mobile app: competitions with progress, entries to grade, entry details, save / delete
/// the evaluation. Same rules and role as /staff/evaluations (STAFF only); writes go through IEvaluationService.
/// </summary>
[Route("api/staff")]
[Authorize(Roles = RoleNames.Staff)]
public class GradingApiController : ApiControllerBase
{
    private readonly AppDbContext _db;
    private readonly IEvaluationService _evaluations;

    public GradingApiController(AppDbContext db, IEvaluationService evaluations)
    {
        _db = db;
        _evaluations = evaluations;
    }

    /// <summary>Competitions that have entries or are ready to grade, with the number of entries and evaluated ones.</summary>
    [HttpGet("competitions")]
    public async Task<ActionResult<List<GradingCompetitionDto>>> Competitions()
        => await GradingQueries.CompetitionsAsync(_db);

    /// <summary>All entries of a competition (not paged) with optional filters.</summary>
    [HttpGet("competitions/{id:long}/submissions")]
    public async Task<ActionResult<GradingListDto>> Submissions(long id, [FromQuery] bool? graded, [FromQuery] Rating? rating, [FromQuery] string? q)
        => await GradingQueries.ListAsync(_db, id, graded, rating, q);

    /// <summary>Entry details for grading.</summary>
    [HttpGet("submissions/{id:long}")]
    public async Task<ActionResult<GradingDetailDto>> Detail(long id)
        => await GradingQueries.DetailAsync(_db, _evaluations, id);

    /// <summary>Creates the evaluation, or edits the existing one (needs its rowVersion; stale → 409).</summary>
    [HttpPut("submissions/{id:long}/evaluation")]
    public async Task<ActionResult<GradingDetailDto>> Save(long id, [FromBody] GradingEvaluationRequest request)
    {
        // 404 for an unknown entry before anything else
        await _evaluations.GetPageAsync(id);

        var stamp = await GradingQueries.EvaluationStampAsync(_db, id);
        if (stamp is { } s && (string.IsNullOrWhiteSpace(request.RowVersion) || !RowVersion.Matches(request.RowVersion, s.CreatedAt, s.UpdatedAt)))
        {
            return StaleVersion();
        }

        var form = new EvaluationFormVm
        {
            Rating = request.Rating,
            PositivePoints = request.PositivePoints,
            NegativePoints = request.NegativePoints,
            ImprovementAreas = request.ImprovementAreas,
        };
        await _evaluations.SaveAsync(id, User.GetProfileId(), form);
        return await GradingQueries.DetailAsync(_db, _evaluations, id);
    }

    /// <summary>Deletes the evaluation (rowVersion must be the current one).</summary>
    [HttpDelete("submissions/{id:long}/evaluation")]
    public async Task<IActionResult> DeleteEvaluation(long id, [FromQuery] string? rowVersion)
    {
        await _evaluations.GetPageAsync(id);

        var stamp = await GradingQueries.EvaluationStampAsync(_db, id);
        if (stamp is { } s && !RowVersion.Matches(rowVersion, s.CreatedAt, s.UpdatedAt))
        {
            return StaleVersion();
        }

        await _evaluations.DeleteAsync(id);
        return NoContent();
    }
}
