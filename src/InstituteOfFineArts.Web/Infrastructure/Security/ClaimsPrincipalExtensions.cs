using System.Security.Claims;

namespace InstituteOfFineArts.Web.Infrastructure.Security;

/// <summary>
/// Hàm tiện ích đọc thông tin người dùng đang đăng nhập từ cookie (Claims).
/// Quy ước Claims do AccountController (M01) tạo khi đăng nhập:
/// NameIdentifier = account.id, Name = username, GivenName = họ tên, Role = vai trò,
/// ProfileId = id của staff_profile hoặc student_profile.
/// </summary>
public static class ClaimsPrincipalExtensions
{
    /// <summary>Tên claim chứa id hồ sơ (staff_profile.id hoặc student_profile.id).</summary>
    public const string ProfileIdClaim = "ProfileId";

    /// <summary>Lấy account.id của người đang đăng nhập; ném lỗi nếu chưa đăng nhập.</summary>
    public static long GetAccountId(this ClaimsPrincipal user)
    {
        // Đọc claim NameIdentifier và chuyển sang số
        var value = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return long.TryParse(value, out var id)
            ? id
            : throw new InvalidOperationException("Người dùng chưa đăng nhập.");
    }

    /// <summary>
    /// Lấy id hồ sơ: staff_profile.id với ADMIN/MANAGER/STAFF, student_profile.id với STUDENT.
    /// Dùng khi ghi created_by, evaluated_by, student_id…
    /// </summary>
    public static long GetProfileId(this ClaimsPrincipal user)
    {
        // Đọc claim ProfileId và chuyển sang số
        var value = user.FindFirstValue(ProfileIdClaim);
        return long.TryParse(value, out var id)
            ? id
            : throw new InvalidOperationException("Không tìm thấy hồ sơ của người dùng.");
    }

    /// <summary>Lấy họ tên hiển thị của người đang đăng nhập.</summary>
    public static string GetFullName(this ClaimsPrincipal user)
        => user.FindFirstValue(ClaimTypes.GivenName) ?? user.Identity?.Name ?? string.Empty;
}
