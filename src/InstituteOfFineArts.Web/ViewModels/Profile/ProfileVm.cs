using System.ComponentModel.DataAnnotations;
using InstituteOfFineArts.Web.Models.Enums;

namespace InstituteOfFineArts.Web.ViewModels.Profile;

/// <summary>
/// Data shown on the "My profile" page (/profile).
/// Only Phone and Address can be edited (see EditProfileVm); role, codes and names are read-only (F-M01-05).
/// </summary>
public class ProfileVm
{
    /// <summary>Username used to log in.</summary>
    [Display(Name = "Username")]
    public string Username { get; set; } = string.Empty;

    /// <summary>Full name.</summary>
    [Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    /// <summary>Email address.</summary>
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    /// <summary>Role of the account.</summary>
    [Display(Name = "Role")]
    public Role Role { get; set; }

    /// <summary>Date of birth.</summary>
    [Display(Name = "Date of birth")]
    public DateOnly? DateOfBirth { get; set; }

    /// <summary>Gender.</summary>
    [Display(Name = "Gender")]
    public Gender? Gender { get; set; }

    /// <summary>Phone number (editable).</summary>
    [Display(Name = "Phone")]
    public string? Phone { get; set; }

    /// <summary>Address (editable).</summary>
    [Display(Name = "Address")]
    public string? Address { get; set; }

    /// <summary>Time of the last successful login (server time).</summary>
    [Display(Name = "Last login")]
    public DateTime? LastLoginAt { get; set; }

    /// <summary>Student code or staff code (null when the account has no profile).</summary>
    [Display(Name = "Code")]
    public string? Code { get; set; }

    /// <summary>Students: course name. Staff: position.</summary>
    [Display(Name = "Course / Position")]
    public string? CourseOrPosition { get; set; }

    /// <summary>Students: class name. Staff: classes in charge.</summary>
    [Display(Name = "Class(es)")]
    public string? Classes { get; set; }

    /// <summary>Staff only: subjects in charge.</summary>
    [Display(Name = "Subjects")]
    public string? Subjects { get; set; }

    /// <summary>Students: enrollment date. Staff: joined date.</summary>
    [Display(Name = "Since")]
    public DateOnly? SinceDate { get; set; }
}
