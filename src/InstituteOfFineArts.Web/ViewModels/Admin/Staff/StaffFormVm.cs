using System.ComponentModel.DataAnnotations;
using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.Validation;

namespace InstituteOfFineArts.Web.ViewModels.Admin.Staff;

/// <summary>
/// Edit form of a staff member (F-M03-02): every field except the username and the password.
/// Used for STAFF, MANAGER and ADMIN accounts (all have a staff_profile).
/// </summary>
public class StaffFormVm : IValidatableObject
{
    /// <summary>staff_profile.id (0 when creating).</summary>
    public long Id { get; set; }

    /// <summary>Username (shown read-only when editing).</summary>
    public string? Username { get; set; }

    /// <summary>Full name.</summary>
    [Required(ErrorMessage = "Please enter the full name")]
    [StringLength(100, ErrorMessage = "Full name must be at most 100 characters")]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    /// <summary>Email (unique, BR-01).</summary>
    [Required(ErrorMessage = "Please enter the email")]
    [EmailAddress(ErrorMessage = "Please enter a valid email address")]
    [StringLength(100, ErrorMessage = "Email must be at most 100 characters")]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    /// <summary>Role: STAFF, MANAGER or ADMIN.</summary>
    [Required(ErrorMessage = "Please choose the role")]
    [Display(Name = "Role")]
    public Role? Role { get; set; } = Models.Enums.Role.STAFF;

    /// <summary>Gender.</summary>
    [Display(Name = "Gender")]
    public Gender? Gender { get; set; }

    /// <summary>Date of birth.</summary>
    [DataType(DataType.Date)]
    [Display(Name = "Date of birth")]
    public DateOnly? DateOfBirth { get; set; }

    /// <summary>Phone number.</summary>
    [StringLength(20, ErrorMessage = "Phone must be at most 20 characters")]
    [RegularExpression(@"^\+?[0-9(][0-9 .\-()]{6,19}$", ErrorMessage = "Please enter a valid phone number")]
    [Display(Name = "Phone")]
    public string? Phone { get; set; }

    /// <summary>Address.</summary>
    [StringLength(255, ErrorMessage = "Address must be at most 255 characters")]
    [Display(Name = "Address")]
    public string? Address { get; set; }

    /// <summary>Staff code (unique). Left empty → generated NVnnnn.</summary>
    [StringLength(20, ErrorMessage = "Staff code must be at most 20 characters")]
    [RegularExpression(@"^[A-Za-z0-9\-]*$", ErrorMessage = "Staff code can only contain letters, digits and dashes")]
    [Display(Name = "Staff code")]
    public string? StaffCode { get; set; }

    /// <summary>Date the person joined the institute.</summary>
    [Required(ErrorMessage = "Please choose the joined date")]
    [DataType(DataType.Date)]
    [Display(Name = "Joined date")]
    public DateOnly? JoinedDate { get; set; }

    /// <summary>Job title, e.g. Lecturer.</summary>
    [StringLength(100, ErrorMessage = "Position must be at most 100 characters")]
    [Display(Name = "Position")]
    public string? Position { get; set; }

    /// <summary>Classes in charge, e.g. "PT01, PT02".</summary>
    [StringLength(255, ErrorMessage = "Classes must be at most 255 characters")]
    [Display(Name = "Classes in charge")]
    public string? ClassesInCharge { get; set; }

    /// <summary>Subjects in charge, e.g. "Oil Painting, Watercolor".</summary>
    [StringLength(255, ErrorMessage = "Subjects must be at most 255 characters")]
    [Display(Name = "Subjects in charge")]
    public string? SubjectsInCharge { get; set; }

    /// <summary>Qualification, e.g. "MFA in Painting".</summary>
    [StringLength(255, ErrorMessage = "Qualification must be at most 255 characters")]
    [Display(Name = "Qualification")]
    public string? Qualification { get; set; }

    /// <summary>Free notes.</summary>
    [StringLength(500, ErrorMessage = "Notes must be at most 500 characters")]
    [Display(Name = "Notes")]
    public string? Notes { get; set; }

    /// <summary>Checks rules that need today's date.</summary>
    public virtual IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (DateOfBirth is { } dob && dob >= Helpers.DateTimeHelper.Today)
        {
            yield return new ValidationResult("Date of birth must be in the past", new[] { nameof(DateOfBirth) });
        }
        if (Role == Models.Enums.Role.STUDENT)
        {
            yield return new ValidationResult("Students are managed on the Students page", new[] { nameof(Role) });
        }
    }
}

/// <summary>
/// Create form (F-M03-01): the edit fields plus username and initial password.
/// </summary>
public class CreateStaffVm : StaffFormVm
{
    /// <summary>Username for logging in (unique, BR-01).</summary>
    [Required(ErrorMessage = "Please enter the username")]
    [StringLength(50, MinimumLength = 3, ErrorMessage = "Username must be 3–50 characters")]
    [RegularExpression(@"^[A-Za-z0-9._]+$", ErrorMessage = "Username can only contain letters, digits, dots and underscores")]
    [Display(Name = "Username")]
    public string NewUsername { get; set; } = string.Empty;

    /// <summary>Initial password (BR-02).</summary>
    [Required(ErrorMessage = "Please enter a password")]
    [StrongPassword]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;

    /// <summary>Password typed a second time.</summary>
    [Required(ErrorMessage = "Please confirm the password")]
    [Compare(nameof(Password), ErrorMessage = "The passwords do not match")]
    [DataType(DataType.Password)]
    [Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

/// <summary>Temporary password set by the administrator (F-M03-06).</summary>
public class ResetPasswordVm
{
    /// <summary>New password (BR-02).</summary>
    [Required(ErrorMessage = "Please enter a new password")]
    [StrongPassword]
    [DataType(DataType.Password)]
    [Display(Name = "New password")]
    public string NewPassword { get; set; } = string.Empty;
}
