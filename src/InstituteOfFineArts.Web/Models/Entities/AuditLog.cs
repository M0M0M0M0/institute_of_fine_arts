using InstituteOfFineArts.Web.Models.Enums;

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

    /// <summary>What was done, e.g. "LOGIN", "Competitions/Edit", "VIEW".</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Kind of work, used to group the history on each page (login history, competition history…).</summary>
    public AuditCategory Category { get; set; } = AuditCategory.OTHER;

    /// <summary>Readable sentence shown in the history, e.g. "Edited the competition “Wildlife”".</summary>
    public string? Summary { get; set; }

    /// <summary>Competition the action belongs to (also for its entries, awards and evaluations); null when none.</summary>
    /// <remarks>No foreign key: the history must stay readable after the competition is deleted.</remarks>
    public long? CompetitionId { get; set; }

    /// <summary>Exhibition the action belongs to (also for its artworks and sales); null when none.</summary>
    public long? ExhibitionId { get; set; }

    /// <summary>Entry the action is about (viewed, graded, awarded, exhibited…); null when none.</summary>
    public long? SubmissionId { get; set; }

    /// <summary>Account the action was done to (e.g. the student an administrator locked); null when none.</summary>
    public long? TargetAccountId { get; set; }

    /// <summary>Kind of object concerned, e.g. "Submission", "ExhibitionEntry".</summary>
    public string? Entity { get; set; }

    /// <summary>Id of the object concerned.</summary>
    public long? EntityId { get; set; }

    /// <summary>Extra details: outcome, path, the fields that changed (never passwords or file contents).</summary>
    public string? Details { get; set; }

    /// <summary>Client IP address.</summary>
    public string? IpAddress { get; set; }

    /// <summary>Account that did the action.</summary>
    public Account? Account { get; set; }
}
