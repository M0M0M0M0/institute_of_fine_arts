using InstituteOfFineArts.Web.Models.Enums;

namespace InstituteOfFineArts.Web.ViewModels.Reports;

/// <summary>
/// Filters of RPT-07 "Account statistics" (F-M10-07, BA §7): role, account status, class / course.
/// </summary>
public class AccountStatisticsSearchVm
{
    /// <summary>Role (null = every role).</summary>
    public Role? Role { get; set; }

    /// <summary>true = active accounts only, false = locked accounts only, null = both.</summary>
    public bool? Active { get; set; }

    /// <summary>Part of the class or course name (student table only).</summary>
    public string? ClassOrCourse { get; set; }
}

/// <summary>Accounts of one role split by status.</summary>
public class AccountRoleRowVm
{
    /// <summary>Role.</summary>
    public Role Role { get; set; }

    /// <summary>Active accounts.</summary>
    public int Active { get; set; }

    /// <summary>Locked (inactive) accounts.</summary>
    public int Inactive { get; set; }

    /// <summary>Active + inactive.</summary>
    public int Total => Active + Inactive;
}

/// <summary>Students of one course / class split by enrollment status.</summary>
public class StudentClassRowVm
{
    /// <summary>Course name.</summary>
    public string CourseName { get; set; } = string.Empty;

    /// <summary>Class name (null = no class).</summary>
    public string? ClassName { get; set; }

    /// <summary>Number of students per enrollment status (every status present, 0 when nobody has it).</summary>
    public Dictionary<EnrollmentStatus, int> ByStatus { get; set; } = new();

    /// <summary>Students in this course / class.</summary>
    public int Total => ByStatus.Values.Sum();
}

/// <summary>Result of RPT-07.</summary>
public class AccountStatisticsReportVm
{
    /// <summary>One row per role (only the filtered role when a role is chosen).</summary>
    public List<AccountRoleRowVm> Roles { get; set; } = new();

    /// <summary>Whether the student table applies (no role or STUDENT chosen).</summary>
    public bool ShowStudents { get; set; }

    /// <summary>Students per course / class.</summary>
    public List<StudentClassRowVm> Classes { get; set; } = new();

    /// <summary>Total active accounts.</summary>
    public int TotalActive => Roles.Sum(r => r.Active);

    /// <summary>Total inactive accounts.</summary>
    public int TotalInactive => Roles.Sum(r => r.Inactive);

    /// <summary>Total accounts.</summary>
    public int TotalAccounts => TotalActive + TotalInactive;

    /// <summary>Total students per enrollment status over every class.</summary>
    public Dictionary<EnrollmentStatus, int> StudentsByStatus => Enum.GetValues<EnrollmentStatus>()
        .ToDictionary(s => s, s => Classes.Sum(c => c.ByStatus.GetValueOrDefault(s)));

    /// <summary>Total students in the class table.</summary>
    public int TotalStudents => Classes.Sum(c => c.Total);
}
