using InstituteOfFineArts.Web.Data;
using InstituteOfFineArts.Web.Helpers;
using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.Services.Interfaces;
using InstituteOfFineArts.Web.ViewModels.Teacher;
using Microsoft.EntityFrameworkCore;

namespace InstituteOfFineArts.Web.Services.Implementations;

/// <summary>
/// Teacher dashboard: CLOSED competitions with "graded X of Y" (the ones with the most entries left first) and the
/// ONGOING competitions closing soonest. Only counts and titles of open competitions are shown, never their entries.
/// </summary>
public class TeacherDashboardService : ITeacherDashboardService
{
    /// <summary>How many competitions "Closing soon" lists.</summary>
    public const int ClosingSoonCount = 5;

    /// <summary>Database access.</summary>
    private readonly AppDbContext _db;

    /// <summary>Receives the database context through Dependency Injection.</summary>
    public TeacherDashboardService(AppDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<TeacherDashboardVm> GetAsync(DateOnly? today = null)
    {
        var day = today ?? DateTimeHelper.Today;

        var closed = await _db.Competitions.AsNoTracking()
            .WhereStatus(CompetitionStatus.CLOSED, day)
            .Select(c => new GradingCompetitionVm(c.Id, c.Code, c.Title, c.EndDate,
                c.Submissions.Count, c.Submissions.Count(s => s.Evaluation != null)))
            .ToListAsync();

        var closing = await _db.Competitions.AsNoTracking()
            .WhereStatus(CompetitionStatus.ONGOING, day)
            .OrderBy(c => c.EndDate).ThenBy(c => c.Id)
            .Take(ClosingSoonCount)
            .Select(c => new ClosingCompetitionVm(c.Id, c.Title, c.EndDate))
            .ToListAsync();

        return new TeacherDashboardVm
        {
            ToGrade = closed.OrderByDescending(c => c.Left).ThenBy(c => c.EndDate).ToList(),
            ClosingSoon = closing,
        };
    }
}
