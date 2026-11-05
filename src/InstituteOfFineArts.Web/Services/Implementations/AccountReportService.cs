using InstituteOfFineArts.Web.Data;
using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.Services.Interfaces;
using InstituteOfFineArts.Web.ViewModels.Reports;
using Microsoft.EntityFrameworkCore;

namespace InstituteOfFineArts.Web.Services.Implementations;

/// <summary>
/// Account statistics (task BE2-12; F-M10-07; BA §7 RPT-07): accounts per role × active / inactive and students per
/// course / class × enrollment status. Counting is done by GROUP BY in the database (a handful of rows come back).
/// </summary>
public class AccountReportService : IAccountReportService
{
    /// <summary>Database access.</summary>
    private readonly AppDbContext _db;

    /// <summary>Receives the database context through Dependency Injection.</summary>
    public AccountReportService(AppDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<AccountStatisticsReportVm> GetAccountStatisticsAsync(AccountStatisticsSearchVm search)
    {
        var vm = new AccountStatisticsReportVm();

        // Accounts per role and status; the status filter keeps only one of the two columns
        var accounts = _db.Accounts.AsNoTracking().AsQueryable();
        if (search.Role is { } role)
        {
            accounts = accounts.Where(a => a.Role == role);
        }
        if (search.Active is { } active)
        {
            accounts = accounts.Where(a => a.Active == active);
        }
        var counts = await accounts
            .GroupBy(a => new { a.Role, a.Active })
            .Select(g => new { g.Key.Role, g.Key.Active, Count = g.Count() })
            .ToListAsync();

        // Every role listed in a fixed order (0 when nobody has it), or only the chosen role
        var roles = search.Role is { } chosen ? new[] { chosen } : new[] { Role.ADMIN, Role.MANAGER, Role.STAFF, Role.STUDENT };
        vm.Roles = roles.Select(r => new AccountRoleRowVm
        {
            Role = r,
            Active = counts.Where(c => c.Role == r && c.Active).Sum(c => c.Count),
            Inactive = counts.Where(c => c.Role == r && !c.Active).Sum(c => c.Count),
        }).ToList();

        // Students per course / class only make sense when students are included
        vm.ShowStudents = search.Role is null or Role.STUDENT;
        if (!vm.ShowStudents)
        {
            return vm;
        }

        var students = _db.StudentProfiles.AsNoTracking().AsQueryable();
        if (search.Active is { } studentActive)
        {
            students = students.Where(s => s.Account.Active == studentActive);
        }

        // Class / course: part of either name
        if (!string.IsNullOrWhiteSpace(search.ClassOrCourse))
        {
            var keyword = search.ClassOrCourse.Trim();
            students = students.Where(s => s.CourseName.Contains(keyword) || (s.ClassName != null && s.ClassName.Contains(keyword)));
        }
        var groups = await students
            .GroupBy(s => new { s.CourseName, s.ClassName, s.EnrollmentStatus })
            .Select(g => new { g.Key.CourseName, g.Key.ClassName, g.Key.EnrollmentStatus, Count = g.Count() })
            .ToListAsync();

        // One row per course / class with every enrollment status as a column
        vm.Classes = groups
            .GroupBy(g => new { g.CourseName, g.ClassName })
            .OrderBy(g => g.Key.CourseName).ThenBy(g => g.Key.ClassName ?? string.Empty)
            .Select(g => new StudentClassRowVm
            {
                CourseName = g.Key.CourseName,
                ClassName = g.Key.ClassName,
                ByStatus = Enum.GetValues<EnrollmentStatus>()
                    .ToDictionary(s => s, s => g.Where(x => x.EnrollmentStatus == s).Sum(x => x.Count)),
            })
            .ToList();
        return vm;
    }
}
