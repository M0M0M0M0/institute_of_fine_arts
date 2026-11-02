using System.Security.Claims;
using InstituteOfFineArts.Web.Data;
using InstituteOfFineArts.Web.Helpers;
using InstituteOfFineArts.Web.Infrastructure.Security;
using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace InstituteOfFineArts.Web.Services.Implementations;

/// <summary>
/// Xác thực người dùng: so mật khẩu bằng BCrypt, chặn tài khoản bị khóa, tạo Claims cho cookie đăng nhập.
/// </summary>
public class AuthService : IAuthService
{
    /// <summary>
    /// Hash BCrypt giả dùng khi không tìm thấy tài khoản: vẫn chạy BCrypt.Verify để thời gian xử lý
    /// giống trường hợp sai mật khẩu → kẻ tấn công không đoán được username nào tồn tại.
    /// </summary>
    private static readonly string DummyHash = BCrypt.Net.BCrypt.HashPassword("khong-dung-de-dang-nhap");

    /// <summary>Truy cập CSDL.</summary>
    private readonly AppDbContext _db;

    /// <summary>Ghi log đăng nhập thành công / thất bại.</summary>
    private readonly ILogger<AuthService> _logger;

    /// <summary>Nhận các dịch vụ cần dùng qua Dependency Injection.</summary>
    public AuthService(AppDbContext db, ILogger<AuthService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<LoginResult> LoginAsync(string username, string password, string authenticationScheme)
    {
        // Bỏ khoảng trắng thừa ở tên đăng nhập; truy vấn qua LINQ nên được tham số hóa (chống SQL injection)
        var name = (username ?? string.Empty).Trim();
        var account = await _db.Accounts
            .Include(a => a.StaffProfile)
            .Include(a => a.StudentProfile)
            .FirstOrDefaultAsync(a => a.Username == name);

        // Không có tài khoản: vẫn chạy BCrypt với hash giả để thời gian phản hồi như khi sai mật khẩu
        if (account == null)
        {
            BCrypt.Net.BCrypt.Verify(password ?? string.Empty, DummyHash);
            _logger.LogWarning("Đăng nhập thất bại: không tồn tại username {Username}", name);
            return new LoginResult(LoginStatus.InvalidCredentials);
        }

        // Sai mật khẩu
        if (!BCrypt.Net.BCrypt.Verify(password ?? string.Empty, account.PasswordHash))
        {
            _logger.LogWarning("Đăng nhập thất bại: sai mật khẩu cho {Username}", name);
            return new LoginResult(LoginStatus.InvalidCredentials);
        }

        // Đúng mật khẩu nhưng tài khoản bị khóa (BR-05) – chỉ báo "bị khóa" khi mật khẩu đúng
        if (!account.Active)
        {
            _logger.LogWarning("Đăng nhập bị chặn: tài khoản {Username} đã bị khóa", name);
            return new LoginResult(LoginStatus.Locked);
        }

        // Ghi lại thời điểm đăng nhập gần nhất
        account.LastLoginAt = DateTimeHelper.Now;
        await _db.SaveChangesAsync();

        // Id hồ sơ: staff_profile với ADMIN/MANAGER/STAFF, student_profile với STUDENT
        long? profileId = account.Role == Role.STUDENT ? account.StudentProfile?.Id : account.StaffProfile?.Id;

        // Tạo Claims lưu trong cookie (quy ước ở Infrastructure/Security/ClaimsPrincipalExtensions)
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, account.Id.ToString()),
            new(ClaimTypes.Name, account.Username),
            new(ClaimTypes.GivenName, account.FullName),
            new(ClaimTypes.Role, account.Role.ToString()),
        };
        if (profileId.HasValue)
        {
            claims.Add(new Claim(ClaimsPrincipalExtensions.ProfileIdClaim, profileId.Value.ToString()));
        }

        _logger.LogInformation("Đăng nhập thành công: {Username} ({Role})", account.Username, account.Role);
        var identity = new ClaimsIdentity(claims, authenticationScheme);
        return new LoginResult(LoginStatus.Success, new ClaimsPrincipal(identity));
    }

    /// <inheritdoc />
    public string GetHomePath(ClaimsPrincipal user)
    {
        // Mỗi vai trò có Area riêng; vai trò lạ thì về trang chủ
        if (user.IsInRole(RoleNames.Admin)) return "/admin";
        if (user.IsInRole(RoleNames.Manager)) return "/manager";
        if (user.IsInRole(RoleNames.Staff)) return "/staff";
        if (user.IsInRole(RoleNames.Student)) return "/student";
        return "/";
    }
}
