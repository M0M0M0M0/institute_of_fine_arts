namespace InstituteOfFineArts.Web.Infrastructure.Email;

/// <summary>
/// Settings of the "Email" section (optional features M12 notifications and M16 forgot password, task BE1-OPT).
/// When <see cref="Host"/> is empty, e-mails are written as .eml files into <see cref="PickupDirectory"/>
/// instead of being sent, so the features work on a developer machine without a mail account.
/// The SMTP password is never stored in appsettings: set it with User Secrets or an environment variable.
/// </summary>
public class EmailOptions
{
    /// <summary>Name of the configuration section.</summary>
    public const string SectionName = "Email";

    /// <summary>SMTP server (e.g. smtp.gmail.com); empty = write .eml files to the pickup folder.</summary>
    public string? Host { get; set; }

    /// <summary>SMTP port (587 for STARTTLS).</summary>
    public int Port { get; set; } = 587;

    /// <summary>true = upgrade the connection with STARTTLS; false = let MailKit choose (implicit SSL on port 465).</summary>
    public bool UseStartTls { get; set; } = true;

    /// <summary>SMTP login; empty = send without authentication.</summary>
    public string? UserName { get; set; }

    /// <summary>SMTP password or app password (User Secrets / environment only, never in git).</summary>
    public string? Password { get; set; }

    /// <summary>Sender address shown to the recipient.</summary>
    public string FromAddress { get; set; } = "no-reply@ifa.local";

    /// <summary>Sender display name.</summary>
    public string FromName { get; set; } = "Institute of Fine Arts";

    /// <summary>Folder for .eml files when no SMTP host is set, relative to the content root (or absolute). Kept outside wwwroot.</summary>
    public string PickupDirectory { get; set; } = "mail-pickup";

    /// <summary>Public address of the site used to build links in e-mails (e.g. https://ifa.example.com), without a trailing slash.</summary>
    public string? AppBaseUrl { get; set; }

    /// <summary>
    /// Absolute path of the pickup folder: PickupDirectory combined with the content root when it is relative.
    /// </summary>
    /// <param name="contentRootPath">IHostEnvironment.ContentRootPath</param>
    public string ResolvePickupDirectory(string contentRootPath)
        => Path.GetFullPath(Path.Combine(contentRootPath, PickupDirectory));
}
