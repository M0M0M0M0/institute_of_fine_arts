using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Infrastructure.Security;
using InstituteOfFineArts.Web.Services.Interfaces;
using InstituteOfFineArts.Web.ViewModels.Profile;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace InstituteOfFineArts.Web.Controllers;

/// <summary>
/// "My profile" for every signed-in role (M01, task BE1-03).
/// GET/POST /profile – view and edit phone/address; GET/POST /profile/change-password.
/// </summary>
[Authorize]
[Route("profile")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class ProfileController : Controller
{
    /// <summary>Message after the profile is saved.</summary>
    public const string ProfileSavedMessage = "Your profile has been updated.";

    /// <summary>Message after the password is changed.</summary>
    public const string PasswordChangedMessage = "Your password has been changed.";

    /// <summary>Profile service.</summary>
    private readonly IProfileService _profileService;

    /// <summary>Translates the password messages when the Vietnamese UI is chosen (F-M19-01); English stays unchanged.</summary>
    private readonly IStringLocalizer<SharedResource> _localizer;

    /// <summary>Receives the profile service and the text localizer through Dependency Injection.</summary>
    public ProfileController(IProfileService profileService, IStringLocalizer<SharedResource> localizer)
    {
        _profileService = profileService;
        _localizer = localizer;
    }

    /// <summary>Shows the signed-in user's profile.</summary>
    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        try
        {
            return View(await _profileService.GetProfileAsync(User.GetAccountId()));
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>Saves phone and address. Other fields posted by the browser are ignored.</summary>
    /// <param name="form">Editable fields</param>
    [HttpPost("")]
    public async Task<IActionResult> Index(EditProfileVm form)
    {
        var accountId = User.GetAccountId();
        try
        {
            if (!ModelState.IsValid)
            {
                // Show the page again with the read-only data and the values the user typed
                var vm = await _profileService.GetProfileAsync(accountId);
                vm.Phone = form.Phone;
                vm.Address = form.Address;
                return View(vm);
            }

            await _profileService.UpdateProfileAsync(accountId, form);
        }
        catch (NotFoundException)
        {
            return NotFound();
        }

        // Post/Redirect/Get: refreshing the page does not post the form again
        TempData["Success"] = ProfileSavedMessage;
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Shows the change password form.</summary>
    [HttpGet("change-password")]
    public IActionResult ChangePassword() => View(new ChangePasswordVm());

    /// <summary>Changes the password after checking the current one.</summary>
    /// <param name="form">Current, new and confirmed password</param>
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordVm form)
    {
        if (ModelState.IsValid)
        {
            try
            {
                await _profileService.ChangePasswordAsync(User.GetAccountId(), form.CurrentPassword, form.NewPassword);
                TempData["Success"] = PasswordChangedMessage;
                return RedirectToAction(nameof(Index));
            }
            catch (BusinessException ex)
            {
                // Wrong current password → error under that field; password rules → under the new password
                ModelState.AddModelError(ex.Field ?? string.Empty, _localizer[ex.Message].Value);
            }
            catch (NotFoundException)
            {
                return NotFound();
            }
        }

        // Password inputs never render the typed value, so the form comes back empty
        return View(new ChangePasswordVm());
    }
}
