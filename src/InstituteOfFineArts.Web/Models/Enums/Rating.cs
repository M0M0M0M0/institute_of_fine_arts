namespace InstituteOfFineArts.Web.Models.Enums;

/// <summary>
/// Rating given to a submission by staff.
/// </summary>
/// <remarks>
/// Member names are UPPER CASE so they match the strings stored in the database (VARCHAR column + CHECK constraint)
/// and the role names used in [Authorize(Roles = "...")].
/// </remarks>
public enum Rating
{
    /// <summary>Best</summary>
    BEST,

    /// <summary>Better</summary>
    BETTER,

    /// <summary>Good</summary>
    GOOD,

    /// <summary>Moderate</summary>
    MODERATE,

    /// <summary>Normal</summary>
    NORMAL,

    /// <summary>Disqualified (rule violation or late submission)</summary>
    DISQUALIFIED
}
