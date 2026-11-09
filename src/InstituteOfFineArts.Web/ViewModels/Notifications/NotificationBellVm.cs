namespace InstituteOfFineArts.Web.ViewModels.Notifications;

/// <summary>Data of the bell in the menu (F-M12-02): unread count and the newest notifications.</summary>
public class NotificationBellVm
{
    /// <summary>Number of unread notifications (badge).</summary>
    public int UnreadCount { get; set; }

    /// <summary>Newest notifications, read or not, newest first.</summary>
    public IReadOnlyList<NotificationItemVm> Items { get; set; } = Array.Empty<NotificationItemVm>();
}
