using InstituteOfFineArts.Web.Models.Enums;

namespace InstituteOfFineArts.Web.Services.Interfaces;

/// <summary>
/// Finds what a request acts on (from its path, e.g. /staff/exhibition-entries/7/sold) and the objects that object
/// belongs to (the artwork 7 → its exhibition, its entry and the entry's competition), so the log row can be shown
/// in every history concerned. Also builds the readable sentence of the row.
/// </summary>
public interface IAuditTargetService
{
    /// <summary>Reads the target from the path; ids missing in the path can come from the form (ExhibitionId…).</summary>
    /// <param name="method">HTTP method</param>
    /// <param name="path">Request path</param>
    /// <param name="formIds">CompetitionId / ExhibitionId / SubmissionId posted in the form (never other fields)</param>
    Task<AuditTarget> ResolveAsync(string method, string path, IReadOnlyDictionary<string, long>? formIds = null);

    /// <summary>Fills the parents and the label of an object created by the request (id taken from the redirect).</summary>
    /// <param name="target">Target found before the action ran</param>
    /// <param name="createdId">Id of the new object</param>
    Task<AuditTarget> WithCreatedIdAsync(AuditTarget target, long createdId);
}

/// <summary>Object of one request and where it belongs.</summary>
public sealed record AuditTarget
{
    /// <summary>Kind: Competition, CompetitionAward, AwardIssued, Submission, Exhibition, ExhibitionEntry, Customer, StudentProfile, StaffProfile; null when unknown.</summary>
    public string? Kind { get; init; }

    /// <summary>Id of the object (null for a create before it ran).</summary>
    public long? Id { get; init; }

    /// <summary>Name / title used in the sentence, e.g. “Wildlife”.</summary>
    public string? Label { get; init; }

    /// <summary>Competition it belongs to.</summary>
    public long? CompetitionId { get; init; }

    /// <summary>Exhibition it belongs to.</summary>
    public long? ExhibitionId { get; init; }

    /// <summary>Entry it is about.</summary>
    public long? SubmissionId { get; init; }

    /// <summary>Account it is about (student / staff profile).</summary>
    public long? TargetAccountId { get; init; }

    /// <summary>Kind of work.</summary>
    public AuditCategory Category { get; init; } = AuditCategory.OTHER;

    /// <summary>Readable sentence without the user, e.g. "Recorded the sale of the artwork “Old Town”".</summary>
    public string Summary { get; init; } = string.Empty;
}
