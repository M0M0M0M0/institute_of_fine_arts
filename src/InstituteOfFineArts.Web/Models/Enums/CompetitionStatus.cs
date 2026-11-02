namespace InstituteOfFineArts.Web.Models.Enums;

/// <summary>
/// Competition status. UPCOMING/ONGOING/CLOSED are derived from the dates (BR-07); GRADED is set only when staff publish the results (BR-08).
/// </summary>
/// <remarks>
/// Member names are UPPER CASE so they match the strings stored in the database (VARCHAR column + CHECK constraint)
/// and the role names used in [Authorize(Roles = "...")].
/// </remarks>
public enum CompetitionStatus
{
    /// <summary>Not started yet (before the start date)</summary>
    UPCOMING,

    /// <summary>Open – students can submit, edit and delete entries</summary>
    ONGOING,

    /// <summary>Submission deadline has passed, grading in progress</summary>
    CLOSED,

    /// <summary>Results have been published</summary>
    GRADED
}
