using InstituteOfFineArts.Web.Helpers;
using SkiaSharp;

namespace InstituteOfFineArts.Tests.Helpers;

/// <summary>
/// JPEG upload rules BR-16 / BR-17 (task BE1-04): extension, content type, magic bytes, decodable, size, small-image warning.
/// </summary>
public class JpegValidatorTests
{
    /// <summary>5 MB, the configured limit.</summary>
    private const long Max = 5 * 1024 * 1024;

    /// <summary>A real JPEG of a good size is accepted with its dimensions and no warning.</summary>
    [Fact]
    public async Task ValidJpeg_Accepted()
    {
        var result = await JpegValidator.ValidateAsync(TestImages.AsFormFile(TestImages.Create(800, 600)), Max);
        Assert.True(result.IsValid);
        Assert.Equal(800, result.Width);
        Assert.Equal(600, result.Height);
        Assert.Null(result.Warning);
    }

    /// <summary>.jpeg extension and the old image/pjpeg content type are accepted too.</summary>
    [Fact]
    public async Task JpegExtensionAndPjpeg_Accepted()
    {
        var file = TestImages.AsFormFile(TestImages.Create(700, 700), "scan.JPEG", "image/pjpeg");
        Assert.True((await JpegValidator.ValidateAsync(file, Max)).IsValid);
    }

    /// <summary>No file or an empty file → "Please choose an image file."</summary>
    [Fact]
    public async Task MissingOrEmpty_Rejected()
    {
        Assert.Equal(JpegValidator.MissingMessage, (await JpegValidator.ValidateAsync(null, Max)).Error);
        Assert.Equal(JpegValidator.MissingMessage, (await JpegValidator.ValidateAsync(TestImages.AsFormFile(Array.Empty<byte>()), Max)).Error);
    }

    /// <summary>Wrong extension or content type → only JPEG allowed.</summary>
    [Theory]
    [InlineData("photo.png", "image/jpeg")]
    [InlineData("photo.jpg.exe", "image/jpeg")]
    [InlineData("photo.jpg", "image/png")]
    [InlineData("photo.jpg", "text/html")]
    public async Task WrongExtensionOrType_Rejected(string name, string type)
    {
        var result = await JpegValidator.ValidateAsync(TestImages.AsFormFile(TestImages.Create(800, 600), name, type), Max);
        Assert.False(result.IsValid);
        Assert.Equal(JpegValidator.NotJpegMessage, result.Error);
    }

    /// <summary>A PNG renamed to .jpg fails the magic-byte check.</summary>
    [Fact]
    public async Task PngRenamedToJpg_Rejected()
    {
        var png = TestImages.Create(800, 600, SKEncodedImageFormat.Png);
        var result = await JpegValidator.ValidateAsync(TestImages.AsFormFile(png), Max);
        Assert.Equal(JpegValidator.CorruptMessage, result.Error);
    }

    /// <summary>Correct magic bytes followed by garbage cannot be decoded → rejected.</summary>
    [Fact]
    public async Task FakeHeader_Rejected()
    {
        var bytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }.Concat(System.Text.Encoding.ASCII.GetBytes("<?php echo 1; ?>")).ToArray();
        var result = await JpegValidator.ValidateAsync(TestImages.AsFormFile(bytes), Max);
        Assert.Equal(JpegValidator.CorruptMessage, result.Error);
    }

    /// <summary>A file larger than the limit is rejected with the limit in the message.</summary>
    [Fact]
    public async Task TooLarge_Rejected()
    {
        var bytes = TestImages.Create(800, 600);
        var result = await JpegValidator.ValidateAsync(TestImages.AsFormFile(bytes), bytes.Length - 1);
        Assert.False(result.IsValid);
        Assert.StartsWith("Images must be", result.Error);
        Assert.Equal("Images must be 5 MB or smaller.", JpegValidator.TooLargeMessage(Max));
    }

    /// <summary>Shortest side under 600 px → accepted with a warning (BR-17).</summary>
    [Fact]
    public async Task SmallImage_AcceptedWithWarning()
    {
        var result = await JpegValidator.ValidateAsync(TestImages.AsFormFile(TestImages.Create(1200, 400)), Max);
        Assert.True(result.IsValid);
        Assert.NotNull(result.Warning);
        Assert.Contains("600", result.Warning);
    }
}
