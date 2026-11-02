namespace InstituteOfFineArts.Web.Models.Enums;

/// <summary>
/// Type of text attached to a painting (BR-19).
/// </summary>
/// <remarks>
/// Member names are UPPER CASE so they match the strings stored in the database (VARCHAR column + CHECK constraint)
/// and the role names used in [Authorize(Roles = "...")].
/// </remarks>
public enum TextType
{
    /// <summary>Poem</summary>
    POEM,

    /// <summary>Quotation</summary>
    QUOTATION,

    /// <summary>Story</summary>
    STORY,

    /// <summary>Other</summary>
    OTHER
}
