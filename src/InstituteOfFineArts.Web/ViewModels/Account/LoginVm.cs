using System.ComponentModel.DataAnnotations;

namespace InstituteOfFineArts.Web.ViewModels.Account;

/// <summary>
/// Data of the login form (/login).
/// </summary>
public class LoginVm
{
    /// <summary>Username.</summary>
    [Required(ErrorMessage = "Please enter your username")]
    [StringLength(50, ErrorMessage = "Username must be at most 50 characters")]
    [Display(Name = "Username")]
    public string Username { get; set; } = string.Empty;

    /// <summary>Password (plain text).</summary>
    [Required(ErrorMessage = "Please enter your password")]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;

    /// <summary>Keep the user signed in for 7 days.</summary>
    [Display(Name = "Remember me")]
    public bool RememberMe { get; set; }

    /// <summary>Page the user wanted before being asked to log in (only local paths are accepted).</summary>
    public string? ReturnUrl { get; set; }
}
