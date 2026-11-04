using System.ComponentModel.DataAnnotations;
using InstituteOfFineArts.Web.Validation;

namespace InstituteOfFineArts.Web.ViewModels.Profile;

/// <summary>
/// Change password form (/profile/change-password) – F-M01-04, rule BR-02.
/// </summary>
public class ChangePasswordVm
{
    /// <summary>Password currently in use (checked against the BCrypt hash).</summary>
    [Required(ErrorMessage = "Please enter your current password")]
    [DataType(DataType.Password)]
    [Display(Name = "Current password")]
    public string CurrentPassword { get; set; } = string.Empty;

    /// <summary>New password.</summary>
    [Required(ErrorMessage = "Please enter a new password")]
    [StrongPassword]
    [DataType(DataType.Password)]
    [Display(Name = "New password")]
    public string NewPassword { get; set; } = string.Empty;

    /// <summary>New password typed a second time.</summary>
    [Required(ErrorMessage = "Please confirm the new password")]
    [Compare(nameof(NewPassword), ErrorMessage = "The new passwords do not match")]
    [DataType(DataType.Password)]
    [Display(Name = "Confirm new password")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
