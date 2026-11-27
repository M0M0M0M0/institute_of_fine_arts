using System.Security.Claims;
using InstituteOfFineArts.Web.Controllers.Api;
using InstituteOfFineArts.Web.Data;
using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Helpers;
using InstituteOfFineArts.Web.Infrastructure.Push;
using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.Services.Implementations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace InstituteOfFineArts.Tests.Services;

/// <summary>
/// Push notifications to the mobile app (TODO step 10): phones register and remove their token, every in-app
/// notification goes to the receivers' phones, rejected and long-quiet tokens are forgotten, nothing is sent while
/// Firebase is not configured, and the notifications API of the app.
/// </summary>
public class PushServiceTests
{
    private readonly AppDbContext _db =
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private readonly Mock<IPushSender> _sender = new();
    private readonly List<(IReadOnlyCollection<string> Tokens, PushMessage Message)> _sent = [];
    private readonly long _anna, _binh;

    public PushServiceTests()
    {
        var a = new Account { Username = "student01", FullName = "Anna", Email = "a@ifa.local", Role = Role.STUDENT, PasswordHash = "x" };
        var b = new Account { Username = "student02", FullName = "Binh", Email = "b@ifa.local", Role = Role.STUDENT, PasswordHash = "x" };
        _db.Accounts.AddRange(a, b);
        _db.SaveChanges();
        _anna = a.Id;
        _binh = b.Id;

        _sender.SetupGet(s => s.IsEnabled).Returns(true);
        _sender.Setup(s => s.SendAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<PushMessage>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyCollection<string>, PushMessage, CancellationToken>((t, m, _) => _sent.Add((t.ToList(), m)))
            .ReturnsAsync((IReadOnlyCollection<string> t, PushMessage _, CancellationToken _) => new PushResult(t.Count, []));
    }

    private PushService Service() => new(_db, _sender.Object, NullLogger<PushService>.Instance);

    /// <summary>A phone is registered once; registering again refreshes it, another user signing in takes it over.</summary>
    [Fact]
    public async Task Register_OncePerPhone_MovesToTheNewUser()
    {
        var push = Service();
        await push.RegisterAsync(_anna, " tok-1 ", "Android");
        await push.RegisterAsync(_anna, "tok-1", "android");
        Assert.Equal(_anna, (await _db.DeviceTokens.SingleAsync()).AccountId);
        Assert.Equal("tok-1", (await _db.DeviceTokens.SingleAsync()).Token);

        await push.RegisterAsync(_binh, "tok-1", "android");
        Assert.Equal(_binh, (await _db.DeviceTokens.SingleAsync()).AccountId);
    }

    /// <summary>A missing token or an unknown platform is refused on its field.</summary>
    [Theory]
    [InlineData("", "android", "Token")]
    [InlineData("tok", "windows", "Platform")]
    public async Task Register_Invalid_Refused(string token, string platform, string field)
    {
        var ex = await Assert.ThrowsAsync<BusinessException>(() => Service().RegisterAsync(_anna, token, platform));
        Assert.Equal(field, ex.Field);
    }

    /// <summary>Signing out removes the phone; nobody can remove another user's phone.</summary>
    [Fact]
    public async Task Unregister_OnlyOwnPhone()
    {
        var push = Service();
        await push.RegisterAsync(_anna, "tok-a", "android");
        await push.UnregisterAsync(_binh, "tok-a");
        Assert.Equal(1, await _db.DeviceTokens.CountAsync());
        await push.UnregisterAsync(_anna, "tok-a");
        Assert.Equal(0, await _db.DeviceTokens.CountAsync());
    }

    /// <summary>A notification reaches every phone of the receivers only, with its link; a long text is cut.</summary>
    [Fact]
    public async Task Notify_PushesToTheReceiversPhones()
    {
        var push = Service();
        await push.RegisterAsync(_anna, "tok-a1", "android");
        await push.RegisterAsync(_anna, "tok-a2", "ios");
        await push.RegisterAsync(_binh, "tok-b", "android");
        var notifications = new NotificationService(_db, NullLogger<NotificationService>.Instance, push: push);

        await notifications.NotifyAsync([_anna], "Results are published", new string('x', 500), "/student/results", sendEmail: false);

        var (tokens, message) = Assert.Single(_sent);
        Assert.Equal(["tok-a1", "tok-a2"], tokens.Order());
        Assert.Equal("Results are published", message.Title);
        Assert.Equal("/student/results", message.Link);
        Assert.Equal(240, message.Body.Length);
        Assert.Equal(1, await _db.Notifications.CountAsync());
    }

    /// <summary>Tokens the push service rejected and phones quiet for too long are forgotten and get nothing.</summary>
    [Fact]
    public async Task Send_ForgetsRejectedAndQuietPhones()
    {
        _sender.Setup(s => s.SendAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<PushMessage>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyCollection<string>, PushMessage, CancellationToken>((t, m, _) => _sent.Add((t.ToList(), m)))
            .ReturnsAsync(new PushResult(1, ["tok-gone"]));
        var push = Service();
        await push.RegisterAsync(_anna, "tok-ok", "android");
        await push.RegisterAsync(_anna, "tok-gone", "android");
        await push.RegisterAsync(_anna, "tok-old", "android");
        (await _db.DeviceTokens.SingleAsync(d => d.Token == "tok-old")).LastSeenAt = DateTimeHelper.Now.AddDays(-90);
        await _db.SaveChangesAsync();

        await push.SendAsync([_anna], "Hello", "Text", null);

        Assert.DoesNotContain("tok-old", Assert.Single(_sent).Tokens);
        Assert.Equal(["tok-ok"], await _db.DeviceTokens.Select(d => d.Token).ToListAsync());
    }

    /// <summary>While Firebase is not configured nothing is sent, but phones can still register.</summary>
    [Fact]
    public async Task PushOff_NothingSent()
    {
        _sender.SetupGet(s => s.IsEnabled).Returns(false);
        var push = Service();
        await push.RegisterAsync(_anna, "tok-a", "android");

        Assert.Equal(0, await push.SendAsync([_anna], "Hello", "Text", null));
        Assert.Empty(_sent);
        Assert.Equal(1, await _db.DeviceTokens.CountAsync());
    }

    /// <summary>The app's list: own notifications only, unread count, mark one / all as read, register the phone.</summary>
    [Fact]
    public async Task Api_ListReadAndRegister()
    {
        var notifications = new NotificationService(_db, NullLogger<NotificationService>.Instance);
        await notifications.NotifyAsync([_anna], "First", "One", "/student/results", sendEmail: false);
        await notifications.NotifyAsync([_anna], "Second", "Two", null, sendEmail: false);
        await notifications.NotifyAsync([_binh], "Not yours", "Three", null, sendEmail: false);
        var api = new NotificationsApiController(notifications, Service())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, _anna.ToString())], "test")),
                },
            },
        };

        var page = (await api.List()).Value!;
        Assert.Equal(2, page.TotalCount);
        Assert.Equal(2, page.UnreadCount);
        Assert.DoesNotContain(page.Items, n => n.Title == "Not yours");

        var first = page.Items.Single(n => n.Title == "First");
        var read = (await api.MarkRead(first.Id)).Value!;
        Assert.Equal("/student/results", read.Link);
        Assert.Equal(1, read.UnreadCount);

        var other = await _db.Notifications.SingleAsync(n => n.AccountId == _binh);
        await Assert.ThrowsAsync<NotFoundException>(() => api.MarkRead(other.Id));

        Assert.Equal(0, (await api.MarkAllRead()).Value!.UnreadCount);
        Assert.Equal(0, (await api.UnreadCount()).Value!.UnreadCount);

        Assert.IsType<NoContentResult>(await api.RegisterDevice(new NotificationsApiController.DeviceDto { Token = "tok-a", Platform = "android" }));
        Assert.Equal(_anna, (await _db.DeviceTokens.SingleAsync()).AccountId);
        Assert.IsType<NoContentResult>(await api.UnregisterDevice(new NotificationsApiController.DeviceDto { Token = "tok-a" }));
        Assert.Equal(0, await _db.DeviceTokens.CountAsync());
    }
}
