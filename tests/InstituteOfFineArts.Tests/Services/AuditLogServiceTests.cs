using System.Net;
using System.Security.Claims;
using InstituteOfFineArts.Web.Data;
using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Helpers;
using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Services.Implementations;
using InstituteOfFineArts.Web.ViewModels.Admin.AuditLog;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace InstituteOfFineArts.Tests.Services;

/// <summary>
/// AuditLogService (F-M17-01): writes rows for the current user / an explicit user, never breaks the caller,
/// and searches by user, action and date range, newest first.
/// </summary>
public class AuditLogServiceTests
{
    /// <summary>Options of a new in-memory database (shared by the service's read and write contexts).</summary>
    private static DbContextOptions<AppDbContext> NewOptions()
        => new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;

    /// <summary>HTTP context of a signed-in staff member (account 7, "staff01") calling from 10.0.0.5.</summary>
    private static HttpContext StaffRequest()
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "7"), new Claim(ClaimTypes.Name, "staff01"),
                new Claim(ClaimTypes.Role, RoleNames.Staff),
            }, "Test")),
        };
        context.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.5");
        return context;
    }

    /// <summary>Creates the service for the given database and HTTP context.</summary>
    private static AuditLogService CreateService(DbContextOptions<AppDbContext> options, AppDbContext db, HttpContext? http)
        => new(db, options, new HttpContextAccessor { HttpContext = http }, NullLogger<AuditLogService>.Instance);

    /// <summary>LogAsync: account, user name and IP come from the current request.</summary>
    [Fact]
    public async Task Log_CurrentUser()
    {
        var options = NewOptions();
        using var db = new AppDbContext(options);

        await CreateService(options, db, StaffRequest()).LogAsync("Competitions/Delete", "Competitions", 12, "deleted");

        var row = Assert.Single(db.AuditLogs.AsNoTracking());
        Assert.Equal(7, row.AccountId);
        Assert.Equal("staff01", row.Username);
        Assert.Equal("Competitions/Delete", row.Action);
        Assert.Equal("Competitions", row.Entity);
        Assert.Equal(12, row.EntityId);
        Assert.Equal("deleted", row.Details);
        Assert.Equal("10.0.0.5", row.IpAddress);
    }

    /// <summary>LogAsAsync for a failed log in: anonymous (no account), the tried user name, no password anywhere.</summary>
    [Fact]
    public async Task LogAs_FailedLogin_Anonymous()
    {
        var options = NewOptions();
        using var db = new AppDbContext(options);
        var http = new DefaultHttpContext();

        await CreateService(options, db, http).LogAsAsync(null, "  student01 ", AuditLogService.LoginFailedAction, "Invalid username or password");

        var row = Assert.Single(db.AuditLogs.AsNoTracking());
        Assert.Null(row.AccountId);
        Assert.Equal("student01", row.Username);
        Assert.Equal(AuditLogService.LoginFailedAction, row.Action);
    }

    /// <summary>Texts longer than the columns are cut (user name 50, details 500) instead of failing.</summary>
    [Fact]
    public async Task Log_LongTexts_Cut()
    {
        var options = NewOptions();
        using var db = new AppDbContext(options);

        await CreateService(options, db, null).LogAsAsync(null, new string('u', 80), "LOGIN_FAILED", new string('d', 900));

        var row = Assert.Single(db.AuditLogs.AsNoTracking());
        Assert.Equal(50, row.Username!.Length);
        Assert.Equal(500, row.Details!.Length);
        Assert.Null(row.IpAddress);
    }

    /// <summary>A logging failure never reaches the caller (the user's action must not break).</summary>
    [Fact]
    public async Task Log_Failure_Swallowed()
    {
        var options = NewOptions();
        using var db = new AppDbContext(options);
        var http = new Mock<HttpContext>();
        http.SetupGet(h => h.User).Returns(new ClaimsPrincipal(new ClaimsIdentity()));
        http.SetupGet(h => h.Connection).Throws(new InvalidOperationException("boom"));

        await CreateService(options, db, http.Object).LogAsync("LOGOUT");

        Assert.Empty(db.AuditLogs);
    }

    /// <summary>Search: newest first; filters by part of the user name, part of the action and date range.</summary>
    [Fact]
    public async Task Search_FiltersAndOrder()
    {
        var options = NewOptions();
        using var db = new AppDbContext(options);
        var day = new DateTime(2026, 11, 2);
        var rows = new[]
        {
            (new AuditLog { Username = "staff01", Action = "LOGIN" }, day.AddHours(8)),
            (new AuditLog { Username = "staff01", Action = "Competitions/Delete" }, day.AddHours(23).AddMinutes(59)),
            (new AuditLog { Username = "student02", Action = "LOGIN" }, day.AddDays(1).AddHours(1)),
            (new AuditLog { Username = null, Action = "LOGIN_FAILED" }, day.AddDays(-1)),
        };
        db.AuditLogs.AddRange(rows.Select(r => r.Item1));
        await db.SaveChangesAsync();

        // SaveChanges stamps CreatedAt with "now" on insert: set the test times with a second save
        foreach (var (row, time) in rows)
        {
            row.CreatedAt = time;
        }
        await db.SaveChangesAsync();
        var service = CreateService(options, db, null);

        // No filter: all rows, newest first
        var all = await service.SearchAsync(new AuditLogSearchVm());
        Assert.Equal(4, all.TotalCount);
        Assert.Equal("student02", all.Items[0].Username);

        // User + action (SQL Server also matches case-insensitively; InMemory is case-sensitive)
        var mine = await service.SearchAsync(new AuditLogSearchVm { User = "staff", Activity = "Competitions/" });
        Assert.Equal("Competitions/Delete", Assert.Single(mine.Items).Action);

        // One day, the To day included up to 23:59
        var oneDay = await service.SearchAsync(new AuditLogSearchVm { From = DateOnly.FromDateTime(day), To = DateOnly.FromDateTime(day) });
        Assert.Equal(2, oneDay.TotalCount);
        Assert.All(oneDay.Items, i => Assert.Equal("staff01", i.Username));
    }

    /// <summary>Search: From after To is refused (TC-M10-03 message).</summary>
    [Fact]
    public async Task Search_InvalidRange_Blocked()
    {
        var options = NewOptions();
        using var db = new AppDbContext(options);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => CreateService(options, db, null)
            .SearchAsync(new AuditLogSearchVm { From = new DateOnly(2026, 11, 5), To = new DateOnly(2026, 11, 1) }));
        Assert.Equal(DateTimeHelper.InvalidDateRangeMessage, ex.Message);
    }
}
