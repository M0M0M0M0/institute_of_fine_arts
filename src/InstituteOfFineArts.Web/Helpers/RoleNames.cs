namespace InstituteOfFineArts.Web.Helpers;

/// <summary>
/// Tên vai trò dạng hằng số, dùng trong [Authorize(Roles = RoleNames.Staff)] và khi tạo Claims.
/// Giá trị trùng với enum Role và giá trị lưu trong cột account.role.
/// </summary>
public static class RoleNames
{
    /// <summary>Quản trị viên.</summary>
    public const string Admin = "ADMIN";

    /// <summary>Quản lý.</summary>
    public const string Manager = "MANAGER";

    /// <summary>Giáo viên / nhân viên.</summary>
    public const string Staff = "STAFF";

    /// <summary>Học viên.</summary>
    public const string Student = "STUDENT";

    /// <summary>Nhóm vai trò được xem báo cáo (dùng cho ReportsController).</summary>
    public const string ReportViewers = Staff + "," + Manager + "," + Admin;
}
