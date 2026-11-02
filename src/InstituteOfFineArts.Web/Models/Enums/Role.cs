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
    /// <summary>Administrator: manages staff and student accounts</summary>
    ADMIN,

    /// <summary>Manager: views all data, manages exhibitions</summary>
    MANAGER,

    /// <summary>Staff/teacher: competitions, grading, awards, exhibitions</summary>
    STAFF,

    /// <summary>Student: submits paintings, views results</summary>
    STUDENT
}
