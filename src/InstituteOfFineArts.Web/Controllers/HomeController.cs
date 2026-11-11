using InstituteOfFineArts.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InstituteOfFineArts.Web.Controllers;

/// <summary>
/// Public home pages (task BE2-11; F-M02-01..04, 06..08; screens SCR-01, SCR-06), open to guests and every role:
/// GET / (home) · GET /gallery (awarded paintings) · GET /about (static page) · GET /Home/Privacy · GET /faq, /returns, /shipping (static pages, FE101).
/// </summary>
[AllowAnonymous]
public class HomeController : Controller
{
    /// <summary>Public queries.</summary>
    private readonly IPublicSiteService _site;

    /// <summary>Receives the service through Dependency Injection.</summary>
    public HomeController(IPublicSiteService site)
    {
        _site = site;
    }

    /// <summary>Upcoming and ongoing competitions, latest winners and upcoming exhibitions.</summary>
    public async Task<IActionResult> Index()
        => View(await _site.GetHomeAsync());

    /// <summary>Thumbnails of the awarded paintings of GRADED competitions, paged.</summary>
    /// <param name="page">Page number (1-based)</param>
    [HttpGet("/gallery")]
    public async Task<IActionResult> Gallery(int page = 1)
        => View(await _site.GetGalleryAsync(page));

    /// <summary>Static page about the institute and how to contact it.</summary>
    [HttpGet("/about")]
    public IActionResult About()
        => View();

    /// <summary>Privacy page (template page, kept as is).</summary>
    public IActionResult Privacy()
    {
        return View();
    }

    /// <summary>Frequently asked questions (static page, FE101).</summary>
    [HttpGet("/faq")]
    public IActionResult Faq()
        => View();

    /// <summary>Returns and refunds of paintings bought at an exhibition (static page, FE101).</summary>
    [HttpGet("/returns")]
    public IActionResult Returns()
        => View();

    /// <summary>Shipping of paintings bought at an exhibition (static page, FE101).</summary>
    [HttpGet("/shipping")]
    public IActionResult Shipping()
        => View();
}
