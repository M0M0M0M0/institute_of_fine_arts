using System.ComponentModel.DataAnnotations;
using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.ViewModels.Common;

namespace InstituteOfFineArts.Web.ViewModels.Admin.AuditLog;

/// <summary>Filters of the audit log page (F-M17-01).</summary>
public class AuditLogSearchVm
{
    /// <summary>Part of the user name.</summary>
    [StringLength(50)]
    [Display(Name = "User")]
    public string? User { get; set; }

    /// <summary>
    /// Part of the action or of the sentence, e.g. "LOGIN", "Competitions/Delete", "sale" (not named "Action": that is
    /// a reserved route value).
    /// </summary>
    [StringLength(50)]
    [Display(Name = "Action")]
    public string? Activity { get; set; }

    /// <summary>Kind of work (login, competition, grading, sale…).</summary>
    [Display(Name = "Kind")]
    public AuditCategory? Category { get; set; }

    /// <summary>Only the rows of one competition (its entries, awards and grading included).</summary>
    [Display(Name = "Competition")]
    public long? CompetitionId { get; set; }

    /// <summary>Only the rows of one exhibition (its artworks and sales included).</summary>
    [Display(Name = "Exhibition")]
    public long? ExhibitionId { get; set; }

    /// <summary>First day (included).</summary>
    [DataType(DataType.Date)]
    [Display(Name = "From")]
    public DateOnly? From { get; set; }

    /// <summary>Last day (included).</summary>
    [DataType(DataType.Date)]
    [Display(Name = "To")]
    public DateOnly? To { get; set; }

    /// <summary>Page number (1-based).</summary>
    public int Page { get; set; } = 1;
}

/// <summary>One row of the audit log.</summary>
public class AuditLogListItemVm
{
    /// <summary>audit_log.id.</summary>
    public long Id { get; set; }

    /// <summary>When it happened (institute local time).</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Account that did it (null for anonymous actions).</summary>
    public long? AccountId { get; set; }

    /// <summary>User name at the time (null for anonymous actions).</summary>
    public string? Username { get; set; }

    /// <summary>Full name of the user today (null when the account is gone).</summary>
    public string? FullName { get; set; }

    /// <summary>Role of the user today (null when the account is gone).</summary>
    public Role? Role { get; set; }

    /// <summary>What was done (technical).</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Kind of work.</summary>
    public AuditCategory Category { get; set; }

    /// <summary>Readable sentence, e.g. "Edited the competition “Wildlife”"; null on rows written before it existed.</summary>
    public string? Summary { get; set; }

    /// <summary>Kind of object concerned.</summary>
    public string? Entity { get; set; }

    /// <summary>Id of the object concerned.</summary>
    public long? EntityId { get; set; }

    /// <summary>Competition it belongs to.</summary>
    public long? CompetitionId { get; set; }

    /// <summary>Exhibition it belongs to.</summary>
    public long? ExhibitionId { get; set; }

    /// <summary>Entry it is about.</summary>
    public long? SubmissionId { get; set; }

    /// <summary>Account it was done to.</summary>
    public long? TargetAccountId { get; set; }

    /// <summary>Outcome, path and changed fields.</summary>
    public string? Details { get; set; }

    /// <summary>Client IP address.</summary>
    public string? IpAddress { get; set; }

    /// <summary>The sentence to show: the summary, or the technical action of an old row.</summary>
    public string Text => Summary ?? Action;

    /// <summary>The changed fields written in the details ("Changes: a; b"), one per line; empty when none.</summary>
    public IReadOnlyList<string> Changes
    {
        get
        {
            const string marker = "Changes: ";
            var at = Details?.IndexOf(marker, StringComparison.Ordinal) ?? -1;
            return at < 0 ? [] : Details![(at + marker.Length)..].Split(" | ", StringSplitOptions.RemoveEmptyEntries);
        }
    }

    /// <summary>False when the action was refused or failed (details say "failed").</summary>
    public bool Failed => Details?.Contains(" failed", StringComparison.Ordinal) == true || Action == "LOGIN_FAILED";
}

/// <summary>
/// History of one object, shown on its own page: the rows of one competition, one exhibition, one entry, or of one
/// account (what it did, its logins, what was done to it). Exactly one of the object ids is set.
/// </summary>
public class AuditHistoryQuery
{
    /// <summary>Rows of this competition.</summary>
    public long? CompetitionId { get; set; }

    /// <summary>Rows of this exhibition.</summary>
    public long? ExhibitionId { get; set; }

    /// <summary>Rows about this entry.</summary>
    public long? SubmissionId { get; set; }

    /// <summary>Rows done by or to this account (the login history includes refused sign-ins with its user name).</summary>
    public long? AccountId { get; set; }

    /// <summary>Rows done by this account.</summary>
    public long? ActorAccountId { get; set; }

    /// <summary>Rows done to this account.</summary>
    public long? TargetAccountId { get; set; }

    /// <summary>Only this kind of work (e.g. LOGIN for the login history).</summary>
    public AuditCategory? Category { get; set; }

    /// <summary>Leave out this kind of work (e.g. LOGIN from the activity tab).</summary>
    public AuditCategory? ExceptCategory { get; set; }

    /// <summary>Part of the user name.</summary>
    public string? User { get; set; }

    /// <summary>Page number (1-based).</summary>
    public int Page { get; set; } = 1;

    /// <summary>Rows per page.</summary>
    public int PageSize { get; set; } = 20;
}

/// <summary>Full history page of one object (Views/Shared/HistoryPage.cshtml): filters + the paged rows.</summary>
public class HistoryPageVm
{
    /// <summary>Page title, e.g. "History of Wildlife".</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Link back to the object.</summary>
    public string BackUrl { get; set; } = string.Empty;

    /// <summary>Text of the back link.</summary>
    public string BackText { get; set; } = string.Empty;

    /// <summary>Kind of work filter (null = all).</summary>
    public AuditCategory? Category { get; set; }

    /// <summary>User name filter.</summary>
    public string? User { get; set; }

    /// <summary>Kinds offered in the filter (the ones that can happen to this object).</summary>
    public IReadOnlyList<AuditCategory> Categories { get; set; } = [];

    /// <summary>The rows.</summary>
    public HistoryVm History { get; set; } = null!;
}

/// <summary>Model of the shared history partial (Views/Shared/_History.cshtml).</summary>
/// <param name="Rows">The page of rows</param>
/// <param name="Title">Heading of the section</param>
/// <param name="MoreUrl">Link to the full, filterable history (null on the full page itself)</param>
/// <param name="ShowObject">Show which object each row is about (on account pages, where rows are about many objects)</param>
public record HistoryVm(PagedResult<AuditLogListItemVm> Rows, string Title, string? MoreUrl = null, bool ShowObject = false);
