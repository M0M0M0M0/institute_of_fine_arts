using System.Security.Claims;
using InstituteOfFineArts.Web.Data;
using InstituteOfFineArts.Web.Helpers;
using InstituteOfFineArts.Web.Infrastructure.Audit;
using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.Services.Interfaces;
using InstituteOfFineArts.Web.ViewModels.Admin.AuditLog;
using InstituteOfFineArts.Web.ViewModels.Common;
using Microsoft.EntityFrameworkCore;

namespace InstituteOfFineArts.Web.Services.Implementations;

/// <summary>
/// Writes and reads table audit_log (F-M17-01).
/// Rows are written through a separate, short-lived AppDbContext: the request's own context may hold changes of an
/// action that just failed, and saving the log row with it would save (or fail on) those changes too.
/// Each row carries the competition / exhibition / entry / account it belongs to, so every page can show its own
/// history (<see cref="GetHistoryAsync"/>) instead of one flat list.
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
    private const int MaxUsername = 50, MaxAction = 50, MaxEntity = 50, MaxDetails = 1000, MaxSummary = 300, MaxIp = 45;

    /// <summary>Database access for the log pages (reads).</summary>
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
        => RecordAsync(new AuditEntry
        {
            Action = action, Entity = entity, EntityId = entityId, Details = details,
            Category = CategoryOfAction(action), Summary = SummaryOfAction(action),
        });

    /// <inheritdoc />
    public Task LogAsAsync(long? accountId, string? username, string action, string? details = null)
        => WriteAsync(accountId, username, new AuditEntry
        {
            Action = action, Details = details, Category = CategoryOfAction(action), Summary = SummaryOfAction(action),
        });

    /// <inheritdoc />
    public Task RecordAsync(AuditEntry entry)
    {
        // Current user from the authentication cookie / token (anonymous → no account, no name)
        var user = _http.HttpContext?.User;
        long? accountId = long.TryParse(user?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
        var username = user?.Identity?.IsAuthenticated == true ? user.Identity.Name : null;
        return WriteAsync(accountId, username, entry);
    }

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

        // Action: part of the technical action or of the sentence
        if (!string.IsNullOrWhiteSpace(search.Activity))
        {
            var action = search.Activity.Trim();
            query = query.Where(l => l.Action.Contains(action) || (l.Summary != null && l.Summary.Contains(action)));
        }

        // Kind of work, one competition, one exhibition
        if (search.Category is { } category)
        {
            query = query.Where(l => l.Category == category);
        }
        if (search.CompetitionId is { } competitionId)
        {
            query = query.Where(l => l.CompetitionId == competitionId);
        }
        if (search.ExhibitionId is { } exhibitionId)
        {
            query = query.Where(l => l.ExhibitionId == exhibitionId);
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

        return await PagedResult<AuditLogListItemVm>.CreateAsync(Rows(query), search.Page);
    }

    /// <inheritdoc />
    public async Task<PagedResult<AuditLogListItemVm>> GetHistoryAsync(AuditHistoryQuery history)
    {
        var query = _db.AuditLogs.AsNoTracking().AsQueryable();
        if (history.CompetitionId is { } competitionId)
        {
            query = query.Where(l => l.CompetitionId == competitionId);
        }
        if (history.ExhibitionId is { } exhibitionId)
        {
            query = query.Where(l => l.ExhibitionId == exhibitionId);
        }
        if (history.SubmissionId is { } submissionId)
        {
            query = query.Where(l => l.SubmissionId == submissionId);
        }
        if (history.AccountId is { } account)
        {
            query = query.Where(l => l.AccountId == account || l.TargetAccountId == account);
        }
        if (history.ActorAccountId is { } actor)
        {
            query = query.Where(l => l.AccountId == actor);
        }
        if (history.TargetAccountId is { } target)
        {
            query = query.Where(l => l.TargetAccountId == target);
        }
        if (history.Category is { } category)
        {
            query = query.Where(l => l.Category == category);
        }
        if (history.ExceptCategory is { } except)
        {
            query = query.Where(l => l.Category != except);
        }
        if (!string.IsNullOrWhiteSpace(history.User))
        {
            var user = history.User.Trim();
            query = query.Where(l => l.Username != null && l.Username.Contains(user));
        }
        return await PagedResult<AuditLogListItemVm>.CreateAsync(Rows(query), history.Page, history.PageSize);
    }

    /// <inheritdoc />
    public async Task<long?> AccountIdOfProfileAsync(bool student, long profileId)
        => student
            ? await _db.StudentProfiles.Where(p => p.Id == profileId).Select(p => (long?)p.AccountId).FirstOrDefaultAsync()
            : await _db.StaffProfiles.Where(p => p.Id == profileId).Select(p => (long?)p.AccountId).FirstOrDefaultAsync();

    /// <summary>Rows newest first (the id breaks ties between rows written in the same second), with the user's name today.</summary>
    private static IQueryable<AuditLogListItemVm> Rows(IQueryable<AuditLog> query)
        => query
            .OrderByDescending(l => l.CreatedAt).ThenByDescending(l => l.Id)
            .Select(l => new AuditLogListItemVm
            {
                Id = l.Id,
                CreatedAt = l.CreatedAt,
                AccountId = l.AccountId,
                Username = l.Username,
                FullName = l.Account == null ? null : l.Account.FullName,
                Role = l.Account == null ? null : l.Account.Role,
                Action = l.Action,
                Category = l.Category,
                Summary = l.Summary,
                Entity = l.Entity,
                EntityId = l.EntityId,
                CompetitionId = l.CompetitionId,
                ExhibitionId = l.ExhibitionId,
                SubmissionId = l.SubmissionId,
                TargetAccountId = l.TargetAccountId,
                Details = l.Details,
                IpAddress = l.IpAddress,
            });

    /// <summary>Kind of work of the log in / log out actions written by the account pages.</summary>
    private static AuditCategory CategoryOfAction(string action)
        => action is LoginAction or LoginFailedAction or LogoutAction ? AuditCategory.LOGIN : AuditCategory.OTHER;

    /// <summary>Sentence of the log in / log out actions.</summary>
    private static string? SummaryOfAction(string action) => action switch
    {
        LoginAction => "Signed in",
        LoginFailedAction => "Sign-in refused",
        LogoutAction => "Signed out",
        _ => null,
    };

    /// <summary>Saves one log row in its own context; any error is logged and swallowed.</summary>
    private async Task WriteAsync(long? accountId, string? username, AuditEntry entry)
    {
        try
        {
            var action = Cut(entry.Action, MaxAction) ?? string.Empty;
            await using var db = new AppDbContext(_options);

            // A refused sign-in has no user yet: link it to the account that was tried, so it shows in its login history
            long? targetAccountId = entry.TargetAccountId;
            if (accountId == null && targetAccountId == null && entry.Action == LoginFailedAction && !string.IsNullOrWhiteSpace(username))
            {
                var tried = username.Trim();
                targetAccountId = await db.Accounts.Where(a => a.Username == tried).Select(a => (long?)a.Id).FirstOrDefaultAsync();
            }

            // Same visit again (e.g. reloading the entry page): one row is enough
            if (entry.DedupeWindow is { } window && accountId != null)
            {
                var since = DateTimeHelper.Now - window;
                var seen = await db.AuditLogs.AnyAsync(l => l.AccountId == accountId && l.Action == action
                    && l.Entity == entry.Entity && l.EntityId == entry.EntityId && l.CreatedAt >= since);
                if (seen)
                {
                    return;
                }
            }

            db.AuditLogs.Add(new AuditLog
            {
                AccountId = accountId,
                Username = Cut(username, MaxUsername),
                Action = action,
                Category = entry.Category,
                Summary = Cut(entry.Summary, MaxSummary),
                Entity = Cut(entry.Entity, MaxEntity),
                EntityId = entry.EntityId,
                CompetitionId = entry.CompetitionId,
                ExhibitionId = entry.ExhibitionId,
                SubmissionId = entry.SubmissionId,
                TargetAccountId = targetAccountId,
                Details = Cut(entry.Details, MaxDetails),
                IpAddress = Cut(_http.HttpContext?.Connection.RemoteIpAddress?.ToString(), MaxIp),
            });
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // The user's action has already happened: never let the log break it
            _logger.LogError(ex, "Audit log row could not be written: {Action} by {Username}", entry.Action, username);
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
