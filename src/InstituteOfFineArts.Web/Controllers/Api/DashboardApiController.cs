using InstituteOfFineArts.Web.Controllers.Api.Dto;
using InstituteOfFineArts.Web.Data;
using InstituteOfFineArts.Web.Helpers;
using InstituteOfFineArts.Web.Infrastructure.Api;
using InstituteOfFineArts.Web.Infrastructure.Security;
using InstituteOfFineArts.Web.Models.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InstituteOfFineArts.Web.Controllers.Api;

/// <summary>Home-screen summaries of the mobile app: GET /api/student/dashboard and GET /api/staff/dashboard.</summary>
[Route("api")]
public class DashboardApiController : ApiControllerBase
{
    /// <summary>Results published within this many days are "new".</summary>
    private const int NewResultDays = 30;

    private readonly AppDbContext _db;

    public DashboardApiController(AppDbContext db) => _db = db;

    /// <summary>Competition the staff has to grade.</summary>
    public sealed record ToGradeDto(long CompetitionId, string Title, DateOnly EndDate, int Total, int Evaluated);

    /// <summary>Fully graded competition waiting for its results to be published.</summary>
    public sealed record ToPublishDto(long CompetitionId, string Title, int EmptySlots);

    public sealed record StudentDashboardDto(IReadOnlyList<CompetitionSummaryDto> OpenToEnter, IReadOnlyList<SubmissionListItemDto> Editable,
        IReadOnlyList<SubmissionListItemDto> NewResults, int PendingProposals, int UnpaidSales);

    public sealed record StaffDashboardDto(IReadOnlyList<ToGradeDto> ToGrade, IReadOnlyList<ToPublishDto> ToPublish, int PendingProposals,
        int UnpaidSales);

    /// <summary>ONGOING competitions the student has not entered, the student's editable entries, results published in the last 30 days, counts.</summary>
    [HttpGet("student/dashboard")]
    [Authorize(Roles = RoleNames.Student)]
    public async Task<ActionResult<StudentDashboardDto>> Student()
    {
        var me = User.GetProfileId();
        var today = DateTimeHelper.Today;

        var entered = await _db.Submissions.AsNoTracking().Where(s => s.StudentId == me).Select(s => s.CompetitionId).ToListAsync();
        var rows = await _db.Competitions.AsNoTracking()
            .Where(c => c.Status != CompetitionStatus.GRADED && c.StartDate <= today && c.EndDate >= today)
            .Select(c => new { c.Id, c.Title, c.StartDate, c.EndDate, c.Status, Count = c.Submissions.Count, HasCover = c.CoverImagePath != null })
            .ToListAsync();
        var open = rows.Where(c => !entered.Contains(c.Id))
            .OrderBy(c => c.EndDate).ThenBy(c => c.Id)
            .Select(c => PublicAndSubmissionQueries.Summary(c.Id, c.Title, c.StartDate, c.EndDate, c.Status, c.Count, today, c.HasCover))
            .Where(c => c.Status == CompetitionStatus.ONGOING)
            .ToList();

        var mine = await PublicAndSubmissionQueries.MineAsync(_db, me, null, null, null, null, null);
        var editable = mine.Where(s => s.CompetitionStatus == CompetitionStatus.ONGOING).ToList();

        var since = DateTimeHelper.Now.AddDays(-NewResultDays);
        var recent = (await _db.Competitions.AsNoTracking()
            .Where(c => c.Status == CompetitionStatus.GRADED && c.PublishedAt != null && c.PublishedAt >= since)
            .Select(c => c.Id).ToListAsync()).ToHashSet();
        var newResults = mine.Where(s => s.CompetitionStatus == CompetitionStatus.GRADED && recent.Contains(s.CompetitionId)).ToList();

        var proposals = await _db.ExhibitionEntries.AsNoTracking()
            .CountAsync(e => e.Submission.StudentId == me && e.Status == EntryStatus.PENDING);
        var unpaid = await _db.ExhibitionEntries.AsNoTracking()
            .CountAsync(e => e.Submission.StudentId == me && e.Status == EntryStatus.SOLD && !e.PaidToStudent);

        return new StudentDashboardDto(open, editable, newResults, proposals, unpaid);
    }

    /// <summary>CLOSED competitions to grade / to publish, pending exhibition proposals, sold paintings not yet paid to the student.</summary>
    [HttpGet("staff/dashboard")]
    [Authorize(Roles = RoleNames.Staff)]
    public async Task<ActionResult<StaffDashboardDto>> Staff()
    {
        var today = DateTimeHelper.Today;
        var closed = await _db.Competitions.AsNoTracking()
            .Where(c => c.Status != CompetitionStatus.GRADED && c.EndDate < today)
            .Select(c => new
            {
                c.Id, c.Title, c.EndDate, Total = c.Submissions.Count,
                Evaluated = c.Submissions.Count(s => s.Evaluation != null),
                Slots = c.Awards.Sum(a => (int?)a.Quantity) ?? 0,
                Issued = c.Awards.Sum(a => (int?)a.IssuedAwards.Count) ?? 0,
            })
            .ToListAsync();

        var toGrade = closed.Where(c => c.Total > c.Evaluated)
            .OrderBy(c => c.EndDate).ThenBy(c => c.Id)
            .Select(c => new ToGradeDto(c.Id, c.Title, c.EndDate, c.Total, c.Evaluated)).ToList();
        var toPublish = closed.Where(c => c.Total > 0 && c.Total == c.Evaluated)
            .OrderBy(c => c.EndDate).ThenBy(c => c.Id)
            .Select(c => new ToPublishDto(c.Id, c.Title, Math.Max(0, c.Slots - c.Issued))).ToList();

        var proposals = await _db.ExhibitionEntries.AsNoTracking().CountAsync(e => e.Status == EntryStatus.PENDING);
        var unpaid = await _db.ExhibitionEntries.AsNoTracking().CountAsync(e => e.Status == EntryStatus.SOLD && !e.PaidToStudent);
        return new StaffDashboardDto(toGrade, toPublish, proposals, unpaid);
    }
}
