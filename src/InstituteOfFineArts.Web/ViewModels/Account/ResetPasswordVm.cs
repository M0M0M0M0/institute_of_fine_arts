using System.ComponentModel.DataAnnotations;
using InstituteOfFineArts.Web.Validation;

namespace InstituteOfFineArts.Web.ViewModels.Account;

/// <summary>
/// "Set a new password" form opened from the reset link (/reset-password?token=...) – F-M16-01, rule BR-02.
/// </summary>
public class ResetPasswordVm
{
    /// <summary>Token from the e-mail link (hidden field).</summary>
    public string? Token { get; set; }

    /// <summary>False when the link is invalid, used or expired: the page shows an error instead of the form.</summary>
    public bool TokenValid { get; set; } = true;

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
