namespace InstituteOfFineArts.Web.Models.Enums;

/// <summary>
/// Status of a painting sent to an exhibition.
/// </summary>
/// <remarks>
/// Member names are UPPER CASE so they match the strings stored in the database (VARCHAR column + CHECK constraint)
/// and the role names used in [Authorize(Roles = "...")].
/// </remarks>
public enum EntryStatus
{
    /// <summary>Proposed by the student, waiting for staff approval</summary>
    PENDING,

    /// <summary>Accepted into the exhibition</summary>
    ACCEPTED,

    /// <summary>Rejected (with a reason)</summary>
    REJECTED,

    /// <summary>Sold</summary>
    SOLD,

    /// <summary>Exhibition finished without a sale</summary>
    UNSOLD
}
