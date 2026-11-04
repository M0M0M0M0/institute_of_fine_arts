namespace InstituteOfFineArts.Web.Infrastructure.FileStorage;

/// <summary>
/// Settings of the "Upload" section in appsettings.json (images stored with File I/O, BR-16..BR-18).
/// </summary>
public class UploadOptions
{
    /// <summary>Name of the configuration section.</summary>
    public const string SectionName = "Upload";

    /// <summary>Root folder of uploaded files, relative to the content root (or absolute). Kept outside wwwroot.</summary>
    public string RootPath { get; set; } = "uploads";

    /// <summary>Maximum size of one image in bytes (BR-17: 5 MB).</summary>
    public long MaxFileSizeBytes { get; set; } = 5 * 1024 * 1024;

    /// <summary>Width in pixels of the generated thumbnails.</summary>
    public int ThumbnailWidth { get; set; } = 400;

    /// <summary>
    /// Absolute path of the upload root: RootPath combined with the content root when it is relative.
    /// </summary>
    /// <param name="contentRootPath">IWebHostEnvironment.ContentRootPath</param>
    public string ResolveRoot(string contentRootPath)
        => Path.GetFullPath(Path.Combine(contentRootPath, RootPath));
}
