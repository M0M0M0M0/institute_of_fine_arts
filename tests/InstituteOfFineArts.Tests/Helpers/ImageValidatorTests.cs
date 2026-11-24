using InstituteOfFineArts.Web.Helpers;
using SkiaSharp;

namespace InstituteOfFineArts.Tests.Helpers;

/// <summary>
/// Picture upload rules BR-16 / BR-17 (widened 11/2026): JPG, PNG or WebP – extension, content type, magic bytes and
/// decoding must all name the same format; size limit (10 MB); small-image warning.
/// </summary>
public class ImageValidatorTests
{
    /// <summary>10 MB, the configured limit.</summary>
    private const long Max = 10 * 1024 * 1024;

    /// <summary>A real JPEG of a good size is accepted with its dimensions, type and no warning.</summary>
    [Fact]
    public async Task ValidJpeg_Accepted()
    {
        var result = await ImageValidator.ValidateAsync(TestImages.AsFormFile(TestImages.Create(800, 600)), Max);
        Assert.True(result.IsValid);
        Assert.Equal(800, result.Width);
        Assert.Equal(600, result.Height);
        Assert.Null(result.Warning);
        Assert.Equal("image/jpeg", result.ContentType);
        Assert.Equal(".jpg", result.Extension);
    }

    /// <summary>.jpeg extension and the old image/pjpeg content type are accepted too.</summary>
    [Fact]
    public async Task JpegExtensionAndPjpeg_Accepted()
    {
        var file = TestImages.AsFormFile(TestImages.Create(700, 700), "scan.JPEG", "image/pjpeg");
        Assert.True((await ImageValidator.ValidateAsync(file, Max)).IsValid);
    }

    /// <summary>Real PNG and WebP files are accepted and keep their own type and extension.</summary>
    [Theory]
    [InlineData(SKEncodedImageFormat.Png, "art.png", "image/png", ".png")]
    [InlineData(SKEncodedImageFormat.Webp, "art.webp", "image/webp", ".webp")]
    public async Task PngAndWebp_Accepted(SKEncodedImageFormat format, string name, string type, string extension)
    {
        var result = await ImageValidator.ValidateAsync(TestImages.AsFormFile(TestImages.Create(800, 600, format), name, type), Max);
        Assert.True(result.IsValid, result.Error);
        Assert.Equal(type, result.ContentType);
        Assert.Equal(extension, result.Extension);
    }

    /// <summary>No file or an empty file → "Please choose an image file."</summary>
    [Fact]
    public async Task MissingOrEmpty_Rejected()
    {
        Assert.Equal(ImageValidator.MissingMessage, (await ImageValidator.ValidateAsync(null, Max)).Error);
        Assert.Equal(ImageValidator.MissingMessage, (await ImageValidator.ValidateAsync(TestImages.AsFormFile(Array.Empty<byte>()), Max)).Error);
    }

    /// <summary>Unknown extension, or an extension and content type that name different formats → refused.</summary>
    [Theory]
    [InlineData("photo.gif", "image/gif")]
    [InlineData("photo.jpg.exe", "image/jpeg")]
    [InlineData("photo.jpg", "image/png")]
    [InlineData("photo.png", "image/jpeg")]
    [InlineData("photo.jpg", "text/html")]
    public async Task WrongExtensionOrType_Rejected(string name, string type)
    {
        var result = await ImageValidator.ValidateAsync(TestImages.AsFormFile(TestImages.Create(800, 600), name, type), Max);
        Assert.False(result.IsValid);
        Assert.Equal(ImageValidator.NotImageMessage, result.Error);
    }

    /// <summary>A PNG renamed to .jpg (and sent as image/jpeg) fails the magic-byte check.</summary>
    [Fact]
    public async Task PngRenamedToJpg_Rejected()
    {
        var png = TestImages.Create(800, 600, SKEncodedImageFormat.Png);
        var result = await ImageValidator.ValidateAsync(TestImages.AsFormFile(png), Max);
        Assert.Equal(ImageValidator.CorruptMessage, result.Error);
    }

    /// <summary>A JPEG renamed to .webp fails too.</summary>
    [Fact]
    public async Task JpegRenamedToWebp_Rejected()
    {
        var result = await ImageValidator.ValidateAsync(TestImages.AsFormFile(TestImages.Create(800, 600), "a.webp", "image/webp"), Max);
        Assert.Equal(ImageValidator.CorruptMessage, result.Error);
    }

    /// <summary>Correct magic bytes followed by garbage cannot be decoded → rejected.</summary>
    [Fact]
    public async Task FakeHeader_Rejected()
    {
        var bytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }.Concat(System.Text.Encoding.ASCII.GetBytes("<?php echo 1; ?>")).ToArray();
        var result = await ImageValidator.ValidateAsync(TestImages.AsFormFile(bytes), Max);
        Assert.Equal(ImageValidator.CorruptMessage, result.Error);
    }

    /// <summary>A file larger than the limit is rejected with the limit in the message.</summary>
    [Fact]
    public async Task TooLarge_Rejected()
    {
        var bytes = TestImages.Create(800, 600);
        var result = await ImageValidator.ValidateAsync(TestImages.AsFormFile(bytes), bytes.Length - 1);
        Assert.False(result.IsValid);
        Assert.StartsWith("Images must be", result.Error);
        Assert.Equal("Images must be 10 MB or smaller.", ImageValidator.TooLargeMessage(Max));
    }

    /// <summary>Shortest side under 600 px → accepted with a warning (BR-17).</summary>
    [Fact]
    public async Task SmallImage_AcceptedWithWarning()
    {
        var result = await ImageValidator.ValidateAsync(TestImages.AsFormFile(TestImages.Create(1200, 400)), Max);
        Assert.True(result.IsValid);
        Assert.NotNull(result.Warning);
        Assert.Contains("600", result.Warning);
    }

    /// <summary>The stored content type follows the extension.</summary>
    [Theory]
    [InlineData("a/b.png", "image/png")]
    [InlineData("a/b.WEBP", "image/webp")]
    [InlineData("a/b.jpg", "image/jpeg")]
    [InlineData(null, "image/jpeg")]
    public void ContentTypeOf_FollowsExtension(string? path, string expected)
        => Assert.Equal(expected, ImageValidator.ContentTypeOf(path));
}
