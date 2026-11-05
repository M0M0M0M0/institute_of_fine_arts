using InstituteOfFineArts.Web.Data;
using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Helpers;
using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Services.Interfaces;
using InstituteOfFineArts.Web.ViewModels.Common;
using InstituteOfFineArts.Web.ViewModels.Staff.Exhibitions;
using InstituteOfFineArts.Web.ViewModels.Student.Exhibitions;
using Microsoft.EntityFrameworkCore;

namespace InstituteOfFineArts.Web.Services.Implementations;

/// <summary>
/// Read-only exhibition pages of the Student area (task BE2-09; F-M09-02, F-M09-03). The stored status may be
/// stale until the nightly job runs, so the status is always recalculated from the dates (BR-42).
/// </summary>
public class StudentExhibitionService : IStudentExhibitionService
{
    /// <summary>Database access.</summary>
    private readonly AppDbContext _db;

    /// <summary>Receives the database context through Dependency Injection.</summary>
    public StudentExhibitionService(AppDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<PagedResult<StudentExhibitionListItemVm>> SearchAsync(ExhibitionSearchVm search, long studentProfileId, int pageSize = 10)
    {
        var today = DateTimeHelper.Today;

        // Same filters as the staff list
        var query = ExhibitionService.Filter(_db.Exhibitions.AsNoTracking(), search, today);

        // UPCOMING (soonest first) → ONGOING → FINISHED; the rank repeats the status rule in SQL
        var rows = query
            .OrderBy(e => e.StartDate > today ? 0 : e.EndDate >= today ? 1 : 2)
            .ThenBy(e => e.StartDate).ThenBy(e => e.Id)
            .Select(e => new StudentExhibitionListItemVm
            {
                Id = e.Id,
                Name = e.Name,
                Venue = e.Venue,
                City = e.City,
                StartDate = e.StartDate,
                EndDate = e.EndDate,
                MyEntryCount = e.Entries.Count(x => x.Submission.StudentId == studentProfileId),
            });

        var page = await PagedResult<StudentExhibitionListItemVm>.CreateAsync(rows, search.Page, pageSize);

        // Status for today, not the stored value
        foreach (var item in page.Items)
        {
            item.Status = CompetitionStatusResolver.Resolve(new Exhibition { StartDate = item.StartDate, EndDate = item.EndDate }, today);
        }
        return page;
    }

    /// <inheritdoc />
    public async Task<StudentExhibitionDetailsVm> GetDetailsAsync(long exhibitionId, long studentProfileId)
    {
        var e = await _db.Exhibitions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == exhibitionId)
            ?? throw new NotFoundException($"Exhibition {exhibitionId} was not found.");

        // Only the student's own entries: other students' prices and sales are private
        var mine = await _db.ExhibitionEntries.AsNoTracking()
            .Where(x => x.ExhibitionId == exhibitionId && x.Submission.StudentId == studentProfileId)
            .OrderBy(x => x.Id)
            .Select(x => new StudentExhibitionEntryRowVm(x.Id, x.Submission.Title, x.AskingPrice, x.Status, x.SoldPrice, x.PaidToStudent))
            .ToListAsync();

        return new StudentExhibitionDetailsVm
        {
            Id = e.Id,
            Name = e.Name,
            Description = e.Description,
            Venue = e.Venue,
            City = e.City,
            Organizer = e.Organizer,
            StartDate = e.StartDate,
            EndDate = e.EndDate,
            Status = CompetitionStatusResolver.Resolve(e),
            MyEntries = mine,
        };
    }
}
