using Microsoft.AspNetCore.Mvc;

namespace InstituteOfFineArts.Web.Areas.Admin.Controllers;

/// <summary>
/// Trang chính của phân hệ Quản trị viên (/admin) – nơi chuyển tới sau khi đăng nhập.
/// Hiện là trang tạm; nội dung thật làm ở các task quản lý nhân viên, học viên (BE2-02, BE2-03).
/// </summary>
public class DashboardController : AdminBaseController
{
    /// <summary>Hiển thị trang chính của phân hệ.</summary>
    public IActionResult Index()
    {
        return View();
    }
}
