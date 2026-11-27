namespace InstituteOfFineArts.Web.Infrastructure.Push;

/// <summary>
/// Settings of the "Push" section: push notifications to the mobile app through Firebase Cloud Messaging (FCM).
/// Push stays off until <see cref="ServiceAccountPath"/> points to the Firebase service account key (a JSON file
/// downloaded from the Firebase console, Project settings → Service accounts). The key is a secret: keep it outside
/// the repository (User Secrets, an environment variable or a path on the server), never in appsettings.json.
/// </summary>
public class PushOptions
{
    /// <summary>Name of the configuration section.</summary>
    public const string SectionName = "Push";

    /// <summary>Path of the Firebase service account JSON key (absolute, or relative to the content root); empty = push off.</summary>
    public string? ServiceAccountPath { get; set; }

    /// <summary>Phones that have not registered their token for this many days get no more pushes and are removed.</summary>
    public int StaleAfterDays { get; set; } = 60;
}
