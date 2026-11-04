using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Helpers;
using InstituteOfFineArts.Web.Infrastructure.FileStorage;
using InstituteOfFineArts.Web.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace InstituteOfFineArts.Web.Services.Implementations;

/// <summary>
/// File I/O storage of images below the upload root (default: src/InstituteOfFineArts.Web/uploads).
/// </summary>
public class FileStorageService : IFileStorageService
{
    /// <summary>Longest original file name kept (column original_file_name is 255 characters).</summary>
    private const int MaxOriginalNameLength = 255;

    /// <summary>Absolute path of the upload root, always ending without a separator.</summary>
    private readonly string _root;

    /// <summary>Upload settings (size limit, thumbnail width).</summary>
    private readonly UploadOptions _options;

    /// <summary>Creates thumbnails.</summary>
    private readonly IThumbnailService _thumbnails;

    /// <summary>Logs stored / deleted files and storage problems.</summary>
    private readonly ILogger<FileStorageService> _logger;

    /// <summary>Receives the required services through Dependency Injection and makes sure the root folder exists.</summary>
    public FileStorageService(IOptions<UploadOptions> options, IWebHostEnvironment environment,
        IThumbnailService thumbnails, ILogger<FileStorageService> logger)
    {
        _options = options.Value;
        _root = Path.TrimEndingDirectorySeparator(_options.ResolveRoot(environment.ContentRootPath));
        _thumbnails = thumbnails;
        _logger = logger;
        Directory.CreateDirectory(_root);
    }

    /// <inheritdoc />
    public async Task<StoredImage> SaveSubmissionImageAsync(IFormFile file, long competitionId, string field = "Image")
    {
        // 1. Reject anything that is not a JPEG within the limit (BR-16, BR-17)
        var check = await ValidateAsync(file, field);

        // 2. New random name – never the user's file name (BR-18, prevents path traversal and clashes)
        var fileName = $"{Guid.NewGuid()}.jpg";
        var relative = $"submissions/{competitionId}/{fileName}";
        var fullPath = await WriteAsync(file, relative);

        // 3. Thumbnail for list pages; a failure here must not lose the upload
        string? thumbRelative = $"thumbnails/{competitionId}/{fileName}";
        try
        {
            await _thumbnails.CreateAsync(fullPath, ResolvePath(thumbRelative), _options.ThumbnailWidth);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Thumbnail could not be created for {Path}", relative);
            thumbRelative = null;
        }

        _logger.LogInformation("Stored submission image {Path} ({Size} bytes)", relative, file.Length);
        return new StoredImage(relative, thumbRelative, CleanFileName(file.FileName), file.Length,
            check.Width, check.Height, check.Warning);
    }

    /// <inheritdoc />
    public async Task<string> SaveCompetitionCoverAsync(IFormFile file, long competitionId, string field = "CoverImage")
    {
        await ValidateAsync(file, field);
        var relative = $"competitions/{competitionId}/cover-{Guid.NewGuid()}.jpg";
        await WriteAsync(file, relative);
        _logger.LogInformation("Stored competition cover {Path}", relative);
        return relative;
    }

    /// <inheritdoc />
    public FileStream? OpenRead(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return null;
        }

        var fullPath = ResolvePath(relativePath);
        if (!File.Exists(fullPath))
        {
            _logger.LogWarning("Stored file is missing: {Path}", relativePath);
            return null;
        }

        // Shared read so several requests can stream the same image at once
        return new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, useAsync: true);
    }

    /// <inheritdoc />
    public void Delete(params string?[] relativePaths)
    {
        foreach (var relative in relativePaths.Where(p => !string.IsNullOrWhiteSpace(p)))
        {
            try
            {
                var fullPath = ResolvePath(relative!);
                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                    _logger.LogInformation("Deleted stored file {Path}", relative);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // A file that cannot be deleted now only wastes disk space; the user's action still succeeds
                _logger.LogWarning(ex, "Stored file could not be deleted: {Path}", relative);
            }
        }
    }

    /// <inheritdoc />
    public string ResolvePath(string relativePath)
    {
        // Paths come from the database, but are still checked so a bad value can never leave the root
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath))
        {
            throw new InvalidOperationException("Invalid file path.");
        }

        var fullPath = Path.GetFullPath(Path.Combine(_root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!fullPath.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Invalid file path.");
        }
        return fullPath;
    }

    /// <summary>Runs the JPEG checks and turns a failure into a BusinessException on the given field.</summary>
    private async Task<JpegCheckResult> ValidateAsync(IFormFile file, string field)
    {
        var check = await JpegValidator.ValidateAsync(file, _options.MaxFileSizeBytes);
        if (!check.IsValid)
        {
            throw new BusinessException(check.Error!, field);
        }
        return check;
    }

    /// <summary>
    /// Writes the upload to a temporary file first, then moves it into place, so a half-written file
    /// never appears under its final name. Returns the absolute path.
    /// </summary>
    private async Task<string> WriteAsync(IFormFile file, string relativePath)
    {
        var fullPath = ResolvePath(relativePath);
        var tempPath = fullPath + ".tmp";
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        try
        {
            await using (var output = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write))
            {
                await file.CopyToAsync(output);
            }
            File.Move(tempPath, fullPath);
            return fullPath;
        }
        catch
        {
            // Do not leave a partial file behind
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
            throw;
        }
    }

    /// <summary>File name without any folder part or control characters, at most 255 characters.</summary>
    private static string CleanFileName(string? name)
    {
        // Browsers may send "C:\fakepath\photo.jpg" or "../../photo.jpg": keep only the last part
        var lastPart = (name ?? string.Empty).Replace('\\', '/').Split('/').Last();
        var clean = new string(lastPart.Where(c => !char.IsControl(c)).ToArray()).Trim();
        if (clean.Length == 0)
        {
            clean = "image.jpg";
        }
        return clean.Length <= MaxOriginalNameLength ? clean : clean[^MaxOriginalNameLength..];
    }
}
