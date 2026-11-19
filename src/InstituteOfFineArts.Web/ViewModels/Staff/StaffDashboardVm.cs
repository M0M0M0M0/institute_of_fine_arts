using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.ViewModels.Common;

namespace InstituteOfFineArts.Web.ViewModels.Staff;

/// <summary>Time range of the activity figures on the staff dashboard (?period=month|year|all).</summary>
public enum DashboardPeriod
{
    /// <summary>Everything since the start (default; no comparison).</summary>
    All,

    /// <summary>The current calendar month, compared with the previous month.</summary>
    Month,

    /// <summary>The current calendar year, compared with the previous year.</summary>
    Year,
}

/// <summary>
/// Numbers shown on the staff dashboard (/staff, task BE2-13; F-M11-01, US-15 AC3, SCR-50):
/// students, staff, competitions by status, submissions, awards issued, paintings sold and total sales.
/// Every dictionary contains all values of its enum (0 when there is no row), so the view never has to check for missing keys.
/// <para>
/// People and competition statuses are the state today. Submissions, awards and sales are activity: they count only the
/// chosen <see cref="Period"/> (by submission date, issue date and sale date) and, for a month or a year, the same
/// figure of the previous period is given for comparison.
/// </para>
/// </summary>
public class StaffDashboardVm
{
    /// <summary>Time range of the activity figures.</summary>
    public DashboardPeriod Period { get; set; } = DashboardPeriod.All;

    /// <summary>Name of the previous period ("last month" / "last year"), null for <see cref="DashboardPeriod.All"/>.</summary>
    public string? PreviousLabel { get; set; }

    /// <summary>Work waiting (entries to grade, competitions to publish, locked accounts…); empty when nothing waits.</summary>
    public List<AttentionItemVm> Attention { get; set; } = new();

    /// <summary>All students (student profiles, active and locked).</summary>
    public int StudentsTotal { get; set; }

    /// <summary>Students whose account is active.</summary>
    public int StudentsActive { get; set; }

    /// <summary>Students per enrollment status.</summary>
    public IReadOnlyDictionary<EnrollmentStatus, int> StudentsByEnrollment { get; set; } = new Dictionary<EnrollmentStatus, int>();

    /// <summary>Staff profiles per role (STAFF, TEACHER, ADMIN).</summary>
    public IReadOnlyDictionary<Role, int> StaffByRole { get; set; } = new Dictionary<Role, int>();

    /// <summary>All staff profiles (the three staff roles).</summary>
    public int StaffTotal => StaffByRole.Values.Sum();

    /// <summary>Competitions per calculated status (CompetitionStatusResolver, resolved for today).</summary>
    public IReadOnlyDictionary<CompetitionStatus, int> CompetitionsByStatus { get; set; } = new Dictionary<CompetitionStatus, int>();

    /// <summary>All competitions.</summary>
    public int CompetitionsTotal => CompetitionsByStatus.Values.Sum();

    /// <summary>Submissions received in the period.</summary>
    public int SubmissionsTotal { get; set; }

    /// <summary>Submissions of the period that have an evaluation.</summary>
    public int SubmissionsEvaluated { get; set; }

    /// <summary>Submissions received in the previous period (null when there is no comparison).</summary>
    public int? SubmissionsPrevious { get; set; }

    /// <summary>Awards issued in the period (rows of award_issued).</summary>
    public int AwardsIssued { get; set; }

    /// <summary>Total prize value of the awards issued in the period (USD).</summary>
    public decimal AwardsPrizeTotal { get; set; }

    /// <summary>Awards issued in the previous period (null when there is no comparison).</summary>
    public int? AwardsPrevious { get; set; }

    /// <summary>Paintings sold in the period (entries with status SOLD, by sale date).</summary>
    public int PaintingsSold { get; set; }

    /// <summary>Total sale price of the paintings sold in the period (USD).</summary>
    public decimal TotalSales { get; set; }

    /// <summary>Paid to students for the paintings sold in the period (USD, BR-45).</summary>
    public decimal PaidToStudents { get; set; }

    /// <summary>
    /// Sales of the period not paid to the student yet (USD). The institute takes no commission (BR-45): the whole sale
    /// price goes to the student, so Total sales − Paid to students is money still owed, not income.
    /// </summary>
    public decimal UnpaidToStudents => TotalSales - PaidToStudents;

    /// <summary>Total sales of the previous period (null when there is no comparison).</summary>
    public decimal? SalesPrevious { get; set; }

    /// <summary>Years that have competitions, newest first (choices of the year filter of the submissions chart).</summary>
    public List<int> CompetitionYears { get; set; } = new();
}
