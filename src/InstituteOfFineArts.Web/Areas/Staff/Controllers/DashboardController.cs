using Microsoft.AspNetCore.Mvc;

namespace InstituteOfFineArts.Web.Areas.Staff.Controllers;

/// <summary>
/// Trang chính của phân hệ Giáo viên (/staff) – nơi chuyển tới sau khi đăng nhập.
/// Hiện là trang tạm; nội dung thật làm ở các task cuộc thi, chấm bài, trao giải, triển lãm.
/// </summary>
public class DashboardController : StaffBaseController
{
    /// <summary>Hiển thị trang chính của phân hệ.</summary>
    public IActionResult Index()
    {
        return View();
    }
}
