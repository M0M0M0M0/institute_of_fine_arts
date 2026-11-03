using System.ComponentModel.DataAnnotations;

namespace InstituteOfFineArts.Web.Validation;

/// <summary>
/// Password rule BR-02: at least 8 characters, with at least one letter and one digit.
/// Reuse it on every form that sets a password (change password, admin creating accounts…).
/// An empty value is accepted here – combine with [Required] when the field is mandatory.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public class StrongPasswordAttribute : ValidationAttribute
{
    /// <summary>Minimum password length.</summary>
    public const int MinLength = 8;

    /// <summary>Maximum password length (BCrypt only uses the first 72 bytes).</summary>
    public const int MaxLength = 72;

    /// <summary>Creates the attribute with the default English message.</summary>
    public StrongPasswordAttribute()
        : base($"Password must be {MinLength}–{MaxLength} characters and contain at least one letter and one digit")
    {
    }

    /// <summary>Checks one value; true when it follows the rule (or is empty).</summary>
    public override bool IsValid(object? value)
    {
        // Empty values are left to [Required]
        if (value is not string password || password.Length == 0)
        {
            return true;
        }

        return IsStrong(password);
    }

    /// <summary>Checks the rule on a plain string (also used by services as a second line of defence).</summary>
    public static bool IsStrong(string? password)
        => password != null
           && password.Length >= MinLength
           && password.Length <= MaxLength
           && password.Any(char.IsLetter)
           && password.Any(char.IsDigit);
}
