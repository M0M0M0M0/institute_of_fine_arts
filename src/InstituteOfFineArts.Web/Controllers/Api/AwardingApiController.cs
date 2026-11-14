using InstituteOfFineArts.Web.Controllers.Api.Dto;
using InstituteOfFineArts.Web.Data;
using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Helpers;
using InstituteOfFineArts.Web.Infrastructure.Api;
using InstituteOfFineArts.Web.Infrastructure.Security;
using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.Services.Interfaces;
using InstituteOfFineArts.Web.ViewModels.Staff.Awards;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InstituteOfFineArts.Web.Controllers.Api;

/// <summary>
/// Mobile app, staff: issue awards and publish results (same services and rules as the web Staff area).
/// Differences from the app's model: revoking an award deletes it (no "revoked" state, same as the web), there is no
/// delivery tracking and no evaluation adjustment after publishing, so those two endpoints answer 409.
/// </summary>
[Route("api/staff/awarding")]
[Authorize(Roles = RoleNames.Staff)]
public class AwardingApiController : ApiControllerBase
{
    private readonly AppDbContext _db;
    private readonly IAwardIssuedService _awards;
    private readonly ICompetitionResultService _results;

    public AwardingApiController(AppDbContext db, IAwardIssuedService awards, ICompetitionResultService results)
    {
        _db = db;
        _awards = awards;
        _results = results;
    }

    /// <summary>GET competitions: CLOSED and GRADED competitions with award progress.</summary>
    [HttpGet("competitions")]
    public async Task<ActionResult<List<AwardingCompetitionDto>>> Competitions()
        => await AwardingQueries.CompetitionsAsync(_db);

    /// <summary>GET competitions/{id}: awards, issued awards and the publish checklist.</summary>
    [HttpGet("competitions/{id:long}")]
    public async Task<ActionResult<AwardingDetailDto>> Detail(long id)
        => await AwardingQueries.DetailAsync(_db, id);

    /// <summary>GET competitions/{id}/candidates: BEST entries without an award yet.</summary>
    [HttpGet("competitions/{id:long}/candidates")]
    public async Task<ActionResult<List<AwardCandidateDto>>> Candidates(long id)
    {
        var form = await _awards.GetForCreateAsync(id);
        return form.Submissions
            .Select(s => new AwardCandidateDto(s.Id, s.Title, s.StudentName, s.StudentCode, Rating.BEST))
            .ToList();
    }

    /// <summary>POST competitions/{id}/awards: issue an award (also after publishing, like the web).</summary>
    [HttpPost("competitions/{id:long}/awards")]
    public async Task<ActionResult<AwardingDetailDto>> Issue(long id, [FromBody] IssueAwardRequest body)
    {
        var form = new AwardIssuedFormVm
        {
            CompetitionId = id,
            CompetitionAwardId = body.CompetitionAwardId,
            SubmissionId = body.SubmissionId,
            IssuedDate = DateTimeHelper.Today,
            Remarks = body.Remarks,
        };
        await _awards.CreateAsync(form, User.GetProfileId());
        return await AwardingQueries.DetailAsync(_db, id);
    }

    /// <summary>PUT awards/{id}: change award, entry or remarks (the issued date is kept).</summary>
    [HttpPut("awards/{id:long}")]
    public async Task<ActionResult<AwardingDetailDto>> Update(long id, [FromBody] UpdateAwardRequest body)
    {
        var current = await FindAsync(id);
        if (!RowVersion.Matches(body.RowVersion, current.CreatedAt, current.UpdatedAt)) return StaleVersion();

        var form = new AwardIssuedFormVm
        {
            Id = id,
            CompetitionId = current.CompetitionId,
            CompetitionAwardId = body.CompetitionAwardId,
            SubmissionId = body.SubmissionId,
            IssuedDate = current.IssuedDate,
            Remarks = body.Remarks,
        };
        var competitionId = await _awards.UpdateAsync(id, form);
        return await AwardingQueries.DetailAsync(_db, competitionId);
    }

    /// <summary>DELETE awards/{id}?rowVersion=: remove an award before publishing (the service's revoke, logged as such).</summary>
    [HttpDelete("awards/{id:long}")]
    public async Task<ActionResult<AwardingDetailDto>> Delete(long id, [FromQuery] string? rowVersion)
    {
        var current = await FindAsync(id);
        if (!RowVersion.Matches(rowVersion, current.CreatedAt, current.UpdatedAt)) return StaleVersion();
        if (current.Status == CompetitionStatus.GRADED)
        {
            throw new BusinessException("Results are already published. Revoke the award with a reason instead.");
        }
        var competitionId = await _awards.RevokeAsync(id, "Removed before the results were published.", User.GetProfileId());
        return await AwardingQueries.DetailAsync(_db, competitionId);
    }

    /// <summary>POST competitions/{id}/publish: CLOSED to GRADED; empty award slots must be confirmed.</summary>
    [HttpPost("competitions/{id:long}/publish")]
    public async Task<ActionResult<AwardingDetailDto>> Publish(long id, [FromBody] PublishRequest body)
    {
        var detail = await AwardingQueries.DetailAsync(_db, id);
        if (!detail.IsPublished && detail.PendingEvaluations == 0 && detail.EmptySlots > 0 && !body.ConfirmEmptySlots)
        {
            throw new BusinessException(
                $"{detail.EmptySlots} award slot(s) are still empty. Confirm that they stay empty to publish.");
        }
        await _results.PublishAsync(id, null, User.GetProfileId());
        return await AwardingQueries.DetailAsync(_db, id);
    }

    /// <summary>POST awards/{id}/revoke: revoke with a reason (the award is deleted and the reason logged, BR-33).</summary>
    [HttpPost("awards/{id:long}/revoke")]
    public async Task<ActionResult<AwardingDetailDto>> Revoke(long id, [FromBody] RevokeRequest body)
    {
        var current = await FindAsync(id);
        if (!RowVersion.Matches(body.RowVersion, current.CreatedAt, current.UpdatedAt)) return StaleVersion();
        var competitionId = await _awards.RevokeAsync(id, body.Reason, User.GetProfileId());
        return await AwardingQueries.DetailAsync(_db, competitionId);
    }

    /// <summary>PUT awards/{id}/delivery: the database has no delivery tracking.</summary>
    [HttpPut("awards/{id:long}/delivery")]
    public IActionResult SetDelivery(long id) => NotSupported("Marking an award as delivered");

    /// <summary>PUT submissions/{id}/adjust-evaluation: evaluations are read only after publishing (BR-21).</summary>
    [HttpPut("submissions/{id:long}/adjust-evaluation")]
    public IActionResult AdjustEvaluation(long id) => NotSupported("Adjusting an evaluation after publishing");

    /// <summary>Fields needed to check an issued award before changing it.</summary>
    private sealed record AwardRef(long CompetitionId, CompetitionStatus Status, DateOnly IssuedDate, DateTime CreatedAt, DateTime? UpdatedAt);

    private async Task<AwardRef> FindAsync(long id)
    {
        var row = await _db.AwardsIssued.AsNoTracking()
            .Where(a => a.Id == id)
            .Select(a => new { a.CompetitionAward.Competition, a.IssuedDate, a.CreatedAt, a.UpdatedAt })
            .FirstOrDefaultAsync()
            ?? throw new NotFoundException($"Issued award {id} was not found.");
        return new AwardRef(row.Competition.Id, CompetitionStatusResolver.Resolve(row.Competition), row.IssuedDate, row.CreatedAt, row.UpdatedAt);
    }
}
