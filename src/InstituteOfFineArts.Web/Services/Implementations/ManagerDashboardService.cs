using InstituteOfFineArts.Web.Data;
using InstituteOfFineArts.Web.Helpers;
using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.Services.Interfaces;
using InstituteOfFineArts.Web.ViewModels.Manager;
using Microsoft.EntityFrameworkCore;

namespace InstituteOfFineArts.Web.Services.Implementations;

/// <summary>
/// Manager dashboard (task BE2-13; F-M11-01, US-15 AC3): read-only counts and money totals over the whole database.
/// </summary>
public class ManagerDashboardService : IManagerDashboardService
{
    /// <summary>Database access.</summary>
    private readonly AppDbContext _db;

    /// <summary>Receives the database context through Dependency Injection.</summary>
    public ManagerDashboardService(AppDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<ManagerDashboardVm> GetAsync(DateOnly? today = null)
    {
        var day = today ?? DateTimeHelper.Today;
        var vm = new ManagerDashboardVm();

        // Students grouped by enrollment status and account state: one small query for three numbers
        var students = await _db.StudentProfiles.AsNoTracking()
            .GroupBy(s => new { s.EnrollmentStatus, s.Account.Active })
            .Select(g => new { g.Key.EnrollmentStatus, g.Key.Active, Count = g.Count() })
            .ToListAsync();
        vm.StudentsTotal = students.Sum(s => s.Count);
        vm.StudentsActive = students.Where(s => s.Active).Sum(s => s.Count);
        vm.StudentsByEnrollment = Enum.GetValues<EnrollmentStatus>()
            .ToDictionary(e => e, e => students.Where(s => s.EnrollmentStatus == e).Sum(s => s.Count));

        // Staff profiles per account role (every staff role listed, 0 when nobody has it)
        var staff = await _db.StaffProfiles.AsNoTracking()
            .GroupBy(s => s.Account.Role)
            .Select(g => new { Role = g.Key, Count = g.Count() })
            .ToListAsync();
        vm.StaffByRole = new[] { Role.STAFF, Role.MANAGER, Role.ADMIN }
            .ToDictionary(r => r, r => staff.Where(s => s.Role == r).Sum(s => s.Count));

        // Competitions: only a few dozen rows, so the status is calculated in memory with the shared resolver
        // (the stored status may be stale until the status sync job has run; GRADED is stored)
        var competitions = await _db.Competitions.AsNoTracking()
            .Select(c => new Competition { Id = c.Id, Status = c.Status, StartDate = c.StartDate, EndDate = c.EndDate })
            .ToListAsync();
        var statuses = competitions.Select(c => CompetitionStatusResolver.Resolve(c, day)).ToList();
        vm.CompetitionsByStatus = Enum.GetValues<CompetitionStatus>()
            .ToDictionary(s => s, s => statuses.Count(v => v == s));

        // Submissions: total and evaluated
        vm.SubmissionsTotal = await _db.Submissions.CountAsync();
        vm.SubmissionsEvaluated = await _db.Evaluations.CountAsync();

        // Awards issued with their total prize value (the value is defined on the competition award)
        vm.AwardsIssued = await _db.AwardsIssued.CountAsync();
        vm.AwardsPrizeTotal = await _db.AwardsIssued.SumAsync(a => (decimal?)a.CompetitionAward.PrizeValue) ?? 0m;

        // Sales: SOLD entries only; SUM over no rows is NULL in SQL, hence the nullable sums
        var sold = _db.ExhibitionEntries.Where(e => e.Status == EntryStatus.SOLD);
        vm.PaintingsSold = await sold.CountAsync();
        vm.TotalSales = await sold.SumAsync(e => e.SoldPrice) ?? 0m;
        vm.PaidToStudents = await sold.Where(e => e.PaidToStudent).SumAsync(e => e.PaidAmount) ?? 0m;

        return vm;
    }
}
