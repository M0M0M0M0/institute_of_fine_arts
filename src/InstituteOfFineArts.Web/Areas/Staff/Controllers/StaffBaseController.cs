using InstituteOfFineArts.Web.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InstituteOfFineArts.Web.Areas.Staff.Controllers;

/// <summary>
/// Controller gốc của phân hệ Giáo viên (Staff): mọi controller trong Area Staff kế thừa lớp này
/// nên chỉ người dùng có vai trò STAFF mới truy cập được (sai vai trò → /error/403).
/// </summary>
/// <remarks>
/// Không cho trình duyệt lưu cache trang sau đăng nhập: sau khi đăng xuất, bấm Back không xem lại được dữ liệu (TC-M01-08).
/// </remarks>
[Area("Staff")]
[Authorize(Roles = RoleNames.Staff)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public abstract class StaffBaseController : Controller
{
}
