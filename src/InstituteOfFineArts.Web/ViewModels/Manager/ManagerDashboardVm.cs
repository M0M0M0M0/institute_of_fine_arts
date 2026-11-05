using InstituteOfFineArts.Web.Models.Enums;

namespace InstituteOfFineArts.Web.ViewModels.Manager;

/// <summary>
/// Numbers shown on the manager dashboard (/manager, task BE2-13; F-M11-01, US-15 AC3, SCR-50):
/// students, staff, competitions by status, submissions, awards issued, paintings sold and total sales.
/// Every dictionary contains all values of its enum (0 when there is no row), so the view never has to check for missing keys.
/// </summary>
public class ManagerDashboardVm
{
    /// <summary>All students (student profiles, active and locked).</summary>
    public int StudentsTotal { get; set; }

    /// <summary>Students whose account is active.</summary>
    public int StudentsActive { get; set; }

    /// <summary>Students per enrollment status.</summary>
    public IReadOnlyDictionary<EnrollmentStatus, int> StudentsByEnrollment { get; set; } = new Dictionary<EnrollmentStatus, int>();

    /// <summary>Staff profiles per role (STAFF, MANAGER, ADMIN).</summary>
    public IReadOnlyDictionary<Role, int> StaffByRole { get; set; } = new Dictionary<Role, int>();

    /// <summary>All staff profiles (the three staff roles).</summary>
    public int StaffTotal => StaffByRole.Values.Sum();

    /// <summary>Competitions per calculated status (CompetitionStatusResolver, resolved for today).</summary>
    public IReadOnlyDictionary<CompetitionStatus, int> CompetitionsByStatus { get; set; } = new Dictionary<CompetitionStatus, int>();

    /// <summary>All competitions.</summary>
    public int CompetitionsTotal => CompetitionsByStatus.Values.Sum();

    /// <summary>All submissions.</summary>
    public int SubmissionsTotal { get; set; }

    /// <summary>Submissions that have an evaluation.</summary>
    public int SubmissionsEvaluated { get; set; }

    /// <summary>Awards issued (rows of award_issued).</summary>
    public int AwardsIssued { get; set; }

    /// <summary>Total prize value of the issued awards (USD).</summary>
    public decimal AwardsPrizeTotal { get; set; }

    /// <summary>Paintings sold at exhibitions (entries with status SOLD).</summary>
    public int PaintingsSold { get; set; }

    /// <summary>Total sale price of the sold paintings (USD).</summary>
    public decimal TotalSales { get; set; }

    /// <summary>Total amount already paid to students for sold paintings (USD, BR-45).</summary>
    public decimal PaidToStudents { get; set; }
}
