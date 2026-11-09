namespace InstituteOfFineArts.Web.Models.Entities;

/// <summary>
/// In-app notification shown under the bell icon (F-M12-02), e.g. "Results of City at Night are published".
/// One row per receiving account; the same event may also be e-mailed (F-M12-01).
/// </summary>
public class Notification : BaseEntity
{
    /// <summary>Account that receives the notification.</summary>
    public long AccountId { get; set; }

    /// <summary>Short title, e.g. "New competition".</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Message text.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Relative URL to open when the notification is clicked (optional), e.g. /student/results.</summary>
    public string? Link { get; set; }

    /// <summary>True once the user has opened / marked it as read.</summary>
    public bool IsRead { get; set; }

    /// <summary>Receiving account.</summary>
    public Account Account { get; set; } = null!;
}
