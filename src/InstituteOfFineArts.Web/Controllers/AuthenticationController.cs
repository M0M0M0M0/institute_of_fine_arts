using Microsoft.AspNetCore.Mvc;

namespace InstituteOfFineArts.Web.Controllers
{
    public class AuthenticationController : Controller
    {
        [HttpGet]
        public IActionResult Login()
        {
            return View(); // Trỏ tới Views/Authentication/Login.cshtml
        }
    }
}
