namespace InstituteOfFineArts.Web.Models.Entities;

/// <summary>
/// Activity log row (F-M17-01): who did what and when – log in, grading, deletions, sales, …
/// Written only by the application; never edited afterwards (UpdatedAt stays null).
/// </summary>
public class AuditLog : BaseEntity
{
    /// <summary>Account that did the action; null for an anonymous action (e.g. failed log in).</summary>
    public long? AccountId { get; set; }

    /// <summary>User name at the time of the action (kept even if the account is renamed / deleted).</summary>
    public string? Username { get; set; }

    /// <summary>What was done, e.g. "LOGIN", "EVALUATE", "DELETE", "SELL".</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Kind of object concerned, e.g. "Submission", "ExhibitionEntry".</summary>
    public string? Entity { get; set; }

    /// <summary>Id of the object concerned.</summary>
    public long? EntityId { get; set; }

    /// <summary>Short description (never passwords or file contents).</summary>
    public string? Details { get; set; }

    /// <summary>Client IP address.</summary>
    public string? IpAddress { get; set; }

    /// <summary>Account that did the action.</summary>
    public Account? Account { get; set; }
}
