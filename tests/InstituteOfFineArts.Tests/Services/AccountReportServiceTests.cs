using InstituteOfFineArts.Web.Data;
using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.Services.Implementations;
using InstituteOfFineArts.Web.ViewModels.Reports;
using Microsoft.EntityFrameworkCore;

namespace InstituteOfFineArts.Tests.Services;

/// <summary>
/// AccountReportService (task BE2-12; F-M10-07; BA §7 RPT-07): accounts per role × active / inactive with totals,
/// students per course / class × enrollment status, filters role / status / class or course.
/// </summary>
public class AccountReportServiceTests : IDisposable
{
    /// <summary>In-memory database.</summary>
    private readonly AppDbContext _db = new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    /// <summary>
    /// 1 admin, 1 manager, 2 staff (one locked); students: Painting/P1 STUDYING ×2 (one locked), Painting/P1 GRADUATED,
    /// Painting/no class SUSPENDED, Animation/A1 STUDYING.
    /// </summary>
    public AccountReportServiceTests()
    {
        _db.Accounts.AddRange(
            NewAccount("admin01", Role.ADMIN),
            NewAccount("manager01", Role.MANAGER),
            NewAccount("staff01", Role.STAFF),
            NewAccount("staff02", Role.STAFF, active: false));
        _db.StudentProfiles.AddRange(
            NewStudent("HV0001", "Painting", "P1", EnrollmentStatus.STUDYING),
            NewStudent("HV0002", "Painting", "P1", EnrollmentStatus.STUDYING, active: false),
            NewStudent("HV0003", "Painting", "P1", EnrollmentStatus.GRADUATED),
            NewStudent("HV0004", "Painting", null, EnrollmentStatus.SUSPENDED),
            NewStudent("HV0005", "Animation", "A1", EnrollmentStatus.STUDYING));
        _db.SaveChanges();
    }

    /// <summary>Service under test.</summary>
    private AccountReportService Service => new(_db);

    /// <summary>Disposes the test database.</summary>
    public void Dispose() => _db.Dispose();

    /// <summary>F-M10-07: every role listed with active / inactive counts and the totals.</summary>
    [Fact]
    public async Task Accounts_PerRoleAndStatus()
    {
        var report = await Service.GetAccountStatisticsAsync(new AccountStatisticsSearchVm());

        Assert.Equal(new[] { Role.ADMIN, Role.MANAGER, Role.STAFF, Role.STUDENT }, report.Roles.Select(r => r.Role));
        var staff = report.Roles.Single(r => r.Role == Role.STAFF);
        Assert.Equal((1, 1), (staff.Active, staff.Inactive));
        var students = report.Roles.Single(r => r.Role == Role.STUDENT);
        Assert.Equal((4, 1), (students.Active, students.Inactive));
        Assert.Equal(7, report.TotalActive);
        Assert.Equal(2, report.TotalInactive);
        Assert.Equal(9, report.TotalAccounts);
    }

    /// <summary>F-M10-07: students per course / class with every enrollment status and the totals.</summary>
    [Fact]
    public async Task Students_PerClassAndEnrollment()
    {
        var report = await Service.GetAccountStatisticsAsync(new AccountStatisticsSearchVm());

        Assert.True(report.ShowStudents);
        Assert.Equal(new[] { ("Animation", "A1"), ("Painting", (string?)null), ("Painting", "P1") },
            report.Classes.Select(c => (c.CourseName, c.ClassName)));
        var p1 = report.Classes.Single(c => c.ClassName == "P1");
        Assert.Equal(2, p1.ByStatus[EnrollmentStatus.STUDYING]);
        Assert.Equal(1, p1.ByStatus[EnrollmentStatus.GRADUATED]);
        Assert.Equal(0, p1.ByStatus[EnrollmentStatus.DROPPED]);
        Assert.Equal(3, p1.Total);
        Assert.Equal(5, report.TotalStudents);
        Assert.Equal(3, report.StudentsByStatus[EnrollmentStatus.STUDYING]);
    }

    /// <summary>Filters: role keeps one row and hides the student table for staff roles; status applies to both tables.</summary>
    [Fact]
    public async Task Filters_RoleAndStatus()
    {
        var staffOnly = await Service.GetAccountStatisticsAsync(new AccountStatisticsSearchVm { Role = Role.STAFF });
        var inactive = await Service.GetAccountStatisticsAsync(new AccountStatisticsSearchVm { Active = false });

        Assert.Equal(Role.STAFF, Assert.Single(staffOnly.Roles).Role);
        Assert.False(staffOnly.ShowStudents);
        Assert.Empty(staffOnly.Classes);
        Assert.Equal(2, inactive.TotalAccounts);
        Assert.Equal(0, inactive.TotalActive);
        Assert.Equal(1, Assert.Single(inactive.Classes).ByStatus[EnrollmentStatus.STUDYING]);
    }

    /// <summary>Filter: class or course keyword matches either name.</summary>
    [Fact]
    public async Task Filters_ClassOrCourse()
    {
        var byCourse = await Service.GetAccountStatisticsAsync(new AccountStatisticsSearchVm { ClassOrCourse = " Anim " });
        var byClass = await Service.GetAccountStatisticsAsync(new AccountStatisticsSearchVm { ClassOrCourse = "P1" });
        var none = await Service.GetAccountStatisticsAsync(new AccountStatisticsSearchVm { ClassOrCourse = "Sculpture" });

        Assert.Equal("Animation", Assert.Single(byCourse.Classes).CourseName);
        Assert.Equal(3, Assert.Single(byClass.Classes).Total);
        Assert.Empty(none.Classes);
    }

    /// <summary>New account with a role.</summary>
    private static Account NewAccount(string username, Role role, bool active = true) => new()
    {
        Username = username, FullName = username, Email = username + "@ifa.local", Role = role, Active = active, PasswordHash = "x",
    };

    /// <summary>New student profile with its account.</summary>
    private static StudentProfile NewStudent(string code, string course, string? className, EnrollmentStatus status, bool active = true) => new()
    {
        StudentCode = code, EnrollmentDate = new DateOnly(2025, 9, 1), CourseName = course, ClassName = className,
        EnrollmentStatus = status, Account = NewAccount(code.ToLowerInvariant(), Role.STUDENT, active),
    };
}
