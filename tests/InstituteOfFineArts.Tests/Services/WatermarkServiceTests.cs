using System.Security.Claims;
using InstituteOfFineArts.Web.Controllers;
using InstituteOfFineArts.Web.Services.Implementations;
using InstituteOfFineArts.Web.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SkiaSharp;

namespace InstituteOfFineArts.Tests.Services;

/// <summary>
/// WatermarkService + FilesController (F-M13-02): public paintings are sent to guests and other students with the
/// institute's name drawn on them, in memory; owners, staff, manager and admin get the stored file unchanged.
/// </summary>
public class WatermarkServiceTests
{
    /// <summary>Creates the service with an empty logger.</summary>
    private static WatermarkService CreateService() => new(NullLogger<WatermarkService>.Instance);

    /// <summary>Watermarked output is a valid JPEG of the same size whose pixels differ from the original.</summary>
    [Fact]
    public async Task Apply_ValidJpeg_SameSizeJpegThatDiffers()
    {
        var original = TestImages.Create(400, 300);

        var result = await CreateService().ApplyAsync(new MemoryStream(original));

        Assert.NotNull(result);
        Assert.NotEqual(original, result);
        using var codec = SKCodec.Create(new MemoryStream(result!));
        Assert.NotNull(codec);
        Assert.Equal(SKEncodedImageFormat.Jpeg, codec!.EncodedFormat);
        Assert.Equal(400, codec.Info.Width);
        Assert.Equal(300, codec.Info.Height);

        // The text is visible: a noticeable share of the pixels (more than JPEG noise) changed colour
        using var before = SKBitmap.Decode(original);
        using var after = SKBitmap.Decode(result);
        var changed = 0;
        for (var y = 0; y < 300; y += 2)
        {
            for (var x = 0; x < 400; x += 2)
            {
                if (Distance(before.GetPixel(x, y), after.GetPixel(x, y)) > 30) changed++;
            }
        }
        Assert.True(changed > 200 * 150 / 100, $"Only {changed} sampled pixels changed");
    }

    /// <summary>A portrait image keeps its orientation and size as well.</summary>
    [Fact]
    public async Task Apply_Portrait_SameSize()
    {
        var result = await CreateService().ApplyAsync(new MemoryStream(TestImages.Create(120, 500)));

        using var bitmap = SKBitmap.Decode(result);
        Assert.Equal(120, bitmap.Width);
        Assert.Equal(500, bitmap.Height);
    }

    /// <summary>Bytes that are not an image → null (the controller then answers 404, never the unmarked file).</summary>
    [Fact]
    public async Task Apply_NotAnImage_Null()
    {
        Assert.Null(await CreateService().ApplyAsync(new MemoryStream(new byte[] { 1, 2, 3, 4, 5 })));
    }

    /// <summary>FilesController: a watermarked image is sent as new JPEG bytes, the stored file is unchanged.</summary>
    [Fact]
    public async Task Controller_Watermark_SendsNewBytes_FileUnchanged()
    {
        var original = TestImages.Create(300, 200);
        var path = WriteTemp(original);
        try
        {
            var controller = CreateController(path, new ImageFile("x.jpg", null, IsPublic: true, Watermark: true));

            var result = Assert.IsType<FileContentResult>(await controller.Submission(1));

            Assert.Equal("image/jpeg", result.ContentType);
            Assert.NotEqual(original, result.FileContents);
            Assert.Equal(original, await File.ReadAllBytesAsync(path));
            // Same URL, different bytes per viewer → never in a shared cache, and keyed on the login cookie
            Assert.Equal("private, max-age=3600", controller.Response.Headers.CacheControl.ToString());
            Assert.Equal("Cookie", controller.Response.Headers.Vary.ToString());
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>FilesController: owner / staff path (no watermark) streams the stored file itself.</summary>
    [Fact]
    public async Task Controller_Owner_StreamsOriginal()
    {
        var original = TestImages.Create(300, 200);
        var path = WriteTemp(original);
        try
        {
            var controller = CreateController(path, new ImageFile("x.jpg", null, IsPublic: false, Watermark: false));

            var result = Assert.IsType<FileStreamResult>(await controller.Submission(1));

            await using var stream = result.FileStream;
            using var copy = new MemoryStream();
            await stream.CopyToAsync(copy);
            Assert.Equal(original, copy.ToArray());
            Assert.Equal("private, max-age=300", controller.Response.Headers.CacheControl.ToString());
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>FilesController: a watermarked download keeps its "Save as" name.</summary>
    [Fact]
    public async Task Controller_WatermarkedDownload_KeepsName()
    {
        var path = WriteTemp(TestImages.Create(100, 100));
        try
        {
            var controller = CreateController(path, new ImageFile("x.jpg", "Owl.jpg", IsPublic: true, Watermark: true));

            var result = Assert.IsType<FileContentResult>(await controller.Submission(1, download: true));

            Assert.Equal("Owl.jpg", result.FileDownloadName);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>Controller with mocked access / storage (returning <paramref name="path"/>) and the real watermark service.</summary>
    private static FilesController CreateController(string path, ImageFile image)
    {
        var access = new Mock<IImageAccessService>();
        access.Setup(a => a.GetSubmissionImageAsync(It.IsAny<long>(), It.IsAny<ClaimsPrincipal>(), It.IsAny<bool>(), It.IsAny<bool>()))
            .ReturnsAsync(image);
        var storage = new Mock<IFileStorageService>();
        storage.Setup(s => s.OpenRead(It.IsAny<string?>())).Returns(() => File.OpenRead(path));
        return new FilesController(access.Object, storage.Object, CreateService())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };
    }

    /// <summary>Writes bytes to a new temporary file and returns its path.</summary>
    private static string WriteTemp(byte[] bytes)
    {
        var path = Path.Combine(Path.GetTempPath(), $"ifa-wm-{Guid.NewGuid():N}.jpg");
        File.WriteAllBytes(path, bytes);
        return path;
    }

    /// <summary>Sum of the absolute RGB differences of two colours.</summary>
    private static int Distance(SKColor a, SKColor b)
        => Math.Abs(a.Red - b.Red) + Math.Abs(a.Green - b.Green) + Math.Abs(a.Blue - b.Blue);
}
