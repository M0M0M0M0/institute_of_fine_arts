using System.ComponentModel.DataAnnotations;
using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.Validation;

namespace InstituteOfFineArts.Web.ViewModels.Admin.Students;

/// <summary>
/// Edit form of a student (F-M04-02): personal and study information, every field except the username and the password.
/// The role is always STUDENT and is not part of the form.
/// </summary>
public class StudentFormVm : IValidatableObject
{
    /// <summary>student_profile.id (0 when creating).</summary>
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

    /// <summary>Gender.</summary>
    [Display(Name = "Gender")]
    public Gender? Gender { get; set; }

    /// <summary>Date of birth (must be in the past).</summary>
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

    /// <summary>Student code (unique, BR-01). Left empty → generated HVnnnn.</summary>
    [StringLength(20, ErrorMessage = "Student code must be at most 20 characters")]
    [RegularExpression(@"^[A-Za-z0-9\-]*$", ErrorMessage = "Student code can only contain letters, digits and dashes")]
    [Display(Name = "Student code")]
    public string? StudentCode { get; set; }

    /// <summary>Admission date.</summary>
    [Required(ErrorMessage = "Please choose the enrollment date")]
    [DataType(DataType.Date)]
    [Display(Name = "Enrollment date")]
    public DateOnly? EnrollmentDate { get; set; }

    /// <summary>Course, e.g. Painting, Animation.</summary>
    [Required(ErrorMessage = "Please enter the course")]
    [StringLength(100, ErrorMessage = "Course must be at most 100 characters")]
    [Display(Name = "Course")]
    public string CourseName { get; set; } = string.Empty;

    /// <summary>Class, e.g. PT01.</summary>
    [StringLength(50, ErrorMessage = "Class must be at most 50 characters")]
    [Display(Name = "Class")]
    public string? ClassName { get; set; }

    /// <summary>Study status.</summary>
    [Required(ErrorMessage = "Please choose the study status")]
    [Display(Name = "Study status")]
    public EnrollmentStatus? EnrollmentStatus { get; set; } = Models.Enums.EnrollmentStatus.STUDYING;

    /// <summary>Free notes.</summary>
    [StringLength(500, ErrorMessage = "Notes must be at most 500 characters")]
    [Display(Name = "Notes")]
    public string? Notes { get; set; }

    /// <summary>Checks rules that need today's date.</summary>
    public virtual IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // TC-M04-02: a date of birth in the future (or today) is refused
        if (DateOfBirth is { } dob && dob >= Helpers.DateTimeHelper.Today)
        {
            yield return new ValidationResult(StudentDateMessages.DateOfBirthInPast, new[] { nameof(DateOfBirth) });
        }
    }
}

/// <summary>Date messages shared by the form and the service.</summary>
public static class StudentDateMessages
{
    /// <summary>Date of birth must be before today.</summary>
    public const string DateOfBirthInPast = "Date of birth must be in the past";
}

/// <summary>
/// Create form (F-M04-01): the edit fields plus username and initial password.
/// </summary>
public class CreateStudentVm : StudentFormVm
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

/// <summary>Temporary password set by the administrator for a student (F-M04-07).</summary>
public class ResetStudentPasswordVm
{
    /// <summary>New password (BR-02).</summary>
    [Required(ErrorMessage = "Please enter a new password")]
    [StrongPassword]
    [DataType(DataType.Password)]
    [Display(Name = "New password")]
    public string NewPassword { get; set; } = string.Empty;
}
