namespace InstituteOfFineArts.Web.Models.Entities;

/// <summary>
/// Employment details of ADMIN, MANAGER and STAFF members (table staff_profile).
/// </summary>
public class StaffProfile : BaseEntity
{
    /// <summary>Foreign key to the account (one-to-one).</summary>
    public long AccountId { get; set; }

    /// <summary>Staff code, unique. Example: NV0001.</summary>
    public string StaffCode { get; set; } = string.Empty;

    /// <summary>Position: Lecturer, Head of Department…</summary>
    public string? Position { get; set; }

    /// <summary>Date joined the institute.</summary>
    public DateOnly JoinedDate { get; set; }

    /// <summary>Classes handled (free text, e.g. "PT01, PT02").</summary>
    public string? ClassesInCharge { get; set; }

    /// <summary>Subjects handled.</summary>
    public string? SubjectsInCharge { get; set; }

    /// <summary>Qualification.</summary>
    public string? Qualification { get; set; }

    /// <summary>Remarks (required by the problem statement).</summary>
    public string? Notes { get; set; }

    /// <summary>Login account.</summary>
    public Account Account { get; set; } = null!;
}
