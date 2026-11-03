using System.ComponentModel.DataAnnotations;
using InstituteOfFineArts.Web.Validation;
using InstituteOfFineArts.Web.ViewModels.Profile;

namespace InstituteOfFineArts.Tests.Validation;

/// <summary>
/// Form validation for the profile pages (task BE1-03): password rule BR-02, matching confirmation, phone format.
/// </summary>
public class ProfileValidationTests
{
    /// <summary>Runs every DataAnnotations rule of an object and returns the names of the invalid fields.</summary>
    private static List<string> InvalidFields(object model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
        return results.SelectMany(r => r.MemberNames).ToList();
    }

    /// <summary>BR-02: 8+ characters with a letter and a digit.</summary>
    [Theory]
    [InlineData("Student@123", true)]
    [InlineData("abcdefg1", true)]
    [InlineData("abc123", false)]          // too short
    [InlineData("abcdefgh", false)]        // no digit
    [InlineData("12345678", false)]        // no letter
    [InlineData("", true)]                 // empty is left to [Required]
    public void StrongPassword_Rule(string password, bool expected)
    {
        Assert.Equal(expected, new StrongPasswordAttribute().IsValid(password));
    }

    /// <summary>Longer than 72 characters (BCrypt limit) is rejected.</summary>
    [Fact]
    public void StrongPassword_TooLong_Invalid()
    {
        Assert.False(StrongPasswordAttribute.IsStrong(new string('a', 72) + "1"));
    }

    /// <summary>A valid change password form has no errors.</summary>
    [Fact]
    public void ChangePassword_Valid()
    {
        var vm = new ChangePasswordVm { CurrentPassword = "x", NewPassword = "NewPass2026", ConfirmPassword = "NewPass2026" };
        Assert.Empty(InvalidFields(vm));
    }

    /// <summary>Confirmation must match the new password.</summary>
    [Fact]
    public void ChangePassword_ConfirmMismatch_Invalid()
    {
        var vm = new ChangePasswordVm { CurrentPassword = "x", NewPassword = "NewPass2026", ConfirmPassword = "NewPass2027" };
        Assert.Contains(nameof(ChangePasswordVm.ConfirmPassword), InvalidFields(vm));
    }

    /// <summary>All three fields are required.</summary>
    [Fact]
    public void ChangePassword_Empty_AllRequired()
    {
        var fields = InvalidFields(new ChangePasswordVm());
        Assert.Contains(nameof(ChangePasswordVm.CurrentPassword), fields);
        Assert.Contains(nameof(ChangePasswordVm.NewPassword), fields);
        Assert.Contains(nameof(ChangePasswordVm.ConfirmPassword), fields);
    }

    /// <summary>Phone format: digits with optional +, spaces, dots, dashes, brackets.</summary>
    [Theory]
    [InlineData("0901234567", true)]
    [InlineData("+84 912 345 678", true)]
    [InlineData("(028) 3822-1234", true)]
    [InlineData(null, true)]
    [InlineData("12345", false)]
    [InlineData("call me maybe", false)]
    [InlineData("<script>", false)]
    public void EditProfile_Phone(string? phone, bool expected)
    {
        var fields = InvalidFields(new EditProfileVm { Phone = phone });
        Assert.Equal(expected, !fields.Contains(nameof(EditProfileVm.Phone)));
    }

    /// <summary>Address longer than 255 characters is rejected.</summary>
    [Fact]
    public void EditProfile_AddressTooLong_Invalid()
    {
        var fields = InvalidFields(new EditProfileVm { Address = new string('a', 256) });
        Assert.Contains(nameof(EditProfileVm.Address), fields);
    }
}
