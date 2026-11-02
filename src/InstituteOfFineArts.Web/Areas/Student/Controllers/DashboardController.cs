using Microsoft.AspNetCore.Mvc;

namespace InstituteOfFineArts.Web.Areas.Student.Controllers;

/// <summary>
/// Trang chính của phân hệ Học viên (/student) – nơi chuyển tới sau khi đăng nhập.
/// Hiện là trang tạm; nội dung thật làm ở các task cuộc thi, nộp bài, kết quả.
/// </summary>
public class DashboardController : StudentBaseController
{
    /// <summary>Hiển thị trang chính của phân hệ.</summary>
    public IActionResult Index()
    {
        return View();
    }
}
