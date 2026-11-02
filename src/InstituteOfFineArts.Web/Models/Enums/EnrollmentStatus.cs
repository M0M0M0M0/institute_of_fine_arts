namespace InstituteOfFineArts.Web.Models.Enums;

/// <summary>
/// Study status of a student.
/// </summary>
/// <remarks>
/// Member names are UPPER CASE so they match the strings stored in the database (VARCHAR column + CHECK constraint)
/// and the role names used in [Authorize(Roles = "...")].
/// </remarks>
public enum EnrollmentStatus
{
    /// <summary>Currently studying</summary>
    STUDYING,

    /// <summary>Graduated</summary>
    GRADUATED,

    /// <summary>Suspended / on hold</summary>
    SUSPENDED,

    /// <summary>Dropped out</summary>
    DROPPED
}
