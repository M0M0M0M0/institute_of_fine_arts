using Microsoft.AspNetCore.Mvc;

namespace InstituteOfFineArts.Web.Areas.Manager.Controllers;

/// <summary>
/// Trang chính của phân hệ Quản lý (/manager) – nơi chuyển tới sau khi đăng nhập.
/// Hiện là trang tạm; nội dung thật làm ở các task xem tổng hợp, quản lý triển lãm (BE2-13).
/// </summary>
public class DashboardController : ManagerBaseController
{
    /// <summary>Hiển thị trang chính của phân hệ.</summary>
    public IActionResult Index()
    {
        return View();
    }
}
