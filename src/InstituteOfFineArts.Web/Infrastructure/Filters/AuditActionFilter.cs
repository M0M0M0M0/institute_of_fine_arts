using InstituteOfFineArts.Web.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace InstituteOfFineArts.Web.Infrastructure.Filters;

/// <summary>
/// Global MVC filter (registered once in Program.cs) that writes an audit log row (F-M17-01) for every POST
/// in the Admin, Staff, Manager and Student areas – i.e. every state-changing action behind a login.
/// Action = "Controller/Action", entity = controller name, entity id = route value "id" (when numeric).
/// Details hold the area, the path and the outcome; form values are never logged (they may contain passwords).
/// </summary>
public class AuditActionFilter : IAsyncActionFilter
{
    /// <summary>Areas whose POST actions are logged.</summary>
    private static readonly HashSet<string> LoggedAreas = new(StringComparer.OrdinalIgnoreCase)
    {
        "Admin", "Staff", "Manager", "Student",
    };

    /// <summary>Writes the log rows.</summary>
    private readonly IAuditLogService _audit;

    /// <summary>Receives the audit log service through Dependency Injection (one filter instance per request).</summary>
    public AuditActionFilter(IAuditLogService audit)
    {
        _audit = audit;
    }

    /// <summary>Runs the action, then records it with its outcome.</summary>
    /// <param name="context">Action about to run</param>
    /// <param name="next">Runs the action</param>
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // Only POSTs of the logged areas; everything else just runs
        var area = context.RouteData.Values["area"]?.ToString();
        if (!HttpMethods.IsPost(context.HttpContext.Request.Method) || area == null || !LoggedAreas.Contains(area))
        {
            await next();
            return;
        }

        var executed = await next();

        // Outcome and HTTP status of the action
        var (succeeded, status) = Outcome(executed);
        var controller = context.RouteData.Values["controller"]?.ToString() ?? string.Empty;
        var action = context.RouteData.Values["action"]?.ToString() ?? string.Empty;
        long? entityId = long.TryParse(context.RouteData.Values["id"]?.ToString(), out var id) ? id : null;
        var details = $"{area}: POST {context.HttpContext.Request.Path} {(succeeded ? "succeeded" : "failed")}"
                      + (status.HasValue ? $" ({status})" : string.Empty);

        // The service never throws, so a logging failure cannot change the response
        await _audit.LogAsync($"{controller}/{action}", controller, entityId, details);
    }

    /// <summary>
    /// Success = the action did not throw and returned a redirect or a 2xx result, without a validation error
    /// (form shown again) or an error message for the next page (TempData["Error"]).
    /// </summary>
    /// <param name="executed">Executed action</param>
    /// <returns>Whether it succeeded, and the HTTP status (null when it threw)</returns>
    internal static (bool Succeeded, int? Status) Outcome(ActionExecutedContext executed)
    {
        // Unhandled exception → error page
        if (executed.Exception != null && !executed.ExceptionHandled)
        {
            return (false, null);
        }

        // Status of the result: redirects are 302, results without an explicit code are 200
        var status = executed.Result switch
        {
            RedirectResult or RedirectToActionResult or RedirectToRouteResult or LocalRedirectResult or RedirectToPageResult => 302,
            IStatusCodeActionResult { StatusCode: { } code } => code,
            _ => StatusCodes.Status200OK,
        };
        if (status is < 200 or >= 400)
        {
            return (false, status);
        }

        // Form shown again with validation errors, or a redirect carrying an error message: nothing was changed
        if (!executed.ModelState.IsValid || HasErrorMessage(executed.Controller))
        {
            return (false, status);
        }
        return (true, status);
    }

    /// <summary>True when the controller set TempData["Error"] (business rule refused the action).</summary>
    private static bool HasErrorMessage(object controller)
        => controller is Controller { TempData: { } tempData } && tempData.ContainsKey("Error");
}
