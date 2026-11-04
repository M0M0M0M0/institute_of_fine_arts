using System.Diagnostics;
using InstituteOfFineArts.Web.ViewModels.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace InstituteOfFineArts.Web.Controllers;

/// <summary>
/// Friendly error pages (F-M01-07, task BE1-03): /error/403, /error/404, /error/413, /error/500…
/// Reached through UseExceptionHandler("/error/500") and UseStatusCodePagesWithReExecute("/error/{0}") in Program.cs,
/// and through the cookie AccessDeniedPath (/error/403). Never shows exception details outside Development.
/// </summary>
[AllowAnonymous]
[IgnoreAntiforgeryToken] // a failed POST is re-executed here as POST; the page itself changes nothing
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class ErrorController : Controller
{
    /// <summary>Message for uploads larger than the limit (docs/plan/02 section 9.4).</summary>
    public const string TooLargeMessage = "The file is too large. Images must be 5 MB or smaller.";

    /// <summary>Logs unexpected exceptions with their stack trace.</summary>
    private readonly ILogger<ErrorController> _logger;

    /// <summary>Tells whether the app runs in Development (to show technical details).</summary>
    private readonly IWebHostEnvironment _environment;

    /// <summary>Receives the required services through Dependency Injection.</summary>
    public ErrorController(ILogger<ErrorController> logger, IWebHostEnvironment environment)
    {
        _logger = logger;
        _environment = environment;
    }

    /// <summary>
    /// Shows the error page for a status code. No HTTP method attribute: re-executed requests keep their
    /// original method (GET, POST…).
    /// The status code is read from the route values, not through an action parameter: model binding also prepares
    /// the form value provider, which fails on an oversized request and would leave the parameter at 0.
    /// </summary>
    [Route("/error/{code:int}")]
    public IActionResult Index()
    {
        _ = int.TryParse(RouteData.Values["code"]?.ToString(), out var code);

        // Unhandled exception (UseExceptionHandler): log it; an oversized upload becomes a 413 page
        var exception = HttpContext.Features.Get<IExceptionHandlerPathFeature>()?.Error;
        if (exception != null)
        {
            if (IsRequestTooLarge(exception))
            {
                code = StatusCodes.Status413PayloadTooLarge;
            }
            else
            {
                var path = HttpContext.Features.Get<IExceptionHandlerPathFeature>()?.Path;
                _logger.LogError(exception, "Unhandled exception on {Path}", path);
            }
        }

        // Only real error codes; anything else (e.g. /error/200) is treated as "not found"
        if (code < 400 || code > 599)
        {
            code = StatusCodes.Status404NotFound;
        }

        var vm = Describe(code);
        vm.RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        if (_environment.IsDevelopment() && exception != null && code == StatusCodes.Status500InternalServerError)
        {
            vm.DeveloperDetails = exception.ToString();
        }

        Response.StatusCode = code;
        return View("Index", vm);
    }

    /// <summary>Title and message shown for each status code.</summary>
    /// <param name="code">HTTP status code</param>
    public static ErrorVm Describe(int code) => code switch
    {
        400 => New(code, "Bad request", "The request could not be processed. Please go back and try again."),
        401 => New(code, "Please log in", "You need to log in to view this page."),
        403 => New(code, "Access denied", "You do not have permission to view this page."),
        404 => New(code, "Page not found", "The page you are looking for does not exist or has been removed."),
        405 => New(code, "Not allowed", "This action is not allowed on this page."),
        413 => New(code, "File too large", TooLargeMessage),
        >= 500 => New(code, "Something went wrong",
            "An unexpected error occurred. Please try again later or contact the administrator."),
        _ => New(code, "Error", "The request could not be completed."),
    };

    /// <summary>
    /// True when the exception means the request body was larger than the configured limit
    /// (Kestrel/IIS body limit or the multipart form limit).
    /// </summary>
    /// <param name="exception">Exception caught by UseExceptionHandler</param>
    public static bool IsRequestTooLarge(Exception exception)
    {
        // The limit error may be wrapped by MVC model binding, so walk the inner exceptions
        for (var e = exception; e != null; e = e.InnerException)
        {
            if (e is BadHttpRequestException { StatusCode: StatusCodes.Status413PayloadTooLarge })
            {
                return true;
            }
            if (e is InvalidDataException && e.Message.Contains("length limit", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>Builds an ErrorVm.</summary>
    private static ErrorVm New(int code, string title, string message)
        => new() { StatusCode = code, Title = title, Message = message };
}
