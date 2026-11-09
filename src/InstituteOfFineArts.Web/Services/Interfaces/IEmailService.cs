namespace InstituteOfFineArts.Web.Services.Interfaces;

/// <summary>
/// Sends e-mail (optional features M12 notifications and M16 forgot password).
/// The implementation (BE1-OPT) uses MailKit over SMTP when SMTP is configured, otherwise it writes .eml files
/// to a local folder so the features can be tested without a mail account.
/// Callers never put passwords or file contents in an e-mail, and a failed send must not break the user's action.
/// </summary>
public interface IEmailService
{
    /// <summary>Sends one HTML e-mail.</summary>
    /// <param name="toEmail">Recipient address</param>
    /// <param name="toName">Recipient display name</param>
    /// <param name="subject">Subject line</param>
    /// <param name="htmlBody">HTML body (user text must already be HTML-encoded)</param>
    /// <param name="cancellationToken">Cancellation</param>
    /// <returns>True when the e-mail was handed over (SMTP) or written (pickup folder); false when sending failed (already logged).</returns>
    Task<bool> SendAsync(string toEmail, string toName, string subject, string htmlBody, CancellationToken cancellationToken = default);
}
