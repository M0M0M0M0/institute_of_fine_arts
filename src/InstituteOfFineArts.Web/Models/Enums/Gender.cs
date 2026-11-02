namespace InstituteOfFineArts.Web.Models.Enums;

/// <summary>
/// Gender (stored as a single character in the database).
/// </summary>
/// <remarks>
/// Member names are UPPER CASE so they match the strings stored in the database (VARCHAR column + CHECK constraint)
/// and the role names used in [Authorize(Roles = "...")].
/// </remarks>
public enum Gender
{
    /// <summary>Male</summary>
    M,

    /// <summary>Female</summary>
    F,

    /// <summary>Other</summary>
    O
}
