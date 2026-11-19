using InstituteOfFineArts.Web.Infrastructure.Audit;
using InstituteOfFineArts.Web.Infrastructure.Filters;
using InstituteOfFineArts.Web.Models.Enums;
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
/// AuditActionFilter (F-M17-01): every POST of the Admin/Staff/Teacher/Student areas is logged as
/// "Controller/Action" with the route id, its outcome, the object's parents and sentence (IAuditTargetService) and
/// the changed fields a controller left in HttpContext.Items; GETs and public pages are not logged.
/// </summary>
public class AuditActionFilterTests
{
    /// <summary>Controller instance the filter inspects for TempData["Error"].</summary>
    private sealed class FakeController : Controller
    {
    }

    /// <summary>Everything one filter run needs.</summary>
    private sealed record Run(Mock<IAuditLogService> Audit, Mock<IAuditTargetService> Targets, ActionExecutingContext Context, FakeController Controller);

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
        // The target service finds the competition (no object id: the filter falls back to the route id)
        var targets = new Mock<IAuditTargetService>();
        targets.Setup(t => t.ResolveAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, long>?>()))
            .ReturnsAsync(new AuditTarget { CompetitionId = 5, Category = AuditCategory.COMPETITION, Summary = "Deleted the competition “Wildlife”" });
        return new Run(new Mock<IAuditLogService>(), targets, context, controller);
    }

    /// <summary>Runs the filter; the "action" returns <paramref name="result"/> or throws <paramref name="error"/>.</summary>
    private static async Task ExecuteAsync(Run run, IActionResult? result, Exception? error = null)
    {
        var filter = new AuditActionFilter(run.Audit.Object, run.Targets.Object);
        await filter.OnActionExecutionAsync(run.Context, () => Task.FromResult(
            new ActionExecutedContext(run.Context, new List<IFilterMetadata>(), run.Controller)
            {
                Result = result,
                Exception = error,
            }));
    }

    /// <summary>Verifies one row with the expected action, id, outcome word, competition and sentence.</summary>
    private static void VerifyLogged(Run run, long? id, string outcome)
        => run.Audit.Verify(a => a.RecordAsync(It.Is<AuditEntry>(e =>
            e.Action == "Competitions/Delete" && e.Entity == "Competitions" && e.EntityId == id
            && e.Details!.Contains(outcome) && e.Details.StartsWith("Staff: POST /staff/competitions/5/delete")
            && e.CompetitionId == 5 && e.Category == AuditCategory.COMPETITION
            && e.Summary!.StartsWith("Deleted the competition “Wildlife”"))), Times.Once);

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
        run.Audit.Verify(a => a.RecordAsync(It.IsAny<AuditEntry>()), Times.Never);
    }

    /// <summary>A refused action keeps its sentence with "(refused)", so the history shows the attempt.</summary>
    [Fact]
    public async Task Post_Refused_SentenceMarked()
    {
        var run = Create();
        await ExecuteAsync(run, new NotFoundResult());
        run.Audit.Verify(a => a.RecordAsync(It.Is<AuditEntry>(e => e.Summary == "Deleted the competition “Wildlife” (refused)")), Times.Once);
    }

    /// <summary>The fields a controller reported as changed are added to the details of a successful action.</summary>
    [Fact]
    public async Task Post_ChangesFromTheController_InDetails()
    {
        var run = Create();
        run.Context.HttpContext.Items[AuditItems.Changes] = new List<string> { "End date: 20 Nov 2026 → 25 Nov 2026", "Award added: Third Prize" };
        await ExecuteAsync(run, new RedirectToActionResult("Details", null, null));
        run.Audit.Verify(a => a.RecordAsync(It.Is<AuditEntry>(e =>
            e.Details!.EndsWith("Changes: End date: 20 Nov 2026 → 25 Nov 2026 | Award added: Third Prize"))), Times.Once);
    }

    /// <summary>A create: the new id from the redirect is passed to the target service to name the new object.</summary>
    [Fact]
    public async Task Post_Create_NewIdFromTheRedirect()
    {
        var run = Create(id: null);
        run.Targets.Setup(t => t.ResolveAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, long>?>()))
            .ReturnsAsync(new AuditTarget { Kind = "Competition", Summary = "Created the competition" });
        run.Targets.Setup(t => t.WithCreatedIdAsync(It.IsAny<AuditTarget>(), 42))
            .ReturnsAsync(new AuditTarget { Kind = "Competition", Id = 42, CompetitionId = 42, Summary = "Created the competition “Lunar”" });

        await ExecuteAsync(run, new RedirectToActionResult("Details", null, new { id = 42L }));

        run.Audit.Verify(a => a.RecordAsync(It.Is<AuditEntry>(e => e.EntityId == 42 && e.CompetitionId == 42
            && e.Summary == "Created the competition “Lunar”")), Times.Once);
    }

    /// <summary>An action that wrote its own row (HttpContext.Items[AuditItems.Done]) is not logged twice.</summary>
    [Fact]
    public async Task Post_AlreadyLogged_NotTwice()
    {
        var run = Create();
        run.Context.HttpContext.Items[AuditItems.Done] = true;
        await ExecuteAsync(run, new RedirectToActionResult("Index", null, null));
        run.Audit.Verify(a => a.RecordAsync(It.IsAny<AuditEntry>()), Times.Never);
    }
}
