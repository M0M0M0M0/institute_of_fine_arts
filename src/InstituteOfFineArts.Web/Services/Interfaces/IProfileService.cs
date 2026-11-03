using InstituteOfFineArts.Web.ViewModels.Profile;

namespace InstituteOfFineArts.Web.Services.Interfaces;

/// <summary>
/// The signed-in user's own profile: view, edit contact details, change password (M01, task BE1-03).
/// </summary>
public interface IProfileService
{
    /// <summary>Loads the profile of an account. Throws NotFoundException when the account does not exist.</summary>
    Task<ProfileVm> GetProfileAsync(long accountId);

    /// <summary>
    /// Saves the editable fields (phone, address). Blank values are stored as null.
    /// Throws NotFoundException when the account does not exist.
    /// </summary>
    Task UpdateProfileAsync(long accountId, EditProfileVm form);

    /// <summary>
    /// Changes the password after checking the current one.
    /// Throws BusinessException when the current password is wrong, the new password breaks BR-02
    /// or equals the current one; NotFoundException when the account does not exist.
    /// </summary>
    Task ChangePasswordAsync(long accountId, string currentPassword, string newPassword);
}
