using System.Collections;
using System.Globalization;
using System.Resources;
using InstituteOfFineArts.Web;
using InstituteOfFineArts.Web.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;

namespace InstituteOfFineArts.Tests.Controllers;

/// <summary>
/// Vietnamese UI (task BE1-OPT 5, F-M19-01): POST /culture cookie + local redirect, and the shared .resx files.
/// </summary>
public class CultureControllerTests
{
    /// <summary>Controller with a real HttpContext and URL helper (for Url.IsLocalUrl).</summary>
    private static CultureController Controller()
    {
        var http = new DefaultHttpContext();
        var controller = new CultureController { ControllerContext = new ControllerContext { HttpContext = http } };
        controller.Url = new UrlHelper(new ActionContext(http, new RouteData(), new ActionDescriptor()));
        return controller;
    }

    /// <summary>Cookie keeps en-US formatting and stores the chosen UI language; back to the local page.</summary>
    [Fact]
    public void Set_Vietnamese_CookieAndRedirect()
    {
        var controller = Controller();

        var result = Assert.IsType<LocalRedirectResult>(controller.Set("vi-VN", "/profile?x=1"));

        Assert.Equal("/profile?x=1", result.Url);
        var cookie = controller.Response.Headers.SetCookie.ToString();
        Assert.Contains(".AspNetCore.Culture=" + Uri.EscapeDataString("c=en-US|uic=vi-VN"), cookie);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Unknown language → English; external return URL → home page (no open redirect).</summary>
    [Theory]
    [InlineData("fr-FR", "https://evil.example.com/")]
    [InlineData(null, "//evil.example.com")]
    [InlineData("en-US", null)]
    public void Set_UnknownOrExternal_FallsBack(string? culture, string? returnUrl)
    {
        var controller = Controller();

        var result = Assert.IsType<LocalRedirectResult>(controller.Set(culture, returnUrl));

        Assert.Equal("/", result.Url);
        Assert.Contains(Uri.EscapeDataString("c=en-US|uic=en-US"), controller.Response.Headers.SetCookie.ToString());
    }

    /// <summary>Resources of IStringLocalizer&lt;SharedResource&gt;.</summary>
    private static readonly ResourceManager Resources = new("InstituteOfFineArts.Web.Resources.SharedResource", typeof(SharedResource).Assembly);

    /// <summary>English output stays byte-identical: every English value equals its key.</summary>
    [Fact]
    public void Resx_EnglishValuesEqualKeys()
    {
        var set = Resources.GetResourceSet(CultureInfo.InvariantCulture, createIfNotExists: true, tryParents: false)!;
        var entries = set.Cast<DictionaryEntry>().ToList();

        Assert.True(entries.Count >= 30);
        Assert.All(entries, e => Assert.Equal(e.Key, e.Value));
        Assert.Equal("Invalid username or password.", Resources.GetString("Invalid username or password.", new CultureInfo("en-US")));
    }

    /// <summary>Every key has a Vietnamese translation, and only known keys are translated.</summary>
    [Fact]
    public void Resx_VietnameseCoversEveryKey()
    {
        var english = Resources.GetResourceSet(CultureInfo.InvariantCulture, true, false)!.Cast<DictionaryEntry>()
            .Select(e => (string)e.Key).ToHashSet();
        var vietnamese = Resources.GetResourceSet(new CultureInfo("vi-VN"), true, false)!.Cast<DictionaryEntry>().ToList();

        Assert.Equal(english.OrderBy(k => k), vietnamese.Select(e => (string)e.Key).OrderBy(k => k));
        Assert.All(vietnamese, e => Assert.False(string.IsNullOrWhiteSpace((string?)e.Value)));
        Assert.Equal("Tên đăng nhập hoặc mật khẩu không đúng.", Resources.GetString("Invalid username or password.", new CultureInfo("vi-VN")));
        Assert.Equal("Đăng nhập", Resources.GetString("Log in", new CultureInfo("vi-VN")));
    }

    /// <summary>Error page texts (ErrorController.Describe) and login / profile messages all have a key.</summary>
    [Fact]
    public void Resx_HasKeysForUserMessages()
    {
        var keys = Resources.GetResourceSet(CultureInfo.InvariantCulture, true, false)!.Cast<DictionaryEntry>()
            .Select(e => (string)e.Key).ToHashSet();

        foreach (var code in new[] { 400, 401, 403, 404, 405, 413, 500, 418 })
        {
            var vm = ErrorController.Describe(code);
            Assert.Contains(vm.Title, keys);
            Assert.Contains(vm.Message, keys);
        }
        Assert.Contains(AccountController.InvalidCredentialsMessage, keys);
        Assert.Contains(AccountController.LockedMessage, keys);
        Assert.Contains(ProfileController.ProfileSavedMessage, keys);
        Assert.Contains(ProfileController.PasswordChangedMessage, keys);
        Assert.Contains(Web.Services.Implementations.ProfileService.WrongCurrentPasswordMessage, keys);
        Assert.Contains(Web.Services.Implementations.ProfileService.SamePasswordMessage, keys);
        Assert.Contains(new Web.Validation.StrongPasswordAttribute().FormatErrorMessage("New password"), keys);
    }
}
