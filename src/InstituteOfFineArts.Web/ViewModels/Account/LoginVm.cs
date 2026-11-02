using System.ComponentModel.DataAnnotations;

namespace InstituteOfFineArts.Web.ViewModels.Account;

/// <summary>
/// Dữ liệu form đăng nhập (trang /login).
/// </summary>
public class LoginVm
{
    /// <summary>Tên đăng nhập.</summary>
    [Required(ErrorMessage = "Vui lòng nhập tên đăng nhập")]
    [StringLength(50, ErrorMessage = "Tên đăng nhập tối đa 50 ký tự")]
    [Display(Name = "Tên đăng nhập")]
    public string Username { get; set; } = string.Empty;

    /// <summary>Mật khẩu (chưa băm).</summary>
    [Required(ErrorMessage = "Vui lòng nhập mật khẩu")]
    [DataType(DataType.Password)]
    [Display(Name = "Mật khẩu")]
    public string Password { get; set; } = string.Empty;

    /// <summary>Ghi nhớ đăng nhập 7 ngày.</summary>
    [Display(Name = "Ghi nhớ đăng nhập")]
    public bool RememberMe { get; set; }

    /// <summary>Trang người dùng định mở trước khi bị yêu cầu đăng nhập (chỉ chấp nhận đường dẫn nội bộ).</summary>
    public string? ReturnUrl { get; set; }
}
