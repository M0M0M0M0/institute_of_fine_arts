using InstituteOfFineArts.Web.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InstituteOfFineArts.Web.Areas.Admin.Controllers;

/// <summary>
/// Controller gốc của phân hệ Quản trị viên (Admin): mọi controller trong Area Admin kế thừa lớp này
/// nên chỉ người dùng có vai trò ADMIN mới truy cập được (sai vai trò → /error/403).
/// </summary>
/// <remarks>
/// Không cho trình duyệt lưu cache trang sau đăng nhập: sau khi đăng xuất, bấm Back không xem lại được dữ liệu (TC-M01-08).
/// </remarks>
[Area("Admin")]
[Authorize(Roles = RoleNames.Admin)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public abstract class AdminBaseController : Controller
{
}
