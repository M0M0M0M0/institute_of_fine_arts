namespace InstituteOfFineArts.Web.Services.Interfaces;

/// <summary>
/// A stored submission image.
/// </summary>
/// <param name="RelativePath">Path below the upload root, saved in submission.image_path (e.g. submissions/12/{guid}.jpg)</param>
/// <param name="ThumbnailPath">Path of the thumbnail, or null when it could not be created</param>
/// <param name="OriginalFileName">File name chosen by the user, cleaned – for display and download only</param>
/// <param name="FileSize">Size in bytes</param>
/// <param name="Width">Image width in pixels</param>
/// <param name="Height">Image height in pixels</param>
/// <param name="Warning">Non-blocking advice for the user (small image), or null</param>
public record StoredImage(string RelativePath, string? ThumbnailPath, string OriginalFileName, long FileSize,
    int Width, int Height, string? Warning);

/// <summary>
/// Stores uploaded images on disk with File I/O, outside wwwroot (BR-16..BR-18, docs/plan/02 section 4.3).
/// File names are always new Guids; user file names are never used in paths.
/// </summary>
public interface IFileStorageService
{
    /// <summary>
    /// Validates and saves a submission image to submissions/{competitionId}/{guid}.jpg and creates its thumbnail.
    /// Throws BusinessException (on <paramref name="field"/>) when the file is not an acceptable JPEG.
    /// </summary>
    /// <param name="file">Uploaded file</param>
    /// <param name="competitionId">Competition id, used as sub folder</param>
    /// <param name="field">Form field name for the error message</param>
    Task<StoredImage> SaveSubmissionImageAsync(IFormFile file, long competitionId, string field = "Image");

    /// <summary>
    /// Validates and saves a competition cover image to competitions/{competitionId}/cover-{guid}.jpg.
    /// Returns the relative path. Throws BusinessException when the file is not an acceptable JPEG.
    /// </summary>
    /// <param name="file">Uploaded file</param>
    /// <param name="competitionId">Competition id</param>
    /// <param name="field">Form field name for the error message</param>
    Task<string> SaveCompetitionCoverAsync(IFormFile file, long competitionId, string field = "CoverImage");

    /// <summary>Opens a stored file for reading, or returns null when the path is empty or the file is missing.</summary>
    /// <param name="relativePath">Path below the upload root (from the database)</param>
    FileStream? OpenRead(string? relativePath);

    /// <summary>Deletes stored files; empty paths and missing files are ignored.</summary>
    /// <param name="relativePaths">Paths below the upload root</param>
    void Delete(params string?[] relativePaths);

    /// <summary>
    /// Absolute path for a relative path. Throws InvalidOperationException when the result would be
    /// outside the upload root (path traversal such as "../appsettings.json").
    /// </summary>
    /// <param name="relativePath">Path below the upload root</param>
    string ResolvePath(string relativePath);
}
