using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Infrastructure.Security;
using InstituteOfFineArts.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InstituteOfFineArts.Web.Controllers;

/// <summary>
/// "My notifications" for every signed-in role (optional feature M12, F-M12-02; tasks BE1-OPT 3 + BE2-OPT 5).
/// GET /notifications – list, newest first · POST /notifications/{id}/read – mark one as read and open its link ·
/// POST /notifications/read-all – mark all as read. A user only sees and changes his own notifications (404 otherwise).
/// </summary>
[Authorize]
[Route("notifications")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class NotificationsController : Controller
{
    /// <summary>Message after "Mark all as read".</summary>
    public const string AllReadMessage = "All notifications have been marked as read.";

    /// <summary>Notification service.</summary>
    private readonly INotificationService _notifications;

    /// <summary>Receives the notification service through Dependency Injection.</summary>
    public NotificationsController(INotificationService notifications)
    {
        _notifications = notifications;
    }

    /// <summary>Lists the signed-in user's notifications, 10 per page.</summary>
    /// <param name="page">Page number</param>
    [HttpGet("")]
    public async Task<IActionResult> Index(int page = 1)
        => View(await _notifications.GetListAsync(User.GetAccountId(), page));

    /// <summary>Opens a notification: marks it as read, then goes to its link (or back to the list).</summary>
    /// <param name="id">notification.id</param>
    [HttpPost("{id:long}/read")]
    public async Task<IActionResult> Read(long id)
    {
        string? link;
        try
        {
            link = await _notifications.MarkReadAsync(id, User.GetAccountId());
        }
        catch (NotFoundException)
        {
            return NotFound();
        }

        // Only links inside this site are followed (the column could never hold another site, but stay safe)
        return !string.IsNullOrEmpty(link) && Url.IsLocalUrl(link) ? LocalRedirect(link) : RedirectToAction(nameof(Index));
    }

    /// <summary>Marks every notification of the signed-in user as read.</summary>
    [HttpPost("read-all")]
    public async Task<IActionResult> ReadAll()
    {
        await _notifications.MarkAllReadAsync(User.GetAccountId());

        // Post/Redirect/Get
        TempData["Success"] = AllReadMessage;
        return RedirectToAction(nameof(Index));
    }
}
