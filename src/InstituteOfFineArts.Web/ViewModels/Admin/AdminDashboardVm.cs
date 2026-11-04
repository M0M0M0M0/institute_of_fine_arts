using InstituteOfFineArts.Web.Models.Enums;

namespace InstituteOfFineArts.Web.ViewModels.Admin;

/// <summary>
/// Numbers shown on the administrator dashboard (/admin, task BE2-04).
/// Every dictionary contains all values of its enum (0 when there is no row), so the view never has to check for missing keys.
/// </summary>
public class AdminDashboardVm
{
    /// <summary>Staff accounts per role (STAFF, MANAGER, ADMIN).</summary>
    public IReadOnlyDictionary<Role, int> StaffByRole { get; set; } = new Dictionary<Role, int>();

    /// <summary>Active staff accounts (all three staff roles).</summary>
    public int StaffActive { get; set; }

    /// <summary>Locked staff accounts (all three staff roles).</summary>
    public int StaffLocked { get; set; }

    /// <summary>Total staff accounts.</summary>
    public int StaffTotal => StaffActive + StaffLocked;

    /// <summary>Active student accounts.</summary>
    public int StudentsActive { get; set; }

    /// <summary>Locked student accounts.</summary>
    public int StudentsLocked { get; set; }

    /// <summary>Total student accounts.</summary>
    public int StudentsTotal => StudentsActive + StudentsLocked;

    /// <summary>Students per enrollment status.</summary>
    public IReadOnlyDictionary<EnrollmentStatus, int> StudentsByEnrollment { get; set; } = new Dictionary<EnrollmentStatus, int>();

    /// <summary>Competitions per calculated status (CompetitionStatusResolver).</summary>
    public IReadOnlyDictionary<CompetitionStatus, int> CompetitionsByStatus { get; set; } = new Dictionary<CompetitionStatus, int>();

    /// <summary>All submissions.</summary>
    public int TotalSubmissions { get; set; }

    /// <summary>Submissions without an evaluation in competitions that are CLOSED (ended, results not published).</summary>
    public int SubmissionsAwaitingEvaluation { get; set; }

    /// <summary>Exhibitions per calculated status.</summary>
    public IReadOnlyDictionary<ExhibitionStatus, int> ExhibitionsByStatus { get; set; } = new Dictionary<ExhibitionStatus, int>();

    /// <summary>The most recent logins, newest first.</summary>
    public IReadOnlyList<RecentLoginVm> RecentLogins { get; set; } = [];
}

/// <summary>One row of the "recent logins" table.</summary>
public class RecentLoginVm
{
    /// <summary>Login name.</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>Full name.</summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>Role of the account.</summary>
    public Role Role { get; set; }

    /// <summary>Time of the last login (institute time zone).</summary>
    public DateTime LastLoginAt { get; set; }
}
