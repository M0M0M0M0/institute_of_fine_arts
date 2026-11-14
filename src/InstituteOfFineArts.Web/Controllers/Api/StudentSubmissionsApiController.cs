using InstituteOfFineArts.Web.Controllers.Api.Dto;
using InstituteOfFineArts.Web.Data;
using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Helpers;
using InstituteOfFineArts.Web.Infrastructure.Api;
using InstituteOfFineArts.Web.Infrastructure.Security;
using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.Services.Implementations;
using InstituteOfFineArts.Web.Services.Interfaces;
using InstituteOfFineArts.Web.ViewModels.Student.Submissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InstituteOfFineArts.Web.Controllers.Api;

/// <summary>
/// The signed-in student's own entries (mobile app): submit, list, details, edit, delete. All writes go through
/// ISubmissionService, so deadline, one-entry-per-competition and JPEG rules are the web's rules. Other students'
/// entries are 404.
/// </summary>
[Route("api")]
[Authorize(Roles = RoleNames.Student)]
public class StudentSubmissionsApiController : ApiControllerBase
{
    /// <summary>A 5 MB image plus 1 MB for the other fields (BR-17).</summary>
    private const long MaxRequestBytes = 6 * 1024 * 1024;

    private readonly ISubmissionService _submissions;
    private readonly AppDbContext _db;

    public StudentSubmissionsApiController(ISubmissionService submissions, AppDbContext db)
    {
        _submissions = submissions;
        _db = db;
    }

    /// <summary>Id of my entry in the competition, or { submissionId: null }.</summary>
    [HttpGet("competitions/{competitionId:long}/my-submission")]
    public async Task<IActionResult> MySubmission(long competitionId)
    {
        var me = User.GetProfileId();
        var id = await _db.Submissions.AsNoTracking()
            .Where(s => s.CompetitionId == competitionId && s.StudentId == me)
            .Select(s => (long?)s.Id).FirstOrDefaultAsync();
        if (id == null && !await _db.Competitions.AnyAsync(c => c.Id == competitionId))
        {
            throw new NotFoundException($"Competition {competitionId} was not found.");
        }
        return Ok(new { SubmissionId = id });
    }

    /// <summary>Submit an entry (multipart). 409 + submissionId when the student already has one.</summary>
    [HttpPost("competitions/{competitionId:long}/submissions")]
    [RequestSizeLimit(MaxRequestBytes)]
    public async Task<IActionResult> Create(long competitionId, [FromForm] SubmissionApiForm form)
    {
        var me = User.GetProfileId();
        var vm = new SubmissionFormVm
        {
            CompetitionId = competitionId,
            Title = form.Title ?? string.Empty,
            Description = form.Description ?? string.Empty,
            TextType = form.ParsedTextType,
            TextContent = form.TextContent ?? string.Empty,
            Image = form.Image,
        };
        try
        {
            var result = await _submissions.CreateAsync(vm, me);
            var detail = await PublicAndSubmissionQueries.DetailAsync(_db, result.Id, me);
            return Created($"/api/submissions/{result.Id}", detail);
        }
        catch (BusinessException ex) when (ex.Message == SubmissionService.AlreadySubmittedMessage)
        {
            var existing = await _db.Submissions.AsNoTracking()
                .Where(s => s.CompetitionId == competitionId && s.StudentId == me)
                .Select(s => (long?)s.Id).FirstOrDefaultAsync();
            return Problem(StatusCodes.Status409Conflict, ex.Message, new Dictionary<string, object?> { ["submissionId"] = existing });
        }
    }

    /// <summary>My entries with optional filters; a plain array (not paged).</summary>
    [HttpGet("my/submissions")]
    public async Task<ActionResult<List<SubmissionListItemDto>>> Mine(long? competitionId, string? q, DateOnly? from, DateOnly? to, Rating? rating)
        => await PublicAndSubmissionQueries.MineAsync(_db, User.GetProfileId(), competitionId, q, from, to, rating);

    /// <summary>One of my entries.</summary>
    [HttpGet("submissions/{id:long}")]
    public async Task<ActionResult<SubmissionDetailDto>> Details(long id)
        => await PublicAndSubmissionQueries.DetailAsync(_db, id, User.GetProfileId());

    /// <summary>Edit texts and optionally replace the image (multipart), while the competition is ongoing.</summary>
    [HttpPut("submissions/{id:long}")]
    [RequestSizeLimit(MaxRequestBytes)]
    public async Task<ActionResult<SubmissionDetailDto>> Update(long id, [FromForm] SubmissionApiForm form)
    {
        var me = User.GetProfileId();
        var vm = new SubmissionEditVm
        {
            Id = id,
            Title = form.Title ?? string.Empty,
            Description = form.Description ?? string.Empty,
            TextType = form.ParsedTextType,
            TextContent = form.TextContent ?? string.Empty,
            NewImage = form.Image,
        };
        await _submissions.UpdateAsync(id, vm, me);
        return await PublicAndSubmissionQueries.DetailAsync(_db, id, me);
    }

    /// <summary>Delete one of my entries while the competition is ongoing.</summary>
    [HttpDelete("submissions/{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        await _submissions.DeleteAsync(id, User.GetProfileId());
        return NoContent();
    }
}
