namespace InstituteOfFineArts.Web.Models.Enums;

/// <summary>
/// Role of an account (each account has exactly one role – BR-03).
/// </summary>
/// <remarks>
/// Member names are UPPER CASE so they match the strings stored in the database (VARCHAR column + CHECK constraint)
/// and the role names used in [Authorize(Roles = "...")].
/// </remarks>
public enum Role
{
    /// <summary>Administrator: manages staff, teacher and student accounts, the audit log</summary>
    ADMIN,

    /// <summary>Staff: runs the institute – competitions, awards, exhibitions, sales, dashboard and reports. Does not grade.</summary>
    STAFF,

    /// <summary>Teacher: the subject expert who grades entries once the competition is closed (jury member)</summary>
    TEACHER,

    /// <summary>Student: submits paintings, views results</summary>
    STUDENT
}
