using InstituteOfFineArts.Web.Data;
using InstituteOfFineArts.Web.Helpers;
using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.Services.Interfaces;
using InstituteOfFineArts.Web.ViewModels.Admin;
using Microsoft.EntityFrameworkCore;

namespace InstituteOfFineArts.Web.Services.Implementations;

/// <summary>
/// Administrator dashboard (task BE2-04): read-only counts over the whole database.
/// </summary>
public class AdminDashboardService : IAdminDashboardService
{
    /// <summary>Number of rows in the "recent logins" table.</summary>
    public const int RecentLoginCount = 5;

    /// <summary>Database access.</summary>
    private readonly AppDbContext _db;

    /// <summary>Receives the database context through Dependency Injection.</summary>
    public AdminDashboardService(AppDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<AdminDashboardVm> GetAsync(DateOnly? today = null)
    {
        var day = today ?? DateTimeHelper.Today;
        var vm = new AdminDashboardVm();

        // Accounts grouped by role and active flag: one small query instead of one per number
        var accounts = await _db.Accounts.AsNoTracking()
            .GroupBy(a => new { a.Role, a.Active })
            .Select(g => new { g.Key.Role, g.Key.Active, Count = g.Count() })
            .ToListAsync();
        var staff = accounts.Where(a => a.Role != Role.STUDENT).ToList();
        vm.StaffByRole = new[] { Role.STAFF, Role.MANAGER, Role.ADMIN }
            .ToDictionary(r => r, r => staff.Where(a => a.Role == r).Sum(a => a.Count));
        vm.StaffActive = staff.Where(a => a.Active).Sum(a => a.Count);
        vm.StaffLocked = staff.Where(a => !a.Active).Sum(a => a.Count);
        vm.StudentsActive = accounts.Where(a => a.Role == Role.STUDENT && a.Active).Sum(a => a.Count);
        vm.StudentsLocked = accounts.Where(a => a.Role == Role.STUDENT && !a.Active).Sum(a => a.Count);

        // Students per enrollment status (every status listed, 0 when nobody has it)
        var enrollment = await _db.StudentProfiles.AsNoTracking()
            .GroupBy(s => s.EnrollmentStatus)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();
        vm.StudentsByEnrollment = Enum.GetValues<EnrollmentStatus>()
            .ToDictionary(s => s, s => enrollment.Where(e => e.Status == s).Sum(e => e.Count));

        // Competitions: there are only a few dozen rows, so the status is calculated in memory with the
        // shared resolver (the stored status may be stale until the status sync job has run)
        var competitions = await _db.Competitions.AsNoTracking()
            .Select(c => new Competition { Id = c.Id, Status = c.Status, StartDate = c.StartDate, EndDate = c.EndDate })
            .ToListAsync();
        var competitionStatus = competitions.ToDictionary(c => c.Id, c => CompetitionStatusResolver.Resolve(c, day));
        vm.CompetitionsByStatus = Enum.GetValues<CompetitionStatus>()
            .ToDictionary(s => s, s => competitionStatus.Values.Count(v => v == s));

        // Submissions: total, and the ones still waiting for an evaluation in competitions that have ended
        var closedIds = competitionStatus.Where(kv => kv.Value == CompetitionStatus.CLOSED).Select(kv => kv.Key).ToList();
        vm.TotalSubmissions = await _db.Submissions.CountAsync();
        vm.SubmissionsAwaitingEvaluation = await _db.Submissions
            .CountAsync(s => closedIds.Contains(s.CompetitionId) && s.Evaluation == null);

        // Exhibitions: status calculated from the dates in the same way
        var exhibitions = await _db.Exhibitions.AsNoTracking()
            .Select(x => new Exhibition { StartDate = x.StartDate, EndDate = x.EndDate })
            .ToListAsync();
        var exhibitionStatus = exhibitions.Select(x => CompetitionStatusResolver.Resolve(x, day)).ToList();
        vm.ExhibitionsByStatus = Enum.GetValues<ExhibitionStatus>()
            .ToDictionary(s => s, s => exhibitionStatus.Count(v => v == s));

        // Most recent logins of any role, newest first
        vm.RecentLogins = await _db.Accounts.AsNoTracking()
            .Where(a => a.LastLoginAt != null)
            .OrderByDescending(a => a.LastLoginAt)
            .Take(RecentLoginCount)
            .Select(a => new RecentLoginVm
            {
                Username = a.Username,
                FullName = a.FullName,
                Role = a.Role,
                LastLoginAt = a.LastLoginAt!.Value,
            })
            .ToListAsync();

        return vm;
    }
}
