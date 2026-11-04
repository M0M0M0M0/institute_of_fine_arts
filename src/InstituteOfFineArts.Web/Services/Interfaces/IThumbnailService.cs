namespace InstituteOfFineArts.Web.Services.Interfaces;

/// <summary>
/// Creates small JPEG copies of uploaded images for list pages and galleries (SkiaSharp, M13).
/// </summary>
public interface IThumbnailService
{
    /// <summary>
    /// Reads a JPEG from <paramref name="sourcePath"/> and writes a copy at most <paramref name="width"/> px wide
    /// to <paramref name="targetPath"/> (aspect ratio kept, EXIF rotation applied). Smaller images are not enlarged.
    /// </summary>
    /// <param name="sourcePath">Absolute path of the original image</param>
    /// <param name="targetPath">Absolute path of the thumbnail to create</param>
    /// <param name="width">Maximum thumbnail width in pixels</param>
    Task CreateAsync(string sourcePath, string targetPath, int width);
}
