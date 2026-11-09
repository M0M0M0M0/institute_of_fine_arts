using InstituteOfFineArts.Web.Infrastructure.Security;
using InstituteOfFineArts.Web.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace InstituteOfFineArts.Web.ViewComponents;

/// <summary>
/// Bell in the menu (optional feature M12, F-M12-02): unread count and the 5 newest notifications of the signed-in
/// user. Used in _Layout next to "Profile"; view: Views/Shared/Components/NotificationBell/Default.cshtml.
/// </summary>
public class NotificationBellViewComponent : ViewComponent
{
    /// <summary>Notification service.</summary>
    private readonly INotificationService _notifications;

    /// <summary>Logs failures (the bell must never break a page).</summary>
    private readonly ILogger<NotificationBellViewComponent> _logger;

    /// <summary>Receives the required services through Dependency Injection.</summary>
    public NotificationBellViewComponent(INotificationService notifications, ILogger<NotificationBellViewComponent> logger)
    {
        _notifications = notifications;
        _logger = logger;
    }

    /// <summary>Renders the bell; nothing for anonymous visitors or when the notifications cannot be read.</summary>
    public async Task<IViewComponentResult> InvokeAsync()
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return Content(string.Empty);
        }

        try
        {
            return View(await _notifications.GetBellAsync(UserClaimsPrincipal.GetAccountId()));
        }
        catch (Exception ex)
        {
            // Every page shows the bell: a database problem here must not turn every page into an error page
            _logger.LogError(ex, "The notification bell could not be loaded");
            return Content(string.Empty);
        }
    }
}
