using System.ComponentModel.DataAnnotations;

namespace InstituteOfFineArts.Web.ViewModels.Account;

/// <summary>
/// "Forgot password" form (/forgot-password) – F-M16-01.
/// </summary>
public class ForgotPasswordVm
{
    /// <summary>User name or e-mail address of the account.</summary>
    [Required(ErrorMessage = "Please enter your username or e-mail")]
    [StringLength(100, ErrorMessage = "Username or e-mail must be at most 100 characters")]
    [Display(Name = "Username or e-mail")]
    public string UserNameOrEmail { get; set; } = string.Empty;
}
