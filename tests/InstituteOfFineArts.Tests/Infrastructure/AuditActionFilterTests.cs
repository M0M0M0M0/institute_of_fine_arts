using InstituteOfFineArts.Web.Infrastructure.Filters;
using InstituteOfFineArts.Web.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Moq;

namespace InstituteOfFineArts.Tests.Infrastructure;

/// <summary>
/// AuditActionFilter (F-M17-01): every POST of the Admin/Staff/Manager/Student areas is logged as
/// "Controller/Action" with the route id and its outcome; GETs and public pages are not logged.
/// </summary>
public class AuditActionFilterTests
{
    /// <summary>Controller instance the filter inspects for TempData["Error"].</summary>
    private sealed class FakeController : Controller
    {
    }

    /// <summary>Everything one filter run needs.</summary>
    private sealed record Run(Mock<IAuditLogService> Audit, ActionExecutingContext Context, FakeController Controller);

    /// <summary>Builds an action context for <paramref name="method"/> on Staff/Competitions/Delete/5 (or another area).</summary>
    private static Run Create(string method = "POST", string? area = "Staff", string? id = "5")
    {
        var http = new DefaultHttpContext();
        http.Request.Method = method;
        http.Request.Path = "/staff/competitions/5/delete";
        var routeData = new RouteData();
        if (area != null) routeData.Values["area"] = area;
        routeData.Values["controller"] = "Competitions";
        routeData.Values["action"] = "Delete";
        if (id != null) routeData.Values["id"] = id;

        var controller = new FakeController { TempData = new TempDataDictionary(http, Mock.Of<ITempDataProvider>()) };
        var actionContext = new ActionContext(http, routeData, new ActionDescriptor());
        var context = new ActionExecutingContext(actionContext, new List<IFilterMetadata>(), new Dictionary<string, object?>(), controller);
        return new Run(new Mock<IAuditLogService>(), context, controller);
    }

    /// <summary>Runs the filter; the "action" returns <paramref name="result"/> or throws <paramref name="error"/>.</summary>
    private static async Task ExecuteAsync(Run run, IActionResult? result, Exception? error = null)
    {
        var filter = new AuditActionFilter(run.Audit.Object);
        await filter.OnActionExecutionAsync(run.Context, () => Task.FromResult(
            new ActionExecutedContext(run.Context, new List<IFilterMetadata>(), run.Controller)
            {
                Result = result,
                Exception = error,
            }));
    }

    /// <summary>Verifies one LogAsync call with the expected action, id and outcome word.</summary>
    private static void VerifyLogged(Run run, long? id, string outcome)
        => run.Audit.Verify(a => a.LogAsync("Competitions/Delete", "Competitions", id,
            It.Is<string>(d => d.Contains(outcome) && d.StartsWith("Staff: POST /staff/competitions/5/delete"))), Times.Once);

    /// <summary>A redirect after the POST → logged as succeeded with the route id.</summary>
    [Fact]
    public async Task Post_Redirect_LoggedSucceeded()
    {
        var run = Create();
        await ExecuteAsync(run, new RedirectToActionResult("Index", null, null));
        VerifyLogged(run, 5, "succeeded (302)");
    }

    /// <summary>A 2xx result (e.g. a file) without errors → succeeded; a non-numeric id is left empty.</summary>
    [Fact]
    public async Task Post_Ok_NoNumericId_LoggedSucceeded()
    {
        var run = Create(id: "abc");
        await ExecuteAsync(run, new OkResult());
        VerifyLogged(run, null, "succeeded (200)");
    }

    /// <summary>404 → failed.</summary>
    [Fact]
    public async Task Post_NotFound_LoggedFailed()
    {
        var run = Create();
        await ExecuteAsync(run, new NotFoundResult());
        VerifyLogged(run, 5, "failed (404)");
    }

    /// <summary>The action threw → failed.</summary>
    [Fact]
    public async Task Post_Exception_LoggedFailed()
    {
        var run = Create();
        await ExecuteAsync(run, null, new InvalidOperationException());
        VerifyLogged(run, 5, "failed");
    }

    /// <summary>Form shown again with validation errors (200) → failed.</summary>
    [Fact]
    public async Task Post_InvalidModelState_LoggedFailed()
    {
        var run = Create();
        run.Context.ModelState.AddModelError("Title", "Required");
        await ExecuteAsync(run, new ViewResult());
        VerifyLogged(run, 5, "failed (200)");
    }

    /// <summary>Redirect with TempData["Error"] (business rule refused) → failed.</summary>
    [Fact]
    public async Task Post_RedirectWithError_LoggedFailed()
    {
        var run = Create();
        run.Controller.TempData["Error"] = "Cannot delete.";
        await ExecuteAsync(run, new RedirectToActionResult("Details", null, null));
        VerifyLogged(run, 5, "failed (302)");
    }

    /// <summary>GET requests and POSTs outside the four areas (e.g. /login) are not logged by the filter.</summary>
    [Theory]
    [InlineData("GET", "Staff")]
    [InlineData("POST", null)]
    [InlineData("POST", "Public")]
    public async Task NotLogged(string method, string? area)
    {
        var run = Create(method, area);
        await ExecuteAsync(run, new RedirectToActionResult("Index", null, null));
        run.Audit.Verify(a => a.LogAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<long?>(), It.IsAny<string?>()), Times.Never);
    }
}
