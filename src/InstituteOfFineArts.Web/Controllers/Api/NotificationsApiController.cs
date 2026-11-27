using System.ComponentModel.DataAnnotations;
using InstituteOfFineArts.Web.Infrastructure.Api;
using InstituteOfFineArts.Web.Infrastructure.Security;
using InstituteOfFineArts.Web.Services.Implementations;
using InstituteOfFineArts.Web.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace InstituteOfFineArts.Web.Controllers.Api;

/// <summary>
/// Notifications of the signed-in user in the mobile app (same list as the bell of the website, F-M12-02) and the
/// phones that receive them as push notifications. Every role; each user only sees and changes his own.
/// <list type="bullet">
/// <item>GET /api/notifications?page=1 – newest first, 20 per page</item>
/// <item>GET /api/notifications/unread-count</item>
/// <item>POST /api/notifications/{id}/read – returns the notification's link</item>
/// <item>POST /api/notifications/read-all</item>
/// <item>POST /api/devices – register the phone's push token; DELETE /api/devices – remove it (signing out)</item>
/// </list>
/// </summary>
[Route("api")]
public class NotificationsApiController : ApiControllerBase
{
    /// <summary>Notifications per page in the app.</summary>
    public const int PageSize = 20;

    private readonly INotificationService _notifications;
    private readonly IPushService _push;

    public NotificationsApiController(INotificationService notifications, IPushService push)
    {
        _notifications = notifications;
        _push = push;
    }

    /// <summary>One notification.</summary>
    public sealed record NotificationDto(long Id, string Title, string Message, string? Link, bool IsRead, DateTime CreatedAt);

    /// <summary>One page of notifications and the unread count (for the badge).</summary>
    public sealed record NotificationPageDto(IReadOnlyList<NotificationDto> Items, int Page, int TotalPages, int TotalCount, int UnreadCount);

    /// <summary>Unread count.</summary>
    public sealed record UnreadCountDto(int UnreadCount);

    /// <summary>Link opened after marking a notification as read.</summary>
    public sealed record ReadDto(string? Link, int UnreadCount);

    /// <summary>Phone to register or remove.</summary>
    public sealed class DeviceDto
    {
        /// <summary>FCM registration token.</summary>
        [Required, StringLength(PushService.MaxTokenLength)]
        public string Token { get; set; } = string.Empty;

        /// <summary>"android" or "ios" (ignored when removing).</summary>
        [StringLength(20)]
        public string Platform { get; set; } = "android";
    }

    /// <summary>The user's notifications, newest first.</summary>
    [HttpGet("notifications")]
    public async Task<ActionResult<NotificationPageDto>> List([FromQuery] int page = 1)
    {
        var me = User.GetAccountId();
        var result = await _notifications.GetListAsync(me, Math.Max(1, page), PageSize);
        var items = result.Items.Select(n => new NotificationDto(n.Id, n.Title, n.Message, n.Link, n.IsRead, n.CreatedAt)).ToList();
        return new NotificationPageDto(items, result.Page, result.TotalPages, result.TotalCount, await _notifications.CountUnreadAsync(me));
    }

    /// <summary>Number of unread notifications.</summary>
    [HttpGet("notifications/unread-count")]
    public async Task<ActionResult<UnreadCountDto>> UnreadCount()
        => new UnreadCountDto(await _notifications.CountUnreadAsync(User.GetAccountId()));

    /// <summary>Marks one notification as read; 404 for an unknown one or one of another user.</summary>
    [HttpPost("notifications/{id:long}/read")]
    public async Task<ActionResult<ReadDto>> MarkRead(long id)
    {
        var me = User.GetAccountId();
        var link = await _notifications.MarkReadAsync(id, me);
        return new ReadDto(link, await _notifications.CountUnreadAsync(me));
    }

    /// <summary>Marks every notification as read.</summary>
    [HttpPost("notifications/read-all")]
    public async Task<ActionResult<UnreadCountDto>> MarkAllRead()
    {
        await _notifications.MarkAllReadAsync(User.GetAccountId());
        return new UnreadCountDto(0);
    }

    /// <summary>Registers the phone for push notifications (the app calls it after signing in and on every start).</summary>
    [HttpPost("devices")]
    public async Task<IActionResult> RegisterDevice([FromBody] DeviceDto device)
    {
        await _push.RegisterAsync(User.GetAccountId(), device.Token, device.Platform);
        return NoContent();
    }

    /// <summary>Removes the phone (signing out): its pushes stop.</summary>
    [HttpDelete("devices")]
    public async Task<IActionResult> UnregisterDevice([FromBody] DeviceDto device)
    {
        await _push.UnregisterAsync(User.GetAccountId(), device.Token);
        return NoContent();
    }
}
