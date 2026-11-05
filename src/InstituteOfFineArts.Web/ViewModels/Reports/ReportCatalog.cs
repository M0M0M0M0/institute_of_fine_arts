using InstituteOfFineArts.Web.Helpers;

namespace InstituteOfFineArts.Web.ViewModels.Reports;

/// <summary>
/// One report of the report index /reports (BA doc §7): code, title, one-line description, URL and the roles
/// that may open it.
/// </summary>
/// <param name="Code">Report code, e.g. RPT-01</param>
/// <param name="Title">Report title</param>
/// <param name="Description">One-line description shown under the title</param>
/// <param name="Url">Report page URL</param>
/// <param name="Roles">Roles allowed to open the report (same as the [Authorize] attribute of its action)</param>
public record ReportLinkVm(string Code, string Title, string Description, string Url, IReadOnlyList<string> Roles);

/// <summary>
/// The 7 reports of the BA doc §7 (F-M10-01..07). RPT-01, 02, 03, 06 are built by ReportsController, RPT-04, 05,
/// 07 by FinanceReportsController; the index only lists the reports the current user's role may open (F-M10-08).
/// </summary>
public static class ReportCatalog
{
    /// <summary>Roles of RPT-01..06 (role matrix §2.3: STAFF and MANAGER).</summary>
    public const string StaffAndManager = RoleNames.Staff + "," + RoleNames.Manager;

    /// <summary>Roles of RPT-07 (role matrix §2.3: ADMIN and MANAGER).</summary>
    public const string AdminAndManager = RoleNames.Admin + "," + RoleNames.Manager;

    /// <summary>Roles of the report index: anyone who may open at least one report.</summary>
    public const string AnyReportViewer = RoleNames.Staff + "," + RoleNames.Manager + "," + RoleNames.Admin;

    /// <summary>Every report in code order.</summary>
    public static readonly IReadOnlyList<ReportLinkVm> All = new[]
    {
        new ReportLinkVm("RPT-01", "Submissions by rating",
            "Submissions of one competition in a date range, filtered by rating and class, with the count per rating.",
            "/reports/rpt-01", Split(StaffAndManager)),
        new ReportLinkVm("RPT-02", "Participation & ratings per competition",
            "Per competition: submissions, % participation of active students, count per rating, and who has not submitted.",
            "/reports/rpt-02", Split(StaffAndManager)),
        new ReportLinkVm("RPT-03", "Awards issued",
            "Awards issued by competition, issued date and student, with the total prize value.",
            "/reports/rpt-03", Split(StaffAndManager)),
        new ReportLinkVm("RPT-04", "Exhibition paintings & sales",
            "Paintings sent to exhibitions with asking and sold prices, and the sales totals.",
            "/reports/rpt-04", Split(StaffAndManager)),
        new ReportLinkVm("RPT-05", "Payments due to students",
            "Sold paintings whose money has not been paid to the student yet.",
            "/reports/rpt-05", Split(StaffAndManager)),
        new ReportLinkVm("RPT-06", "Student achievement profile",
            "One student's entries, ratings, awards, exhibitions and total sales in a period.",
            "/reports/rpt-06", Split(StaffAndManager)),
        new ReportLinkVm("RPT-07", "Account statistics",
            "Accounts per role and status, and students per class and course.",
            "/reports/rpt-07", Split(AdminAndManager)),
    };

    /// <summary>Turns a comma separated role list into a list.</summary>
    /// <param name="roles">e.g. "STAFF,MANAGER"</param>
    private static string[] Split(string roles) => roles.Split(',');
}
