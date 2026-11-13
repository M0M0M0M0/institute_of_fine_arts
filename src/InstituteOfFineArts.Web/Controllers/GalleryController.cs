using Microsoft.AspNetCore.Mvc;

namespace InstituteOfFineArts.Web.Controllers;

public class GalleryController : Controller
{
    // Trang hiển thị tất cả tác phẩm (See All Artworks)
    [HttpGet]
    public IActionResult GalleryIndex()
    {
        return View(); // Trỏ tới Views/Gallery/Index.cshtml
    }

    // Nhóm SUBJECT (Animals, Flora, Landscape, People)
    [HttpGet]
    public IActionResult Subject(string category = "Animals")
    {
        ViewData["Category"] = category;
        // Tìm file tương ứng trong thư mục con: Views/Gallery/Subject/{category}.cshtml
        return View($"Subject/{category}");
    }

    // Nhóm STYLE (Abstract, Classical, Realism...)
    [HttpGet]
    public IActionResult Style(string category = "Abstract")
    {
        ViewData["Category"] = category;
        // Tìm file tương ứng trong thư mục con: Views/Gallery/Style/{category}.cshtml
        return View($"Style/{category}");
    }

    // Nhóm MEDIUM (OilPaintings, WatercolorPaintings, AcrylicPaintings...)
    [HttpGet]
    public IActionResult Medium(string category = "OilPaintings")
    {
        ViewData["Category"] = category;
        // Tìm file tương ứng trong thư mục con: Views/Gallery/Medium/{category}.cshtml
        return View($"Medium/{category}");
    }

    // Các mục nổi bật khác
    [HttpGet]
    public IActionResult NewArrivals()
    {
        return View(); // Views/Gallery/NewArrivals.cshtml
    }

    [HttpGet]
    public IActionResult CommissionsArt()
    {
        return View(); // Views/Gallery/CommissionArt.cshtml
    }

    // Trang danh sách các cuộc thi vẽ (Commissions)
    [HttpGet]
    public IActionResult Commissions()
    {
        return View(); // Tự động tìm tới Views/Gallery/Commissions.cshtml
    }

    [HttpGet]
    public IActionResult CommissionDetail(string id = "cultural-heritage")
    {
        // Tạm thời truyền cứng dữ liệu tên cuộc thi để test giao diện
        ViewData["CompetitionName"] = "Cultural Heritage";
        return View(); // Trỏ tới Views/Gallery/CommissionDetail.cshtml
    }

    // Trang danh sách bài thi để bình chọn
    [HttpGet]
    public IActionResult CommissionArt()
    {
        return View();
    }

    // Trang xem chi tiết một bài thi
    [HttpGet]
    public IActionResult CommissionArtDetail(int id = 1)
    {
        // Dùng id để sau này query database lấy thông tin bài thi tương ứng
        return View();
    }
}
