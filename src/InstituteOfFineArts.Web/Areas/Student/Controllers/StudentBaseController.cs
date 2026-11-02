using InstituteOfFineArts.Web.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InstituteOfFineArts.Web.Areas.Student.Controllers;

/// <summary>
/// Controller gốc của phân hệ Học viên (Student): mọi controller trong Area Student kế thừa lớp này
/// nên chỉ người dùng có vai trò STUDENT mới truy cập được (sai vai trò → /error/403).
/// </summary>
/// <remarks>
/// Không cho trình duyệt lưu cache trang sau đăng nhập: sau khi đăng xuất, bấm Back không xem lại được dữ liệu (TC-M01-08).
/// </remarks>
[Area("Student")]
[Authorize(Roles = RoleNames.Student)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public abstract class StudentBaseController : Controller
{
}
