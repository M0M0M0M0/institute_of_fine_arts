namespace InstituteOfFineArts.Web.ViewModels.Notifications;

/// <summary>One notification in the list page and in the bell (F-M12-02).</summary>
public class NotificationItemVm
{
    /// <summary>notification.id</summary>
    public long Id { get; set; }

    /// <summary>Short title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Message text.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Relative URL opened from the notification, or null.</summary>
    public string? Link { get; set; }

    /// <summary>True once opened / marked as read.</summary>
    public bool IsRead { get; set; }

    /// <summary>When the notification was created.</summary>
    public DateTime CreatedAt { get; set; }
}
