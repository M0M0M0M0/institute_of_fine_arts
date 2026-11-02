using InstituteOfFineArts.Web.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InstituteOfFineArts.Web.Areas.Manager.Controllers;

/// <summary>
/// Controller gốc của phân hệ Quản lý (Manager): mọi controller trong Area Manager kế thừa lớp này
/// nên chỉ người dùng có vai trò MANAGER mới truy cập được (sai vai trò → /error/403).
/// </summary>
/// <remarks>
/// Không cho trình duyệt lưu cache trang sau đăng nhập: sau khi đăng xuất, bấm Back không xem lại được dữ liệu (TC-M01-08).
/// </remarks>
[Area("Manager")]
[Authorize(Roles = RoleNames.Manager)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public abstract class ManagerBaseController : Controller
{
}
