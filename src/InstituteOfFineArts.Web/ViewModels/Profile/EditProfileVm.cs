using System.ComponentModel.DataAnnotations;

namespace InstituteOfFineArts.Web.ViewModels.Profile;

/// <summary>
/// Fields a user may change on their own profile (POST /profile). Everything else is read-only.
/// Property names match ProfileVm so the same view shows the validation errors.
/// </summary>
public class EditProfileVm
{
    /// <summary>Phone number: digits with optional +, spaces, dots, dashes or brackets.</summary>
    [StringLength(20, ErrorMessage = "Phone must be at most 20 characters")]
    [RegularExpression(@"^\+?[0-9(][0-9 .\-()]{6,19}$", ErrorMessage = "Please enter a valid phone number")]
    [Display(Name = "Phone")]
    public string? Phone { get; set; }

    /// <summary>Address.</summary>
    [StringLength(255, ErrorMessage = "Address must be at most 255 characters")]
    [Display(Name = "Address")]
    public string? Address { get; set; }
}
