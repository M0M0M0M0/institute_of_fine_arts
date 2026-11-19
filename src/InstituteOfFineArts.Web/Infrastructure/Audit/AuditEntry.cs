using InstituteOfFineArts.Web.Models.Enums;

namespace InstituteOfFineArts.Web.Infrastructure.Audit;

/// <summary>
/// One row to write to audit_log, with the objects it belongs to so it can be shown in the history of the
/// competition, exhibition, entry or account concerned (not only in one flat list).
/// </summary>
public sealed record AuditEntry
{
    /// <summary>Technical action, e.g. "Competitions/Edit", "VIEW", "LOGIN".</summary>
    public required string Action { get; init; }

    /// <summary>Kind of work: decides in which history the row appears.</summary>
    public AuditCategory Category { get; init; } = AuditCategory.OTHER;

    /// <summary>Readable sentence, e.g. "Edited the competition “Wildlife”".</summary>
    public string? Summary { get; init; }

    /// <summary>Outcome, path and the changed fields (never passwords or file contents).</summary>
    public string? Details { get; init; }

    /// <summary>Kind of object the action was done to, e.g. "Competition", "ExhibitionEntry".</summary>
    public string? Entity { get; init; }

    /// <summary>Id of that object.</summary>
    public long? EntityId { get; init; }

    /// <summary>Competition it belongs to.</summary>
    public long? CompetitionId { get; init; }

    /// <summary>Exhibition it belongs to.</summary>
    public long? ExhibitionId { get; init; }

    /// <summary>Entry it is about.</summary>
    public long? SubmissionId { get; init; }

    /// <summary>Account the action was done to.</summary>
    public long? TargetAccountId { get; init; }

    /// <summary>
    /// When set, the row is skipped if the same user already has a row with the same action and object within this
    /// time (opening the same entry ten times in a minute is one visit).
    /// </summary>
    public TimeSpan? DedupeWindow { get; init; }
}

/// <summary>Keys of HttpContext.Items shared by the controllers and <see cref="Filters.AuditActionFilter"/>.</summary>
public static class AuditItems
{
    /// <summary>List&lt;string&gt; of changed fields ("End date: 20 Nov 2026 → 25 Nov 2026") added to the row's details.</summary>
    public const string Changes = "audit.changes";

    /// <summary>Set to true when the action wrote its own row, so the filter does not write a second one.</summary>
    public const string Done = "audit.done";
}

/// <summary>
/// Marks a GET action whose use must be logged for staff, teachers and administrators: opening or downloading an
/// entry (who looked at which painting). Students and guests are not logged (their own / public pages).
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class AuditViewAttribute : Attribute
{
    /// <summary>Creates the marker.</summary>
    /// <param name="what">"view" or "download"</param>
    public AuditViewAttribute(string what = "view")
    {
        What = what;
    }

    /// <summary>"view" or "download".</summary>
    public string What { get; }
}
