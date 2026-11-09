using InstituteOfFineArts.Web.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace InstituteOfFineArts.Web.Controllers;

/// <summary>
/// Language switch (F-M19-01, task BE1-OPT 5): POST /culture stores the chosen UI language in the culture cookie
/// read by <see cref="CookieRequestCultureProvider"/>. Open to everyone (guests too); anti-forgery checked globally.
/// </summary>
[AllowAnonymous]
public class CultureController : Controller
{
    /// <summary>How long the browser remembers the language.</summary>
    private static readonly TimeSpan CookieLifetime = TimeSpan.FromDays(365);

    /// <summary>
    /// Saves the UI language and goes back to the page the user was on (local URLs only – no open redirect).
    /// An unknown language falls back to English. Formatting stays en-US for both languages.
    /// </summary>
    /// <param name="culture">en-US or vi-VN</param>
    /// <param name="returnUrl">Page to go back to</param>
    [HttpPost("/culture")]
    public IActionResult Set(string? culture, string? returnUrl)
    {
        var uiCulture = AppCultures.UiCultures.FirstOrDefault(c => string.Equals(c, culture, StringComparison.OrdinalIgnoreCase))
                        ?? AppCultures.English;

        // c=en-US (numbers, dates, money) | uic=chosen language (texts)
        Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(AppCultures.English, uiCulture)),
            new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.Add(CookieLifetime),
                IsEssential = true,
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                Path = "/",
            });

        return LocalRedirect(!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : "/");
    }
}
