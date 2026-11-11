using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using InstituteOfFineArts.Web.Models;

namespace InstituteOfFineArts.Web.Controllers;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [HttpGet]
    public IActionResult Faq()
    {
        return View();
    }

    [HttpGet]
    public IActionResult Returns()
    {
        return View();
    }

    [HttpGet]
    public IActionResult Shipping()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
