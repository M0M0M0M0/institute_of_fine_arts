namespace InstituteOfFineArts.Web.Models.Enums;

/// <summary>
/// Exhibition status, derived from the dates (BR-42).
/// </summary>
/// <remarks>
/// Member names are UPPER CASE so they match the strings stored in the database (VARCHAR column + CHECK constraint)
/// and the role names used in [Authorize(Roles = "...")].
/// </remarks>
public enum ExhibitionStatus
{
    /// <summary>Upcoming</summary>
    UPCOMING,

    /// <summary>Ongoing</summary>
    ONGOING,

    /// <summary>Finished</summary>
    FINISHED
}
