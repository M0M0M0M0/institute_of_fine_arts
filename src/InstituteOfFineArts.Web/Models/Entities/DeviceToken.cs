namespace InstituteOfFineArts.Web.Models.Entities;

/// <summary>
/// A phone of the mobile app that receives push notifications (Firebase Cloud Messaging): the app registers its FCM
/// registration token after signing in and removes it when signing out. One account can have several phones; a
/// token belongs to one account at a time (signing in with another account on the same phone moves it).
/// </summary>
public class DeviceToken : BaseEntity
{
    /// <summary>Account signed in on the phone.</summary>
    public long AccountId { get; set; }

    /// <summary>FCM registration token of the app on the phone.</summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>"android" or "ios".</summary>
    public string Platform { get; set; } = string.Empty;

    /// <summary>Last time the app registered the token (it does so on every start), to drop phones gone quiet.</summary>
    public DateTime LastSeenAt { get; set; }

    /// <summary>The account.</summary>
    public Account Account { get; set; } = null!;
}
