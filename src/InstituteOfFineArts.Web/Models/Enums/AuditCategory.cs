namespace InstituteOfFineArts.Web.Models.Enums;

/// <summary>
/// Kind of work an audit log row is about (audit_log.category). Lets each page show only its own history: the
/// login history of an account, the history of one competition, one exhibition, one entry.
/// </summary>
/// <remarks>
/// Member names are UPPER CASE so they match the strings stored in the database (VARCHAR column + CHECK constraint).
/// </remarks>
public enum AuditCategory
{
    /// <summary>Log in, failed log in, log out (web and mobile).</summary>
    LOGIN,

    /// <summary>Accounts and profiles: create, edit, lock, unlock, reset or change password, import, delete.</summary>
    ACCOUNT,

    /// <summary>Competitions and their awards: create, edit (with the changed fields), delete.</summary>
    COMPETITION,

    /// <summary>Entries: submitted, edited, withdrawn by the student; opened or downloaded by staff / teachers.</summary>
    SUBMISSION,

    /// <summary>Grading: evaluation saved, changed or deleted.</summary>
    GRADING,

    /// <summary>Awards given or revoked, results published.</summary>
    AWARD,

    /// <summary>Exhibitions and the paintings sent to them: create, edit, propose, accept, reject.</summary>
    EXHIBITION,

    /// <summary>Sales: sold, unsold, paid to the student, customers.</summary>
    SALE,

    /// <summary>Anything else (notifications, profile page…).</summary>
    OTHER,
}
