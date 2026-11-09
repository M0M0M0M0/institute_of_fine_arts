using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Services.Implementations;
using InstituteOfFineArts.Web.Services.Interfaces;
using InstituteOfFineArts.Web.ViewModels.Account;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InstituteOfFineArts.Web.Controllers;

/// <summary>
/// Forgot password (optional feature F-M16-01, task BE1-OPT).
/// GET/POST /forgot-password – ask for a reset link; GET/POST /reset-password?token=... – set a new password.
/// </summary>
[AllowAnonymous]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class PasswordResetController : Controller
{
    /// <summary>Password reset service.</summary>
    private readonly IPasswordResetService _passwordResetService;

    /// <summary>Receives the password reset service through Dependency Injection.</summary>
    public PasswordResetController(IPasswordResetService passwordResetService)
    {
        _passwordResetService = passwordResetService;
    }

    /// <summary>Shows the "forgot password" form.</summary>
    [HttpGet("/forgot-password")]
    public IActionResult ForgotPassword() => View(new ForgotPasswordVm());

    /// <summary>
    /// Handles the request and always shows the same neutral message, whether or not the account exists
    /// (no account enumeration).
    /// </summary>
    /// <param name="form">User name or e-mail</param>
    /// <param name="cancellationToken">Cancelled when the browser disconnects</param>
    [HttpPost("/forgot-password")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordVm form, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(form);
        }

        // Address of this site, used for the link only when Email:AppBaseUrl is not configured
        var requestBaseUrl = $"{Request.Scheme}://{Request.Host}{Request.PathBase}";
        await _passwordResetService.RequestResetAsync(form.UserNameOrEmail, requestBaseUrl, cancellationToken);

        // Post/Redirect/Get with the neutral message
        TempData["Success"] = PasswordResetService.RequestAcceptedMessage;
        return RedirectToAction(nameof(ForgotPassword));
    }

    /// <summary>Shows the "set a new password" form, or an error when the link is invalid, used or expired.</summary>
    /// <param name="token">Token from the e-mail link</param>
    [HttpGet("/reset-password")]
    public async Task<IActionResult> ResetPassword(string? token)
    {
        // The token is in the URL: do not leak it to other sites through the Referer header
        Response.Headers["Referrer-Policy"] = "no-referrer";
        var valid = await _passwordResetService.IsTokenValidAsync(token);
        return View(new ResetPasswordVm { Token = token, TokenValid = valid });
    }

    /// <summary>Sets the new password, then sends the user to the login page.</summary>
    /// <param name="form">Token (hidden field), new and confirmed password</param>
    [HttpPost("/reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordVm form)
    {
        Response.Headers["Referrer-Policy"] = "no-referrer";
        if (ModelState.IsValid)
        {
            try
            {
                await _passwordResetService.ResetPasswordAsync(form.Token, form.NewPassword);
                TempData["Success"] = PasswordResetService.PasswordResetMessage;
                return RedirectToAction(nameof(AccountController.Login), "Account");
            }
            catch (BusinessException ex)
            {
                // Weak password → under the field; invalid link / locked account → general error
                ModelState.AddModelError(ex.Field ?? string.Empty, ex.Message);
            }
        }

        // Password inputs never render the typed value, so the form comes back empty
        return View(new ResetPasswordVm { Token = form.Token, TokenValid = true });
    }
}
