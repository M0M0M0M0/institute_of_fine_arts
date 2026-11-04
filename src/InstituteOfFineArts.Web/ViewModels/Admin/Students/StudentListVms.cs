using System.ComponentModel.DataAnnotations;
using InstituteOfFineArts.Web.Models.Enums;

namespace InstituteOfFineArts.Web.ViewModels.Admin.Students;

/// <summary>Filters of the student list (F-M04-04), used by the Admin and the Staff pages.</summary>
public class StudentSearchVm
{
    /// <summary>Part of the name, username, student code or email.</summary>
    [StringLength(100)]
    [Display(Name = "Name, username, code or email")]
    public string? Keyword { get; set; }

    /// <summary>Part of the course name.</summary>
    [StringLength(100)]
    [Display(Name = "Course")]
    public string? Course { get; set; }

    /// <summary>Part of the class name.</summary>
    [StringLength(50)]
    [Display(Name = "Class")]
    public string? ClassName { get; set; }

    /// <summary>Study status filter.</summary>
    [Display(Name = "Study status")]
    public EnrollmentStatus? Status { get; set; }

    /// <summary>Enrolled on or after this date.</summary>
    [DataType(DataType.Date)]
    [Display(Name = "Enrolled from")]
    public DateOnly? EnrolledFrom { get; set; }

    /// <summary>Enrolled on or before this date.</summary>
    [DataType(DataType.Date)]
    [Display(Name = "Enrolled to")]
    public DateOnly? EnrolledTo { get; set; }

    /// <summary>true = active only, false = locked only, null = all.</summary>
    [Display(Name = "Account")]
    public bool? Active { get; set; }

    /// <summary>Page number (1-based).</summary>
    public int Page { get; set; } = 1;
}

/// <summary>One row of the student list.</summary>
public class StudentListItemVm
{
    /// <summary>student_profile.id.</summary>
    public long Id { get; set; }

    /// <summary>Student code.</summary>
    public string StudentCode { get; set; } = string.Empty;

    /// <summary>Full name.</summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>Username.</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>Email.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Course.</summary>
    public string CourseName { get; set; } = string.Empty;

    /// <summary>Class.</summary>
    public string? ClassName { get; set; }

    /// <summary>Admission date.</summary>
    public DateOnly EnrollmentDate { get; set; }

    /// <summary>Study status.</summary>
    public EnrollmentStatus EnrollmentStatus { get; set; }

    /// <summary>False when the account is locked.</summary>
    public bool Active { get; set; }
}

/// <summary>Student details (F-M04-05 / F-M04-06): login, personal and study information, never the password.</summary>
public class StudentDetailsVm : StudentFormVm
{
    /// <summary>False when the account is locked.</summary>
    public bool Active { get; set; }

    /// <summary>Last successful login.</summary>
    public DateTime? LastLoginAt { get; set; }

    /// <summary>When the account was created.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Competition submissions made by the student.</summary>
    public int SubmissionCount { get; set; }

    /// <summary>Exhibition entries proposed by the student or made from their submissions.</summary>
    public int ExhibitionEntryCount { get; set; }

    /// <summary>Awards won.</summary>
    public int AwardCount { get; set; }

    /// <summary>True when submissions or exhibition entries exist: delete will lock the account instead (BR-04).</summary>
    public bool HasRelatedData => SubmissionCount + ExhibitionEntryCount > 0;
}

/// <summary>What happened on delete.</summary>
public enum StudentDeleteResult
{
    /// <summary>Account and profile were removed.</summary>
    Deleted,

    /// <summary>Submissions or exhibition entries exist, so the account was locked instead (BR-04).</summary>
    Deactivated,
}
