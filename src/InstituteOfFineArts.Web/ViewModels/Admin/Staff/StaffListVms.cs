using System.ComponentModel.DataAnnotations;
using InstituteOfFineArts.Web.Models.Enums;

namespace InstituteOfFineArts.Web.ViewModels.Admin.Staff;

/// <summary>Filters of the staff list (F-M03-04).</summary>
public class StaffSearchVm
{
    /// <summary>Part of the name, username, staff code or email.</summary>
    [StringLength(100)]
    [Display(Name = "Name, username, code or email")]
    public string? Keyword { get; set; }

    /// <summary>Role filter.</summary>
    [Display(Name = "Role")]
    public Role? Role { get; set; }

    /// <summary>Part of the subjects in charge.</summary>
    [StringLength(100)]
    [Display(Name = "Subject")]
    public string? Subject { get; set; }

    /// <summary>Part of the classes in charge.</summary>
    [StringLength(100)]
    [Display(Name = "Class")]
    public string? ClassName { get; set; }

    /// <summary>true = active only, false = locked only, null = all.</summary>
    [Display(Name = "Status")]
    public bool? Active { get; set; }

    /// <summary>Page number (1-based).</summary>
    public int Page { get; set; } = 1;
}

/// <summary>One row of the staff list.</summary>
public class StaffListItemVm
{
    /// <summary>staff_profile.id.</summary>
    public long Id { get; set; }

    /// <summary>Staff code.</summary>
    public string StaffCode { get; set; } = string.Empty;

    /// <summary>Full name.</summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>Username.</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>Email.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Role.</summary>
    public Role Role { get; set; }

    /// <summary>Position.</summary>
    public string? Position { get; set; }

    /// <summary>Subjects in charge.</summary>
    public string? SubjectsInCharge { get; set; }

    /// <summary>Classes in charge.</summary>
    public string? ClassesInCharge { get; set; }

    /// <summary>False when the account is locked.</summary>
    public bool Active { get; set; }
}

/// <summary>Staff details (F-M03-05): login, personal and job information, never the password.</summary>
public class StaffDetailsVm : StaffFormVm
{
    /// <summary>False when the account is locked.</summary>
    public bool Active { get; set; }

    /// <summary>Last successful login.</summary>
    public DateTime? LastLoginAt { get; set; }

    /// <summary>When the account was created.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Competitions created or published by this person.</summary>
    public int CompetitionCount { get; set; }

    /// <summary>Evaluations written.</summary>
    public int EvaluationCount { get; set; }

    /// <summary>Awards issued.</summary>
    public int AwardCount { get; set; }

    /// <summary>Exhibitions created and exhibition entries proposed or reviewed.</summary>
    public int ExhibitionCount { get; set; }

    /// <summary>True when any related data exists: delete will lock the account instead (BR-04).</summary>
    public bool HasRelatedData => CompetitionCount + EvaluationCount + AwardCount + ExhibitionCount > 0;

    /// <summary>True when this is the signed-in administrator (cannot lock or delete themself).</summary>
    public bool IsCurrentUser { get; set; }
}

/// <summary>What happened on delete.</summary>
public enum StaffDeleteResult
{
    /// <summary>Account and profile were removed.</summary>
    Deleted,

    /// <summary>Related data exists, so the account was locked instead (BR-04).</summary>
    Deactivated,
}
