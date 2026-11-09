using System.ComponentModel.DataAnnotations;

namespace InstituteOfFineArts.Web.ViewModels.Admin.AuditLog;

/// <summary>Filters of the audit log page (F-M17-01).</summary>
public class AuditLogSearchVm
{
    /// <summary>Part of the user name.</summary>
    [StringLength(50)]
    [Display(Name = "User")]
    public string? User { get; set; }

    /// <summary>
    /// Part of the action, e.g. "LOGIN" or "Competitions/Delete" (not named "Action": that is a reserved route value).
    /// </summary>
    [StringLength(50)]
    [Display(Name = "Action")]
    public string? Activity { get; set; }

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

    /// <summary>User name at the time (null for anonymous actions).</summary>
    public string? Username { get; set; }

    /// <summary>What was done.</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Kind of object concerned.</summary>
    public string? Entity { get; set; }

    /// <summary>Id of the object concerned.</summary>
    public long? EntityId { get; set; }

    /// <summary>Short description.</summary>
    public string? Details { get; set; }

    /// <summary>Client IP address.</summary>
    public string? IpAddress { get; set; }
}
