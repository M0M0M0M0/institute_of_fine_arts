using InstituteOfFineArts.Web.Controllers;
using Microsoft.AspNetCore.Http;

namespace InstituteOfFineArts.Tests.Controllers;

/// <summary>
/// Error page texts and detection of oversized uploads (task BE1-03, F-M01-07).
/// </summary>
public class ErrorControllerTests
{
    /// <summary>Each known code has its own title.</summary>
    [Theory]
    [InlineData(403, "Access denied")]
    [InlineData(404, "Page not found")]
    [InlineData(413, "File too large")]
    [InlineData(500, "Something went wrong")]
    [InlineData(503, "Something went wrong")]
    public void Describe_KnownCodes(int code, string title)
    {
        var vm = ErrorController.Describe(code);
        Assert.Equal(code, vm.StatusCode);
        Assert.Equal(title, vm.Title);
        Assert.False(string.IsNullOrWhiteSpace(vm.Message));
    }

    /// <summary>The 413 page tells the user the 5 MB limit.</summary>
    [Fact]
    public void Describe_413_MentionsLimit()
    {
        Assert.Contains("5 MB", ErrorController.Describe(413).Message);
    }

    /// <summary>Kestrel's "request body too large" error is detected.</summary>
    [Fact]
    public void IsRequestTooLarge_KestrelLimit()
    {
        var ex = new BadHttpRequestException("Request body too large.", StatusCodes.Status413PayloadTooLarge);
        Assert.True(ErrorController.IsRequestTooLarge(ex));
    }

    /// <summary>The multipart form limit error is detected, even when wrapped by model binding.</summary>
    [Fact]
    public void IsRequestTooLarge_MultipartLimitWrapped()
    {
        var inner = new InvalidDataException("Multipart body length limit 6291456 exceeded.");
        var ex = new InvalidOperationException("Failed to read the request form.", inner);
        Assert.True(ErrorController.IsRequestTooLarge(ex));
    }

    /// <summary>Other errors are not treated as "too large".</summary>
    [Fact]
    public void IsRequestTooLarge_OtherErrors_False()
    {
        Assert.False(ErrorController.IsRequestTooLarge(new InvalidOperationException("boom")));
        Assert.False(ErrorController.IsRequestTooLarge(new BadHttpRequestException("bad", 400)));
    }
}
