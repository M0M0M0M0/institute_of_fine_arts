using InstituteOfFineArts.Web.Infrastructure.Email;
using InstituteOfFineArts.Web.Services.Interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace InstituteOfFineArts.Web.Services.Implementations;

/// <summary>
/// Sends e-mail with MailKit (task BE1-OPT, used by M12 notifications and M16 forgot password).
/// With an SMTP host configured (Email:Host) the message is sent over SMTP; otherwise it is written as an .eml file
/// into the pickup folder (open it with any mail client). Errors are logged and reported as false, never thrown,
/// so a mail problem never breaks the user's action.
/// </summary>
public class EmailService : IEmailService
{
    /// <summary>SMTP connect/send timeout in milliseconds.</summary>
    private const int SmtpTimeoutMilliseconds = 15000;

    /// <summary>E-mail settings (section "Email").</summary>
    private readonly EmailOptions _options;

    /// <summary>Content root used to resolve a relative pickup folder.</summary>
    private readonly IHostEnvironment _environment;

    /// <summary>Logs sent and failed e-mails (recipient and subject only, never the body).</summary>
    private readonly ILogger<EmailService> _logger;

    /// <summary>Receives the required services through Dependency Injection.</summary>
    public EmailService(IOptions<EmailOptions> options, IHostEnvironment environment, ILogger<EmailService> logger)
    {
        _options = options.Value;
        _environment = environment;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<bool> SendAsync(string toEmail, string toName, string subject, string htmlBody,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var message = BuildMessage(toEmail, toName, subject, htmlBody);

            // No SMTP server configured → keep the message as a file (development / demo)
            if (string.IsNullOrWhiteSpace(_options.Host))
            {
                var path = await WriteToPickupAsync(message, cancellationToken);
                _logger.LogInformation("E-mail \"{Subject}\" to {To} written to {Path}", subject, toEmail, path);
                return true;
            }

            await SendSmtpAsync(message, cancellationToken);
            _logger.LogInformation("E-mail \"{Subject}\" sent to {To} via {Host}", subject, toEmail, _options.Host);
            return true;
        }
        catch (Exception ex)
        {
            // Never propagate: the caller's action (e.g. a password reset request) must still succeed
            _logger.LogError(ex, "Sending e-mail \"{Subject}\" to {To} failed", subject, toEmail);
            return false;
        }
    }

    /// <summary>Builds the MIME message with the configured sender and an HTML body.</summary>
    private MimeMessage BuildMessage(string toEmail, string toName, string subject, string htmlBody)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));
        message.To.Add(new MailboxAddress(toName ?? string.Empty, toEmail));
        message.Subject = subject;
        message.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();
        return message;
    }

    /// <summary>Writes the message as a uniquely named .eml file in the pickup folder; returns its path.</summary>
    private async Task<string> WriteToPickupAsync(MimeMessage message, CancellationToken cancellationToken)
    {
        var folder = _options.ResolvePickupDirectory(_environment.ContentRootPath);
        Directory.CreateDirectory(folder);

        // Time prefix keeps the files sorted by creation; the GUID makes the name unique
        var fileName = $"{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}-{Guid.NewGuid():N}.eml";
        var path = Path.Combine(folder, fileName);
        await message.WriteToAsync(path, cancellationToken);
        return path;
    }

    /// <summary>Connects to the SMTP server, authenticates when a user name is configured and sends the message.</summary>
    private async Task SendSmtpAsync(MimeMessage message, CancellationToken cancellationToken)
    {
        using var client = new SmtpClient { Timeout = SmtpTimeoutMilliseconds };
        var security = _options.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto;
        await client.ConnectAsync(_options.Host!,_options.Port, security, cancellationToken);

        // Servers such as Gmail require a login (Gmail: an app password, set via User Secrets)
        if (!string.IsNullOrEmpty(_options.UserName))
        {
            await client.AuthenticateAsync(_options.UserName, _options.Password ?? string.Empty, cancellationToken);
        }

        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }
}
