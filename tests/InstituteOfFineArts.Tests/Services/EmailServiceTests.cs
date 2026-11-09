using InstituteOfFineArts.Web.Infrastructure.Email;
using InstituteOfFineArts.Web.Services.Implementations;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MimeKit;
using Moq;

namespace InstituteOfFineArts.Tests.Services;

/// <summary>
/// Unit tests for EmailService (task BE1-OPT): .eml pickup folder without SMTP, and no exception when SMTP fails.
/// </summary>
public class EmailServiceTests : IDisposable
{
    /// <summary>Temporary content root of one test.</summary>
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ifa-email-tests-" + Guid.NewGuid().ToString("N"));

    /// <summary>Creates the service with the given options and the temporary content root.</summary>
    private EmailService CreateService(EmailOptions options)
    {
        var environment = new Mock<IHostEnvironment>();
        environment.Setup(e => e.ContentRootPath).Returns(_root);
        return new EmailService(Options.Create(options), environment.Object, NullLogger<EmailService>.Instance);
    }

    /// <summary>Removes the temporary folder.</summary>
    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    /// <summary>No SMTP host: the message is written as an .eml file with sender, recipient, subject and HTML body.</summary>
    [Fact]
    public async Task Send_NoHost_WritesEmlFile()
    {
        var service = CreateService(new EmailOptions { PickupDirectory = "mail-pickup", FromAddress = "no-reply@ifa.local" });

        var ok = await service.SendAsync("student03@ifa.local", "Le Van Nam", "Hello", "<p>Body text</p>");

        Assert.True(ok);
        var file = Assert.Single(Directory.GetFiles(Path.Combine(_root, "mail-pickup"), "*.eml"));
        var message = await MimeMessage.LoadAsync(file);
        Assert.Equal("Hello", message.Subject);
        Assert.Equal("student03@ifa.local", message.To.Mailboxes.Single().Address);
        Assert.Equal("no-reply@ifa.local", message.From.Mailboxes.Single().Address);
        Assert.Contains("Body text", message.HtmlBody);
    }

    /// <summary>An unreachable SMTP server is logged and reported as false, never thrown to the caller.</summary>
    [Fact]
    public async Task Send_SmtpUnreachable_ReturnsFalse()
    {
        // Port 1 on the local machine refuses the connection immediately
        var service = CreateService(new EmailOptions { Host = "127.0.0.1", Port = 1, UseStartTls = false });

        var ok = await service.SendAsync("student03@ifa.local", "Le Van Nam", "Hello", "<p>Body</p>");

        Assert.False(ok);
        Assert.False(Directory.Exists(Path.Combine(_root, "mail-pickup")));
    }

    /// <summary>An invalid recipient address does not throw either.</summary>
    [Fact]
    public async Task Send_InvalidAddress_ReturnsFalse()
    {
        var service = CreateService(new EmailOptions());

        var ok = await service.SendAsync("not an address", "X", "Hello", "<p>Body</p>");

        Assert.False(ok);
    }
}
