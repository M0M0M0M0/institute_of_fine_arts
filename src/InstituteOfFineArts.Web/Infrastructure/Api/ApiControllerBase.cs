using InstituteOfFineArts.Web.Exceptions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace InstituteOfFineArts.Web.Infrastructure.Api;

/// <summary>
/// Base class of every /api controller used by the mobile app (folder Controllers/Api).
/// <list type="bullet">
/// <item>Signed-in user from the JWT bearer token (the web cookie is not accepted here)</item>
/// <item>No anti-forgery check: a bearer token cannot be sent by another site the way a cookie is</item>
/// <item>Errors as ProblemDetails JSON: NotFoundException → 404, BusinessException → 400, title = the service's message</item>
/// </list>
/// Add [Authorize(Roles = RoleNames.Student)] etc. on the derived controller, or [AllowAnonymous] for public data.
/// </summary>
[ApiController]
[Produces("application/json")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[IgnoreAntiforgeryToken]
[EnableCors(ApiSetup.CorsPolicy)]
[ApiExceptionFilter]
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>ProblemDetails with the given status and message (shown as-is by the app).</summary>
    protected ObjectResult Problem(int status, string title, IDictionary<string, object?>? extensions = null)
    {
        var problem = new ProblemDetails { Status = status, Title = title };
        if (extensions != null)
        {
            foreach (var (key, value) in extensions) problem.Extensions[key] = value;
        }
        return new ObjectResult(problem) { StatusCode = status, ContentTypes = { "application/problem+json" } };
    }

    /// <summary>409 when the client's rowVersion is not the current one (someone else changed the record).</summary>
    protected ObjectResult StaleVersion()
        => Problem(StatusCodes.Status409Conflict, "This record was changed by someone else. Reload it and try again.");

    /// <summary>409 for a feature of the mobile app this system does not support (the matching can… flag is false).</summary>
    protected ObjectResult NotSupported(string what)
        => Problem(StatusCodes.Status409Conflict, $"{what} is not supported by this system.");
}

/// <summary>Turns the services' exceptions into ProblemDetails (see <see cref="ApiControllerBase"/>).</summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class ApiExceptionFilterAttribute : ExceptionFilterAttribute
{
    public override void OnException(ExceptionContext context)
    {
        (int Status, string Title)? mapped = context.Exception switch
        {
            NotFoundException e => (StatusCodes.Status404NotFound, e.Message),
            BusinessException e => (StatusCodes.Status400BadRequest, e.Message),
            UnauthorizedAccessException e => (StatusCodes.Status403Forbidden, string.IsNullOrWhiteSpace(e.Message) ? "You do not have permission to do this." : e.Message),
            _ => null,
        };
        if (mapped == null) return; // unexpected → 500 ProblemDetails from ApiSetup.UseErrorHandling

        var problem = new ProblemDetails { Status = mapped.Value.Status, Title = mapped.Value.Title };
        if (context.Exception is BusinessException { Field: { } field })
        {
            problem.Extensions["errors"] = new Dictionary<string, string[]> { [field] = [mapped.Value.Title] };
        }
        context.Result = new ObjectResult(problem) { StatusCode = mapped.Value.Status, ContentTypes = { "application/problem+json" } };
        context.ExceptionHandled = true;
    }
}

/// <summary>
/// Optimistic concurrency token for the app's "rowVersion" fields. The database has no rowversion column, so the
/// token is the record's last change time (UpdatedAt, else CreatedAt) in ticks: a client that sends back an older
/// value gets 409 instead of silently overwriting someone else's change.
/// </summary>
public static class RowVersion
{
    /// <summary>Token of a record that was last changed at <paramref name="updatedAt"/> (or created at <paramref name="createdAt"/>).</summary>
    public static string Of(DateTime createdAt, DateTime? updatedAt)
    {
        // Whole seconds: the columns keep no fractions (HasPrecision(0)), so a value just saved and the same value
        // read back later give the same token
        var t = (updatedAt ?? createdAt).Ticks;
        return (t - t % TimeSpan.TicksPerSecond).ToString();
    }

    /// <summary>True when the client sent no token or the current one.</summary>
    public static bool Matches(string? clientValue, DateTime createdAt, DateTime? updatedAt)
        => string.IsNullOrWhiteSpace(clientValue) || clientValue == Of(createdAt, updatedAt);
}
