using InstituteOfFineArts.Web.Models.Enums;

namespace InstituteOfFineArts.Web.Models.Entities;

/// <summary>
/// Tài khoản đăng nhập + thông tin cá nhân chung (bảng account).
/// </summary>
public class Account : BaseEntity
{
    /// <summary>Tên đăng nhập, duy nhất (BR-01).</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>Mật khẩu đã băm BCrypt – không bao giờ lưu mật khẩu thô (BR-02).</summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>Họ và tên.</summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>Email, duy nhất (BR-01).</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Số điện thoại.</summary>
    public string? Phone { get; set; }

    /// <summary>Ngày sinh.</summary>
    public DateOnly? DateOfBirth { get; set; }

    /// <summary>Giới tính.</summary>
    public Gender? Gender { get; set; }

    /// <summary>Địa chỉ.</summary>
    public string? Address { get; set; }

    /// <summary>Vai trò – mỗi tài khoản đúng 1 vai trò (BR-03).</summary>
    public Role Role { get; set; }

    /// <summary>false = tài khoản bị khóa/vô hiệu hóa thay cho xóa cứng (BR-04, BR-05).</summary>
    public bool Active { get; set; } = true;

    /// <summary>Lần đăng nhập gần nhất.</summary>
    public DateTime? LastLoginAt { get; set; }

    /// <summary>Hồ sơ nhân sự (nếu vai trò là ADMIN, MANAGER, STAFF).</summary>
    public StaffProfile? StaffProfile { get; set; }

    /// <summary>Hồ sơ học viên (nếu vai trò là STUDENT).</summary>
    public StudentProfile? StudentProfile { get; set; }
}
