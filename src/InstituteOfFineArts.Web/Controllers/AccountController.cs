using InstituteOfFineArts.Web.Services.Interfaces;
using InstituteOfFineArts.Web.ViewModels.Account;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace InstituteOfFineArts.Web.Controllers;

/// <summary>
/// Login / logout (module M01, task BE1-02).
/// Fixed URLs: GET/POST /login, POST /logout.
/// </summary>
public class AccountController : Controller
{
    /// <summary>Generic message for a wrong username or password (deliberately not saying which one).</summary>
    public const string InvalidCredentialsMessage = "Invalid username or password.";

    /// <summary>Message shown when the account is locked.</summary>
    public const string LockedMessage = "Your account has been locked. Please contact the administrator.";

    /// <summary>How long the user stays signed in when "Remember me" is checked.</summary>
    private static readonly TimeSpan RememberMeDuration = TimeSpan.FromDays(7);

    /// <summary>Authentication service.</summary>
    private readonly IAuthService _authService;

    /// <summary>Translates the login messages when the Vietnamese UI is chosen (F-M19-01); English stays unchanged.</summary>
    private readonly IStringLocalizer<SharedResource> _localizer;

    /// <summary>Receives the authentication service and the text localizer through Dependency Injection.</summary>
    public AccountController(IAuthService authService, IStringLocalizer<SharedResource> localizer)
    {
        _authService = authService;
        _localizer = localizer;
    }

    /// <summary>
    /// Shows the login form. A signed-in user is redirected straight to the home page of their role.
    /// </summary>
    /// <param name="returnUrl">Page requested before being asked to log in</param>
    [HttpGet("/login")]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        // Already signed in: no need to show the form
        if (User.Identity?.IsAuthenticated == true)
        {
            return LocalRedirect(_authService.GetHomePath(User));
        }

        return View(new LoginVm { ReturnUrl = returnUrl });
    }

    /// <summary>
    /// Handles the login form: checks the account, creates the cookie, redirects by role.
    /// </summary>
    /// <param name="form">Data entered by the user</param>
    [HttpPost("/login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(LoginVm form)
    {
        // Missing username / password → show the form again with errors
        if (!ModelState.IsValid)
        {
            return View(form);
        }

        var result = await _authService.LoginAsync(form.Username, form.Password,
            CookieAuthenticationDefaults.AuthenticationScheme);

        // Login failed: show a generic error and never send the typed password back
        if (result.Status != LoginStatus.Success || result.Principal == null)
        {
            var message = result.Status == LoginStatus.Locked ? LockedMessage : InvalidCredentialsMessage;
            ModelState.AddModelError(string.Empty, _localizer[message].Value);
            form.Password = string.Empty;
            return View(form);
        }

        // Create the login cookie; with "Remember me" it lives 7 days even after the browser is closed
        var properties = new AuthenticationProperties { IsPersistent = form.RememberMe };
        if (form.RememberMe)
        {
            properties.ExpiresUtc = DateTimeOffset.UtcNow.Add(RememberMeDuration);
        }
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, result.Principal, properties);

        // Go back to the requested page (local paths only – prevents open redirects), otherwise to the role's home page
        if (!string.IsNullOrEmpty(form.ReturnUrl) && Url.IsLocalUrl(form.ReturnUrl))
        {
            return LocalRedirect(form.ReturnUrl);
        }
        return LocalRedirect(_authService.GetHomePath(result.Principal));
    }

    /// <summary>
    /// Logs out: removes the cookie and returns to the home page. POST only (with anti-forgery token)
    /// so a user cannot be logged out by a foreign link.
    /// </summary>
    [HttpPost("/logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return LocalRedirect("/");
    }
}
