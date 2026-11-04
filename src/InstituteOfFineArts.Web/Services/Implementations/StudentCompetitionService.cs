using InstituteOfFineArts.Web.Data;
using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Helpers;
using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.Services.Interfaces;
using InstituteOfFineArts.Web.ViewModels.Student.Competitions;
using Microsoft.EntityFrameworkCore;

namespace InstituteOfFineArts.Web.Services.Implementations;

/// <summary>
/// Read-only competition pages of the Student area (F-M05-09, task BE1-06). The stored status may be stale
/// until the nightly job runs, so the status is always recalculated from the dates (BR-07).
/// </summary>
public class StudentCompetitionService : IStudentCompetitionService
{
    /// <summary>Database access.</summary>
    private readonly AppDbContext _db;

    /// <summary>Receives the database context through Dependency Injection.</summary>
    public StudentCompetitionService(AppDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<List<StudentCompetitionListItemVm>> GetOpenCompetitionsAsync(long studentProfileId)
    {
        var today = DateTimeHelper.Today;

        // Not graded and the deadline not passed = UPCOMING or ONGOING (same rule as CompetitionStatusResolver, run in SQL)
        var rows = await _db.Competitions.AsNoTracking()
            .Where(c => c.Status != CompetitionStatus.GRADED && c.EndDate >= today)
            .Select(c => new StudentCompetitionListItemVm
            {
                Id = c.Id,
                Code = c.Code,
                Title = c.Title,
                StartDate = c.StartDate,
                EndDate = c.EndDate,
                Status = c.Status,
                HasCover = c.CoverImagePath != null,
                AwardCount = c.Awards.Count,
                TopPrize = c.Awards.Select(a => (decimal?)a.PrizeValue).Max() ?? 0m,
                HasSubmitted = c.Submissions.Any(s => s.StudentId == studentProfileId),
            })
            .ToListAsync();

        // Status for today, not the stored value
        foreach (var item in rows)
        {
            item.Status = CompetitionStatusResolver.Resolve(
                new Competition { Status = item.Status, StartDate = item.StartDate, EndDate = item.EndDate }, today);
        }

        // Ongoing first (nearest deadline first), then upcoming (soonest start first)
        return rows
            .OrderBy(r => r.Status == CompetitionStatus.ONGOING ? 0 : 1)
            .ThenBy(r => r.Status == CompetitionStatus.ONGOING ? r.EndDate : r.StartDate)
            .ThenBy(r => r.Id)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<StudentCompetitionDetailsVm> GetDetailsAsync(long competitionId, long studentProfileId)
    {
        var c = await _db.Competitions.AsNoTracking()
            .Include(x => x.Awards)
            .FirstOrDefaultAsync(x => x.Id == competitionId)
            ?? throw new NotFoundException($"Competition {competitionId} was not found.");
        var status = CompetitionStatusResolver.Resolve(c);

        // The student's own entry (at most one per competition, BR-15)
        var mine = await _db.Submissions.AsNoTracking()
            .Where(s => s.CompetitionId == competitionId && s.StudentId == studentProfileId)
            .Select(s => new StudentOwnSubmissionVm(s.Id, s.Title, s.SubmittedAt))
            .FirstOrDefaultAsync();

        return new StudentCompetitionDetailsVm
        {
            Id = c.Id,
            Code = c.Code,
            Title = c.Title,
            Description = c.Description,
            Rules = c.Rules,
            StartDate = c.StartDate,
            EndDate = c.EndDate,
            Status = status,
            HasCover = c.CoverImagePath != null,
            Awards = c.Awards.OrderBy(a => a.Rank).ThenBy(a => a.AwardName)
                .Select(a => new StudentAwardVm(a.AwardName, a.Rank, a.PrizeValue, a.Quantity, a.PrizeDescription))
                .ToList(),
            MySubmission = mine,
            // Entries only while ONGOING (BR-13) and only one per student (BR-15)
            CanSubmit = status == CompetitionStatus.ONGOING && mine == null,
        };
    }
}
