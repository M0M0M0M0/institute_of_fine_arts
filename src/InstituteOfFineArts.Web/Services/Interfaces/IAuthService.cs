using System.Security.Claims;

namespace InstituteOfFineArts.Web.Services.Interfaces;

/// <summary>
/// Kết quả kiểm tra đăng nhập.
/// </summary>
public enum LoginStatus
{
    /// <summary>Đúng tên đăng nhập và mật khẩu, tài khoản đang hoạt động.</summary>
    Success,

    /// <summary>Sai tên đăng nhập hoặc sai mật khẩu (không nói rõ sai cái nào).</summary>
    InvalidCredentials,

    /// <summary>Đúng mật khẩu nhưng tài khoản đã bị khóa (BR-05).</summary>
    Locked,
}

/// <summary>
/// Kết quả trả về cho AccountController sau khi kiểm tra đăng nhập.
/// </summary>
/// <param name="Status">Trạng thái đăng nhập</param>
/// <param name="Principal">Thông tin người dùng (Claims) để tạo cookie – chỉ có khi Status = Success</param>
public record LoginResult(LoginStatus Status, ClaimsPrincipal? Principal = null);

/// <summary>
/// Xác thực người dùng bằng bảng account (task BE1-02, module M01).
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Kiểm tra tên đăng nhập + mật khẩu; nếu đúng thì cập nhật thời điểm đăng nhập
    /// và trả về ClaimsPrincipal để controller tạo cookie.
    /// </summary>
    /// <param name="username">Tên đăng nhập người dùng nhập</param>
    /// <param name="password">Mật khẩu người dùng nhập (chưa băm)</param>
    /// <param name="authenticationScheme">Tên scheme xác thực dùng cho ClaimsIdentity</param>
    Task<LoginResult> LoginAsync(string username, string password, string authenticationScheme);

    /// <summary>
    /// Đường dẫn trang chính của từng vai trò sau khi đăng nhập
    /// (ADMIN → /admin, MANAGER → /manager, STAFF → /staff, STUDENT → /student).
    /// </summary>
    /// <param name="user">Người dùng đã đăng nhập</param>
    string GetHomePath(ClaimsPrincipal user);
}
