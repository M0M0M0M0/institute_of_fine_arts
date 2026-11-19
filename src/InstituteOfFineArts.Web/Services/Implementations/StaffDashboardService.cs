using InstituteOfFineArts.Web.Data;
using InstituteOfFineArts.Web.Helpers;
using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.Services.Interfaces;
using InstituteOfFineArts.Web.ViewModels.Staff;
using Microsoft.EntityFrameworkCore;

namespace InstituteOfFineArts.Web.Services.Implementations;

/// <summary>
/// Staff dashboard (/staff; task BE2-13, F-M11-01, US-15 AC3 – the former Manager overview): counts and money totals over the whole database,
/// the work waiting ("Needs attention") and the activity of a chosen period compared with the previous one.
/// </summary>
public class StaffDashboardService : IStaffDashboardService
{
    /// <summary>Database access.</summary>
    private readonly AppDbContext _db;

    /// <summary>Receives the database context through Dependency Injection.</summary>
    public StaffDashboardService(AppDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<StaffDashboardVm> GetAsync(DateOnly? today = null, DashboardPeriod period = DashboardPeriod.All)
    {
        var day = today ?? DateTimeHelper.Today;
        var vm = new StaffDashboardVm { Period = period };

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
            .GroupBy(s => new { s.Account.Role, s.Account.Active })
            .Select(g => new { g.Key.Role, g.Key.Active, Count = g.Count() })
            .ToListAsync();
        vm.StaffByRole = new[] { Role.STAFF, Role.TEACHER, Role.ADMIN }
            .ToDictionary(r => r, r => staff.Where(s => s.Role == r).Sum(s => s.Count));

        // Competitions: only a few dozen rows, so the status is calculated in memory with the shared resolver
        // (the stored status may be stale until the status sync job has run; GRADED is stored)
        var competitions = await _db.Competitions.AsNoTracking()
            .Select(c => new Competition { Id = c.Id, Status = c.Status, StartDate = c.StartDate, EndDate = c.EndDate })
            .ToListAsync();
        var statuses = competitions.Select(c => CompetitionStatusResolver.Resolve(c, day)).ToList();
        vm.CompetitionsByStatus = Enum.GetValues<CompetitionStatus>()
            .ToDictionary(s => s, s => statuses.Count(v => v == s));
        vm.CompetitionYears = competitions.Select(c => c.StartDate.Year).Distinct().OrderDescending().ToList();

        // Activity of the period (submission date, issue date, sale date) and of the previous period
        var (from, to) = Range(period, day, 0);
        await FillActivityAsync(vm, from, to);
        if (period != DashboardPeriod.All)
        {
            var (prevFrom, prevTo) = Range(period, day, -1);
            var previous = new StaffDashboardVm();
            await FillActivityAsync(previous, prevFrom, prevTo);
            vm.SubmissionsPrevious = previous.SubmissionsTotal;
            vm.AwardsPrevious = previous.AwardsIssued;
            vm.SalesPrevious = previous.TotalSales;
            vm.PreviousLabel = period == DashboardPeriod.Month ? "last month" : "last year";
        }

        vm.Attention = await AttentionItems.BuildAsync(_db, day);
        return vm;
    }

    /// <summary>
    /// First day (included) and last day (excluded) of the period that is <paramref name="offset"/> periods away from the
    /// one containing <paramref name="day"/>; (null, null) for <see cref="DashboardPeriod.All"/>.
    /// </summary>
    internal static (DateOnly? From, DateOnly? To) Range(DashboardPeriod period, DateOnly day, int offset)
        => period switch
        {
            DashboardPeriod.Month => (new DateOnly(day.Year, day.Month, 1).AddMonths(offset), new DateOnly(day.Year, day.Month, 1).AddMonths(offset + 1)),
            DashboardPeriod.Year => (new DateOnly(day.Year + offset, 1, 1), new DateOnly(day.Year + offset + 1, 1, 1)),
            _ => (null, null),
        };

    /// <summary>Submissions, awards and sales between <paramref name="from"/> (included) and <paramref name="to"/> (excluded).</summary>
    private async Task FillActivityAsync(StaffDashboardVm vm, DateOnly? from, DateOnly? to)
    {
        // Submissions (by submission time) and how many of them are evaluated
        var submissions = _db.Submissions.AsNoTracking();
        if (from is { } f && to is { } t)
        {
            var start = f.ToDateTime(TimeOnly.MinValue);
            var end = t.ToDateTime(TimeOnly.MinValue);
            submissions = submissions.Where(s => s.SubmittedAt >= start && s.SubmittedAt < end);
        }
        vm.SubmissionsTotal = await submissions.CountAsync();
        vm.SubmissionsEvaluated = await submissions.CountAsync(s => s.Evaluation != null);

        // Awards issued with their total prize value (the value is defined on the competition award)
        var awards = _db.AwardsIssued.AsNoTracking();
        if (from is { } af && to is { } at)
        {
            awards = awards.Where(a => a.IssuedDate >= af && a.IssuedDate < at);
        }
        vm.AwardsIssued = await awards.CountAsync();
        vm.AwardsPrizeTotal = await awards.SumAsync(a => (decimal?)a.CompetitionAward.PrizeValue) ?? 0m;

        // Sales: SOLD entries only (by sale date); SUM over no rows is NULL in SQL, hence the nullable sums
        var sold = _db.ExhibitionEntries.AsNoTracking().Where(e => e.Status == EntryStatus.SOLD);
        if (from is { } sf && to is { } st)
        {
            sold = sold.Where(e => e.SoldDate >= sf && e.SoldDate < st);
        }
        vm.PaintingsSold = await sold.CountAsync();
        vm.TotalSales = await sold.SumAsync(e => e.SoldPrice) ?? 0m;
        vm.PaidToStudents = await sold.Where(e => e.PaidToStudent).SumAsync(e => e.PaidAmount) ?? 0m;
    }
}
