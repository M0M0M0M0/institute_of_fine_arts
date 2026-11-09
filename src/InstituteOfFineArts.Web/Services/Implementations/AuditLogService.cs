using System.Security.Claims;
using InstituteOfFineArts.Web.Data;
using InstituteOfFineArts.Web.Helpers;
using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Services.Interfaces;
using InstituteOfFineArts.Web.ViewModels.Admin.AuditLog;
using InstituteOfFineArts.Web.ViewModels.Common;
using Microsoft.EntityFrameworkCore;

namespace InstituteOfFineArts.Web.Services.Implementations;

/// <summary>
/// Writes and reads table audit_log (F-M17-01).
/// Rows are written through a separate, short-lived AppDbContext: the request's own context may hold changes of an
/// action that just failed, and saving the log row with it would save (or fail on) those changes too.
/// </summary>
public class AuditLogService : IAuditLogService
{
    /// <summary>Action of a successful log in.</summary>
    public const string LoginAction = "LOGIN";

    /// <summary>Action of a failed log in (wrong user name / password or locked account).</summary>
    public const string LoginFailedAction = "LOGIN_FAILED";

    /// <summary>Action of a log out.</summary>
    public const string LogoutAction = "LOGOUT";

    /// <summary>Column sizes of audit_log (Data/Configurations/AuditLogConfiguration).</summary>
    private const int MaxUsername = 50, MaxAction = 50, MaxEntity = 50, MaxDetails = 500, MaxIp = 45;

    /// <summary>Database access for the log page (reads).</summary>
    private readonly AppDbContext _db;

    /// <summary>Options used to open a separate context for each log row.</summary>
    private readonly DbContextOptions<AppDbContext> _options;

    /// <summary>Gives the current user and client IP.</summary>
    private readonly IHttpContextAccessor _http;

    /// <summary>Reports log rows that could not be written.</summary>
    private readonly ILogger<AuditLogService> _logger;

    /// <summary>Receives the required services through Dependency Injection.</summary>
    public AuditLogService(AppDbContext db, DbContextOptions<AppDbContext> options, IHttpContextAccessor http,
        ILogger<AuditLogService> logger)
    {
        _db = db;
        _options = options;
        _http = http;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task LogAsync(string action, string? entity = null, long? entityId = null, string? details = null)
    {
        // Current user from the authentication cookie (anonymous → no account, no name)
        var user = _http.HttpContext?.User;
        long? accountId = long.TryParse(user?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
        var username = user?.Identity?.IsAuthenticated == true ? user.Identity.Name : null;
        return WriteAsync(accountId, username, action, entity, entityId, details);
    }

    /// <inheritdoc />
    public Task LogAsAsync(long? accountId, string? username, string action, string? details = null)
        => WriteAsync(accountId, username, action, null, null, details);

    /// <inheritdoc />
    public async Task<PagedResult<AuditLogListItemVm>> SearchAsync(AuditLogSearchVm search)
    {
        // From after To is refused like in the reports (TC-M10-03)
        DateTimeHelper.EnsureValidRange(search.From, search.To, nameof(search.From));

        var query = _db.AuditLogs.AsNoTracking().AsQueryable();

        // User: part of the user name
        if (!string.IsNullOrWhiteSpace(search.User))
        {
            var user = search.User.Trim();
            query = query.Where(l => l.Username != null && l.Username.Contains(user));
        }

        // Action: part of the action
        if (!string.IsNullOrWhiteSpace(search.Activity))
        {
            var action = search.Activity.Trim();
            query = query.Where(l => l.Action.Contains(action));
        }

        // Date range, both days included (created_at has a time part, so the upper bound is the next midnight)
        if (search.From is { } from)
        {
            var start = DateTimeHelper.StartOfDay(from);
            query = query.Where(l => l.CreatedAt >= start);
        }
        if (search.To is { } to)
        {
            var end = DateTimeHelper.StartOfNextDay(to);
            query = query.Where(l => l.CreatedAt < end);
        }

        // Newest first; the id breaks ties between rows written in the same second
        var rows = query
            .OrderByDescending(l => l.CreatedAt).ThenByDescending(l => l.Id)
            .Select(l => new AuditLogListItemVm
            {
                Id = l.Id,
                CreatedAt = l.CreatedAt,
                Username = l.Username,
                Action = l.Action,
                Entity = l.Entity,
                EntityId = l.EntityId,
                Details = l.Details,
                IpAddress = l.IpAddress,
            });
        return await PagedResult<AuditLogListItemVm>.CreateAsync(rows, search.Page);
    }

    /// <summary>Saves one log row in its own context; any error is logged and swallowed.</summary>
    private async Task WriteAsync(long? accountId, string? username, string action, string? entity, long? entityId,
        string? details)
    {
        try
        {
            var row = new AuditLog
            {
                AccountId = accountId,
                Username = Cut(username, MaxUsername),
                Action = Cut(action, MaxAction) ?? string.Empty,
                Entity = Cut(entity, MaxEntity),
                EntityId = entityId,
                Details = Cut(details, MaxDetails),
                IpAddress = Cut(_http.HttpContext?.Connection.RemoteIpAddress?.ToString(), MaxIp),
            };

            // Separate context: only this row is saved (see the class summary)
            await using var db = new AppDbContext(_options);
            db.AuditLogs.Add(row);
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // The user's action has already happened: never let the log break it
            _logger.LogError(ex, "Audit log row could not be written: {Action} by {Username}", action, username);
        }
    }

    /// <summary>Trims the text and cuts it to the column size; empty text becomes null.</summary>
    private static string? Cut(string? text, int max)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }
        var trimmed = text.Trim();
        return trimmed.Length <= max ? trimmed : trimmed[..max];
    }
}
