using InstituteOfFineArts.Web.Controllers.Api.Dto;
using InstituteOfFineArts.Web.Data;
using InstituteOfFineArts.Web.Helpers;
using InstituteOfFineArts.Web.Infrastructure.Api;
using InstituteOfFineArts.Web.Infrastructure.Security;
using InstituteOfFineArts.Web.Services.Interfaces;
using InstituteOfFineArts.Web.ViewModels.Student.ExhibitionEntries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InstituteOfFineArts.Web.Controllers.Api;

/// <summary>
/// Mobile app, student side of exhibitions: /api/student/exhibitions, /eligible-submissions, /exhibition-entries.
/// Reads are AsNoTracking queries; proposing, changing the price and withdrawing go through IStudentExhibitionEntryService.
/// </summary>
[Route("api/student")]
[Authorize(Roles = RoleNames.Student)]
public class StudentExhibitionsApiController : ApiControllerBase
{
    private readonly AppDbContext _db;
    private readonly IStudentExhibitionEntryService _entries;

    public StudentExhibitionsApiController(AppDbContext db, IStudentExhibitionEntryService entries)
    {
        _db = db;
        _entries = entries;
    }

    /// <summary>Request body of POST /api/student/exhibition-entries.</summary>
    public sealed class ProposeRequest
    {
        public long? ExhibitionId { get; set; }
        public long? SubmissionId { get; set; }
        public decimal? AskingPrice { get; set; }
    }

    /// <summary>Request body of PUT /api/student/exhibition-entries/{id}.</summary>
    public sealed class UpdatePriceRequest
    {
        public decimal? AskingPrice { get; set; }
        public string? RowVersion { get; set; }
    }

    /// <summary>GET /api/student/exhibitions: counts are the student's own entries.</summary>
    [HttpGet("exhibitions")]
    public async Task<IActionResult> Exhibitions()
        => Ok(await ExhibitionApiQueries.ExhibitionsAsync(_db, null, null, User.GetProfileId()));

    /// <summary>GET /api/student/exhibitions/{id}</summary>
    [HttpGet("exhibitions/{id:long}")]
    public async Task<IActionResult> Exhibition(long id)
        => Ok(await ExhibitionApiQueries.ExhibitionAsync(_db, id, User.GetProfileId()));

    /// <summary>GET /api/student/eligible-submissions: the student's paintings that may be proposed.</summary>
    [HttpGet("eligible-submissions")]
    public async Task<IActionResult> Eligible()
        => Ok(await ExhibitionApiQueries.EligibleAsync(_db, User.GetProfileId(), null));

    /// <summary>GET /api/student/exhibition-entries</summary>
    [HttpGet("exhibition-entries")]
    public async Task<IActionResult> Entries()
        => Ok(await ExhibitionApiQueries.StudentEntriesAsync(_db, User.GetProfileId()));

    /// <summary>GET /api/student/exhibition-entries/{id}: 404 for another student's entry.</summary>
    [HttpGet("exhibition-entries/{id:long}")]
    public async Task<IActionResult> Entry(long id)
        => Ok(await ExhibitionApiQueries.StudentEntryAsync(_db, User.GetProfileId(), id));

    /// <summary>POST /api/student/exhibition-entries → the new PENDING entry.</summary>
    [HttpPost("exhibition-entries")]
    public async Task<IActionResult> Propose(ProposeRequest request)
    {
        var form = new ProposeEntryFormVm
        {
            ExhibitionId = request.ExhibitionId ?? 0,
            SubmissionId = request.SubmissionId,
            AskingPrice = request.AskingPrice,
        };
        var id = await _entries.CreateAsync(form, User.GetProfileId(), User.GetAccountId());
        var dto = await ExhibitionApiQueries.StudentEntryAsync(_db, User.GetProfileId(), id);
        return StatusCode(StatusCodes.Status201Created, dto);
    }

    /// <summary>PUT /api/student/exhibition-entries/{id}: new asking price while PENDING; 409 when the rowVersion is stale.</summary>
    [HttpPut("exhibition-entries/{id:long}")]
    public async Task<IActionResult> UpdatePrice(long id, UpdatePriceRequest request)
    {
        var profileId = User.GetProfileId();
        var current = await ExhibitionApiQueries.StudentEntryAsync(_db, profileId, id); // 404 unless own
        if (request.RowVersion != null && request.RowVersion != current.RowVersion) return StaleVersion();

        await _entries.UpdatePriceAsync(id, new EditPriceFormVm { Id = id, AskingPrice = request.AskingPrice }, profileId);
        return Ok(await ExhibitionApiQueries.StudentEntryAsync(_db, profileId, id));
    }

    /// <summary>DELETE /api/student/exhibition-entries/{id}: withdraw a PENDING proposal (the service deletes it).</summary>
    [HttpDelete("exhibition-entries/{id:long}")]
    public async Task<IActionResult> Withdraw(long id)
    {
        await _entries.WithdrawAsync(id, User.GetProfileId());
        return NoContent();
    }
}
