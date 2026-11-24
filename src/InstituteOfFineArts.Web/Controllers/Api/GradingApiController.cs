using InstituteOfFineArts.Web.Infrastructure.Audit;
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
/// Grading for the mobile app: competitions with progress, entries, entry details, save / delete the evaluation.
/// TEACHER: competitions whose submission period is over, grades the CLOSED ones (same rules as /teacher/evaluations).
/// STAFF: published (GRADED) competitions only, read only – staff do not grade. Writes go through IEvaluationService.
/// </summary>
[Route("api/staff")]
[Authorize(Roles = RoleNames.Teacher + "," + RoleNames.Staff)]
public class GradingApiController : ApiControllerBase
{
    private readonly AppDbContext _db;
    private readonly IEvaluationService _evaluations;

    public GradingApiController(AppDbContext db, IEvaluationService evaluations)
    {
        _db = db;
        _evaluations = evaluations;
    }

    /// <summary>True for a teacher (grades), false for staff (published results, read only).</summary>
    private bool IsTeacher => User.IsInRole(RoleNames.Teacher);

    /// <summary>The signed-in teacher's staff_profile.id; null for staff.</summary>
    private long? JudgeId => IsTeacher ? User.GetProfileId() : null;

    /// <summary>Who asks for a score sheet.</summary>
    private ScoreSheetViewer Viewer => new(IsTeacher ? RoleNames.Teacher : RoleNames.Staff, User.GetProfileId());

    /// <summary>Competitions the caller may see: closed or graded for a teacher, graded for staff.</summary>
    private IReadOnlySet<CompetitionStatus> Visible => IsTeacher ? GradingQueries.TeacherStatuses : GradingQueries.StaffStatuses;

    /// <summary>Competitions with the number of entries and graded ones (a teacher: only those they judge).</summary>
    [HttpGet("competitions")]
    public async Task<ActionResult<List<GradingCompetitionDto>>> Competitions()
        => await GradingQueries.CompetitionsAsync(_db, Visible, JudgeId);

    /// <summary>All entries of a competition (not paged) with optional filters.</summary>
    [HttpGet("competitions/{id:long}/submissions")]
    public async Task<ActionResult<GradingListDto>> Submissions(long id, [FromQuery] bool? graded, [FromQuery] Rating? rating, [FromQuery] string? q)
        => await GradingQueries.ListAsync(_db, id, graded, rating, q, Visible, JudgeId);

    /// <summary>Entry details for grading.</summary>
    [HttpGet("submissions/{id:long}")]
    [AuditView]
    public async Task<ActionResult<GradingDetailDto>> Detail(long id)
        => await GradingQueries.DetailAsync(_db, _evaluations, id, Visible, JudgeId, Viewer);

    /// <summary>Creates the judge's grade, or edits it (needs its rowVersion; stale → 409).</summary>
    [HttpPut("submissions/{id:long}/evaluation")]
    [Authorize(Roles = RoleNames.Teacher)]
    public async Task<ActionResult<GradingDetailDto>> Save(long id, [FromBody] GradingEvaluationRequest request)
    {
        // 404 for an unknown entry, an open competition or one the teacher does not judge
        var judgeId = User.GetProfileId();
        await _evaluations.GetPageAsync(id, judgeId);

        var stamp = await GradingQueries.EvaluationStampAsync(_db, id, judgeId);
        if (stamp is { } s && (string.IsNullOrWhiteSpace(request.RowVersion) || !RowVersion.Matches(request.RowVersion, s.CreatedAt, s.UpdatedAt)))
        {
            return StaleVersion();
        }

        var form = new EvaluationFormVm
        {
            Disqualified = request.Disqualified,
            Scores = request.Scores.Select(x => new CriterionScoreVm { CompetitionCriterionId = x.CriterionId, Score = x.Score, Comment = x.Comment }).ToList(),
            PositivePoints = request.PositivePoints,
            NegativePoints = request.NegativePoints,
            ImprovementAreas = request.ImprovementAreas,
        };
        await _evaluations.SaveAsync(id, judgeId, form);
        return await GradingQueries.DetailAsync(_db, _evaluations, id, Visible, judgeId, Viewer);
    }

    /// <summary>Deletes the judge's grade (rowVersion must be the current one).</summary>
    [HttpDelete("submissions/{id:long}/evaluation")]
    [Authorize(Roles = RoleNames.Teacher)]
    public async Task<IActionResult> DeleteEvaluation(long id, [FromQuery] string? rowVersion)
    {
        var judgeId = User.GetProfileId();
        await _evaluations.GetPageAsync(id, judgeId);

        var stamp = await GradingQueries.EvaluationStampAsync(_db, id, judgeId);
        if (stamp is { } s && !RowVersion.Matches(rowVersion, s.CreatedAt, s.UpdatedAt))
        {
            return StaleVersion();
        }

        await _evaluations.DeleteAsync(id, judgeId);
        return NoContent();
    }
}
