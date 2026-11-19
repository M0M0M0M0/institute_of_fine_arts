using System.Reflection;
using InstituteOfFineArts.Web.Helpers;
using InstituteOfFineArts.Web.Infrastructure.Audit;
using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace InstituteOfFineArts.Web.Infrastructure.Filters;

/// <summary>
/// Global MVC filter (registered once in Program.cs) that writes the audit log (F-M17-01):
/// <list type="bullet">
/// <item>every POST of the Admin, Staff, Teacher and Student areas and every POST / PUT / DELETE of the mobile API by a
/// signed-in user – i.e. every change behind a login;</item>
/// <item>the GET actions marked <see cref="AuditViewAttribute"/> (opening or downloading an entry) when the user is
/// staff, a teacher or an administrator: who looked at which painting.</item>
/// </list>
/// Each row gets the object it acts on and where that object belongs (competition, exhibition, entry, account –
/// <see cref="IAuditTargetService"/>), a readable sentence, the outcome, and the changed fields a controller put in
/// <c>HttpContext.Items[AuditItems.Changes]</c>. Form values are never logged (they may contain passwords).
/// </summary>
public class AuditActionFilter : IAsyncActionFilter
{
    /// <summary>Areas whose POST actions are logged.</summary>
    private static readonly HashSet<string> LoggedAreas = new(StringComparer.OrdinalIgnoreCase)
    {
        "Admin", "Staff", "Teacher", "Student",
    };

    /// <summary>Controllers that write their own rows (log in / out) or are not worth logging (marking a notification read).</summary>
    private static readonly HashSet<string> SkippedControllers = new(StringComparer.OrdinalIgnoreCase)
    {
        "Account", "AuthApi", "Notifications",
    };

    /// <summary>Form fields that may name the parent of a new object (ids only).</summary>
    private static readonly string[] FormIdFields = ["CompetitionId", "ExhibitionId", "SubmissionId"];

    /// <summary>A second visit to the same entry within this time is not logged again.</summary>
    public static readonly TimeSpan ViewDedupeWindow = TimeSpan.FromMinutes(10);

    /// <summary>Writes the log rows.</summary>
    private readonly IAuditLogService _audit;

    /// <summary>Finds the object of the request and its parents.</summary>
    private readonly IAuditTargetService _targets;

    /// <summary>Receives the services through Dependency Injection (one filter instance per request).</summary>
    public AuditActionFilter(IAuditLogService audit, IAuditTargetService targets)
    {
        _audit = audit;
        _targets = targets;
    }

    /// <summary>Runs the action, then records it with its outcome.</summary>
    /// <param name="context">Action about to run</param>
    /// <param name="next">Runs the action</param>
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var http = context.HttpContext;
        var request = http.Request;
        var path = request.Path.Value ?? string.Empty;
        var area = context.RouteData.Values["area"]?.ToString();
        var controller = context.RouteData.Values["controller"]?.ToString() ?? string.Empty;
        var actionName = context.RouteData.Values["action"]?.ToString() ?? string.Empty;
        var signedIn = http.User.Identity?.IsAuthenticated == true;

        var isChange = !HttpMethods.IsGet(request.Method) && !HttpMethods.IsHead(request.Method)
                       && !SkippedControllers.Contains(controller)
                       && ((area != null && LoggedAreas.Contains(area)) || (signedIn && path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase)));
        var view = HttpMethods.IsGet(request.Method) ? ViewMarker(context) : null;
        var isView = view != null && signedIn
                     && (http.User.IsInRole(RoleNames.Staff) || http.User.IsInRole(RoleNames.Teacher) || http.User.IsInRole(RoleNames.Admin))
                     && !IsThumbnail(request);
        if (!isChange && !isView)
        {
            await next();
            return;
        }

        // Object and parents, read before the action runs (a deleted row cannot be looked up afterwards)
        AuditTarget target;
        try
        {
            target = await _targets.ResolveAsync(request.Method, path, FormIds(request));
        }
        catch (Exception)
        {
            target = new AuditTarget { Summary = $"{controller}/{actionName}" };
        }

        var executed = await next();
        if (http.Items[AuditItems.Done] is true)
        {
            return;
        }
        var (succeeded, status) = Outcome(executed);

        if (isView)
        {
            var download = view!.What == "download" || string.Equals(request.Query["download"], "true", StringComparison.OrdinalIgnoreCase);
            var entry = target.Label == null ? "the entry" : $"the entry “{target.Label}”";
            await _audit.RecordAsync(new AuditEntry
            {
                Action = download ? "DOWNLOAD" : "VIEW",
                Category = AuditCategory.SUBMISSION,
                Summary = (download ? $"Downloaded the original of {entry}" : $"Opened {entry}") + (succeeded ? string.Empty : " (refused)"),
                Details = $"GET {path} {(succeeded ? "succeeded" : "failed")}" + (status.HasValue ? $" ({status})" : string.Empty),
                Entity = "Submission", EntityId = target.SubmissionId,
                CompetitionId = target.CompetitionId, SubmissionId = target.SubmissionId,
                DedupeWindow = ViewDedupeWindow,
            });
            return;
        }

        // A create: the new id comes back in the redirect (or the 201 location) → name and parents of the new object
        if (target.Id == null && succeeded && CreatedId(executed.Result) is { } createdId && target.Kind != null)
        {
            try
            {
                target = await _targets.WithCreatedIdAsync(target, createdId);
            }
            catch (Exception)
            {
                // keep what was found before
            }
        }

        long? routeId = long.TryParse(context.RouteData.Values["id"]?.ToString(), out var id) ? id : null;
        var details = $"{area ?? "API"}: {request.Method} {path} {(succeeded ? "succeeded" : "failed")}"
                      + (status.HasValue ? $" ({status})" : string.Empty);
        if (succeeded && http.Items[AuditItems.Changes] is IEnumerable<string> changes && changes.Any())
        {
            details += " · Changes: " + string.Join(" | ", changes);
        }

        // The service never throws, so a logging failure cannot change the response
        await _audit.RecordAsync(new AuditEntry
        {
            Action = $"{controller}/{actionName}",
            Category = target.Category,
            Summary = target.Summary + (succeeded ? string.Empty : " (refused)"),
            Details = details,
            Entity = target.Kind ?? controller,
            EntityId = target.Id ?? routeId,
            CompetitionId = target.CompetitionId,
            ExhibitionId = target.ExhibitionId,
            SubmissionId = target.SubmissionId,
            TargetAccountId = target.TargetAccountId,
        });
    }

    /// <summary>The <see cref="AuditViewAttribute"/> of the action, if any.</summary>
    private static AuditViewAttribute? ViewMarker(ActionExecutingContext context)
        => (context.ActionDescriptor as ControllerActionDescriptor)?.MethodInfo.GetCustomAttribute<AuditViewAttribute>();

    /// <summary>Thumbnails of a list are not a visit to the entry.</summary>
    private static bool IsThumbnail(HttpRequest request)
        => string.Equals(request.Query["thumb"], "true", StringComparison.OrdinalIgnoreCase)
           || (request.Path.Value?.EndsWith("/thumb", StringComparison.OrdinalIgnoreCase) ?? false);

    /// <summary>Parent ids posted in the form (CompetitionId, ExhibitionId, SubmissionId); null when there is no form.</summary>
    private static IReadOnlyDictionary<string, long>? FormIds(HttpRequest request)
    {
        if (!request.HasFormContentType)
        {
            return null;
        }
        var ids = new Dictionary<string, long>();
        foreach (var field in FormIdFields)
        {
            if (long.TryParse(request.Form[field].FirstOrDefault(), out var value))
            {
                ids[field] = value;
            }
        }
        return ids;
    }

    /// <summary>Id of the object a create action made: route value "id" of its redirect or 201 response.</summary>
    private static long? CreatedId(IActionResult? result)
    {
        var values = result switch
        {
            RedirectToActionResult r => r.RouteValues,
            CreatedAtActionResult c => c.RouteValues,
            CreatedAtRouteResult c => c.RouteValues,
            _ => null,
        };
        return values != null && long.TryParse(values.GetValueOrDefault("id")?.ToString(), out var id) ? id : null;
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
