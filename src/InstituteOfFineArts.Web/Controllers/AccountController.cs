using InstituteOfFineArts.Web.Services.Interfaces;
using InstituteOfFineArts.Web.ViewModels.Account;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InstituteOfFineArts.Web.Controllers;

/// <summary>
/// Đăng nhập / đăng xuất (module M01, task BE1-02).
/// URL cố định: GET/POST /login, POST /logout.
/// </summary>
public class AccountController : Controller
{
    /// <summary>Thông báo chung khi sai tên đăng nhập hoặc mật khẩu (không nói rõ sai cái nào).</summary>
    public const string InvalidCredentialsMessage = "Tên đăng nhập hoặc mật khẩu không đúng.";

    /// <summary>Thông báo khi tài khoản bị khóa.</summary>
    public const string LockedMessage = "Tài khoản đã bị khóa. Vui lòng liên hệ quản trị viên.";

    /// <summary>Thời gian giữ đăng nhập khi chọn "Ghi nhớ đăng nhập".</summary>
    private static readonly TimeSpan RememberMeDuration = TimeSpan.FromDays(7);

    /// <summary>Service xác thực.</summary>
    private readonly IAuthService _authService;

    /// <summary>Nhận service xác thực qua Dependency Injection.</summary>
    public AccountController(IAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>
    /// Hiển thị form đăng nhập. Nếu đã đăng nhập rồi thì chuyển thẳng về trang chính của vai trò.
    /// </summary>
    /// <param name="returnUrl">Trang định mở trước khi bị yêu cầu đăng nhập</param>
    [HttpGet("/login")]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        // Đã đăng nhập thì không cần hiện form nữa
        if (User.Identity?.IsAuthenticated == true)
        {
            return LocalRedirect(_authService.GetHomePath(User));
        }

        return View(new LoginVm { ReturnUrl = returnUrl });
    }

    /// <summary>
    /// Nhận form đăng nhập: kiểm tra tài khoản, tạo cookie, chuyển trang theo vai trò.
    /// </summary>
    /// <param name="form">Dữ liệu người dùng nhập</param>
    [HttpPost("/login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(LoginVm form)
    {
        // Thiếu tên đăng nhập / mật khẩu → hiện lại form kèm lỗi
        if (!ModelState.IsValid)
        {
            return View(form);
        }

        var result = await _authService.LoginAsync(form.Username, form.Password,
            CookieAuthenticationDefaults.AuthenticationScheme);

        // Đăng nhập không thành công: báo lỗi chung, không giữ lại mật khẩu đã nhập
        if (result.Status != LoginStatus.Success || result.Principal == null)
        {
            var message = result.Status == LoginStatus.Locked ? LockedMessage : InvalidCredentialsMessage;
            ModelState.AddModelError(string.Empty, message);
            form.Password = string.Empty;
            return View(form);
        }

        // Tạo cookie đăng nhập; "Ghi nhớ" thì cookie sống 7 ngày kể cả khi đóng trình duyệt
        var properties = new AuthenticationProperties { IsPersistent = form.RememberMe };
        if (form.RememberMe)
        {
            properties.ExpiresUtc = DateTimeOffset.UtcNow.Add(RememberMeDuration);
        }
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, result.Principal, properties);

        // Quay lại trang định mở (chỉ đường dẫn nội bộ – chống chuyển hướng sang trang lạ), nếu không thì về trang của vai trò
        if (!string.IsNullOrEmpty(form.ReturnUrl) && Url.IsLocalUrl(form.ReturnUrl))
        {
            return LocalRedirect(form.ReturnUrl);
        }
        return LocalRedirect(_authService.GetHomePath(result.Principal));
    }

    /// <summary>
    /// Đăng xuất: xóa cookie và về trang chủ. Chỉ nhận POST (có anti-forgery token) để tránh bị đăng xuất qua link lạ.
    /// </summary>
    [HttpPost("/logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return LocalRedirect("/");
    }
}
