using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Helpers;
using InstituteOfFineArts.Web.Infrastructure.FileStorage;
using InstituteOfFineArts.Web.Services.Implementations;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using SkiaSharp;

namespace InstituteOfFineArts.Tests.Services;

/// <summary>
/// FileStorageService + ThumbnailService on a temporary folder (task BE1-04, BR-16..BR-18).
/// </summary>
public sealed class FileStorageServiceTests : IDisposable
{
    /// <summary>Temporary content root, deleted after each test.</summary>
    private readonly string _contentRoot = Path.Combine(Path.GetTempPath(), "ifa-tests-" + Guid.NewGuid().ToString("N"));

    /// <summary>Service under test, storing below {contentRoot}/uploads.</summary>
    private readonly FileStorageService _service;

    /// <summary>Creates the service with real thumbnails and a fake hosting environment.</summary>
    public FileStorageServiceTests()
    {
        var env = new Mock<IWebHostEnvironment>();
        env.SetupGet(e => e.ContentRootPath).Returns(_contentRoot);
        var options = Options.Create(new UploadOptions { RootPath = "uploads", MaxFileSizeBytes = 5 * 1024 * 1024, ThumbnailWidth = 400 });
        _service = new FileStorageService(options, env.Object, new ThumbnailService(), NullLogger<FileStorageService>.Instance);
    }

    /// <summary>Removes the temporary folder.</summary>
    public void Dispose()
    {
        if (Directory.Exists(_contentRoot))
        {
            Directory.Delete(_contentRoot, recursive: true);
        }
    }

    /// <summary>Root folder is created when the service starts.</summary>
    [Fact]
    public void Constructor_CreatesRoot()
    {
        Assert.True(Directory.Exists(Path.Combine(_contentRoot, "uploads")));
    }

    /// <summary>A valid image is written under submissions/{competitionId}/{guid}.jpg with a 400 px thumbnail.</summary>
    [Fact]
    public async Task SaveSubmission_WritesFileAndThumbnail()
    {
        var bytes = TestImages.Create(1600, 1200);
        var stored = await _service.SaveSubmissionImageAsync(TestImages.AsFormFile(bytes, "Sunset.jpg"), 12);

        Assert.Matches(@"^submissions/12/[0-9a-f\-]{36}\.jpg$", stored.RelativePath);
        Assert.Equal("Sunset.jpg", stored.OriginalFileName);
        Assert.Equal(bytes.Length, stored.FileSize);
        Assert.Equal(1600, stored.Width);
        Assert.Equal(bytes, await File.ReadAllBytesAsync(_service.ResolvePath(stored.RelativePath)));

        // Thumbnail: same file name in thumbnails/12/, 400 px wide, ratio kept
        Assert.Equal(stored.RelativePath.Replace("submissions/", "thumbnails/"), stored.ThumbnailPath);
        using var codec = SKCodec.Create(_service.ResolvePath(stored.ThumbnailPath!));
        Assert.Equal(400, codec.Info.Width);
        Assert.Equal(300, codec.Info.Height);

        // No temporary file left behind
        Assert.Empty(Directory.GetFiles(Path.Combine(_contentRoot, "uploads"), "*.tmp", SearchOption.AllDirectories));
    }

    /// <summary>A small image is not enlarged in its thumbnail.</summary>
    [Fact]
    public async Task SaveSubmission_SmallImage_ThumbnailNotEnlarged()
    {
        var stored = await _service.SaveSubmissionImageAsync(TestImages.AsFormFile(TestImages.Create(300, 200)), 1);
        using var codec = SKCodec.Create(_service.ResolvePath(stored.ThumbnailPath!));
        Assert.Equal(300, codec.Info.Width);
        Assert.NotNull(stored.Warning);
    }

    /// <summary>Two uploads of the same file get different names (no overwrite).</summary>
    [Fact]
    public async Task SaveSubmission_SameFileTwice_DifferentPaths()
    {
        var bytes = TestImages.Create(800, 600);
        var a = await _service.SaveSubmissionImageAsync(TestImages.AsFormFile(bytes), 3);
        var b = await _service.SaveSubmissionImageAsync(TestImages.AsFormFile(bytes), 3);
        Assert.NotEqual(a.RelativePath, b.RelativePath);
    }

    /// <summary>User file names with folders are reduced to the last part; they never affect the stored path.</summary>
    [Theory]
    [InlineData(@"..\..\appsettings.jpg", "appsettings.jpg")]
    [InlineData("../../etc/passwd.jpg", "passwd.jpg")]
    [InlineData(@"C:\fakepath\my photo.jpg", "my photo.jpg")]
    public async Task SaveSubmission_PathInFileName_Ignored(string name, string expected)
    {
        var stored = await _service.SaveSubmissionImageAsync(TestImages.AsFormFile(TestImages.Create(800, 600), name), 5);
        Assert.Equal(expected, stored.OriginalFileName);
        Assert.StartsWith("submissions/5/", stored.RelativePath);
    }

    /// <summary>An invalid file throws BusinessException on the given field and writes nothing.</summary>
    [Fact]
    public async Task SaveSubmission_Invalid_ThrowsAndWritesNothing()
    {
        var png = TestImages.Create(800, 600, SKEncodedImageFormat.Png);
        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => _service.SaveSubmissionImageAsync(TestImages.AsFormFile(png), 7, "NewImage"));
        Assert.Equal(JpegValidator.CorruptMessage, ex.Message);
        Assert.Equal("NewImage", ex.Field);
        Assert.False(Directory.Exists(Path.Combine(_contentRoot, "uploads", "submissions", "7")));
    }

    /// <summary>Cover images go to competitions/{id}/cover-{guid}.jpg.</summary>
    [Fact]
    public async Task SaveCover_WritesUnderCompetitions()
    {
        var path = await _service.SaveCompetitionCoverAsync(TestImages.AsFormFile(TestImages.Create(1200, 600)), 4);
        Assert.Matches(@"^competitions/4/cover-[0-9a-f\-]{36}\.jpg$", path);
        Assert.True(File.Exists(_service.ResolvePath(path)));
    }

    /// <summary>OpenRead returns the file, or null when it is missing or the path is empty.</summary>
    [Fact]
    public async Task OpenRead_ExistingAndMissing()
    {
        var stored = await _service.SaveSubmissionImageAsync(TestImages.AsFormFile(TestImages.Create(800, 600)), 2);
        using (var stream = _service.OpenRead(stored.RelativePath))
        {
            Assert.NotNull(stream);
            Assert.Equal(stored.FileSize, stream!.Length);
        }
        Assert.Null(_service.OpenRead("submissions/2/missing.jpg"));
        Assert.Null(_service.OpenRead(null));
        Assert.Null(_service.OpenRead(""));
    }

    /// <summary>Delete removes original and thumbnail; missing and empty paths are ignored.</summary>
    [Fact]
    public async Task Delete_RemovesFiles()
    {
        var stored = await _service.SaveSubmissionImageAsync(TestImages.AsFormFile(TestImages.Create(800, 600)), 9);
        _service.Delete(stored.RelativePath, stored.ThumbnailPath, null, "submissions/9/missing.jpg");
        Assert.False(File.Exists(_service.ResolvePath(stored.RelativePath)));
        Assert.False(File.Exists(_service.ResolvePath(stored.ThumbnailPath!)));
    }

    /// <summary>Paths that would leave the upload root are refused (path traversal).</summary>
    [Theory]
    [InlineData("../appsettings.json")]
    [InlineData("submissions/../../Program.cs")]
    [InlineData(@"..\secret.txt")]
    [InlineData(@"C:\Windows\win.ini")]
    [InlineData("/etc/passwd")]
    [InlineData("")]
    public void ResolvePath_OutsideRoot_Throws(string path)
    {
        Assert.Throws<InvalidOperationException>(() => _service.ResolvePath(path));
    }
}
