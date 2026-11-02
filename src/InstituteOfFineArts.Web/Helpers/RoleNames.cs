namespace InstituteOfFineArts.Web.Helpers;

/// <summary>
/// Role names as constants, used in [Authorize(Roles = RoleNames.Staff)] and when creating claims.
/// The values match the Role enum and the values stored in column account.role.
/// </summary>
public static class RoleNames
{
    /// <summary>Administrator.</summary>
    public const string Admin = "ADMIN";

    /// <summary>Manager.</summary>
    public const string Manager = "MANAGER";

    /// <summary>Staff / teacher.</summary>
    public const string Staff = "STAFF";

    /// <summary>Student.</summary>
    public const string Student = "STUDENT";

    /// <summary>Roles allowed to view reports (used by ReportsController).</summary>
    public const string ReportViewers = Staff + "," + Manager + "," + Admin;
}
