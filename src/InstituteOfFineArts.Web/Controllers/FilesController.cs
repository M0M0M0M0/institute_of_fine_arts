using InstituteOfFineArts.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InstituteOfFineArts.Web.Controllers;

/// <summary>
/// Streams stored images (task BE1-04). Images live outside wwwroot, so every request passes here and
/// the viewing rules (BR-20) are checked first:
/// GET /files/submissions/{id}               – image shown in a page
/// GET /files/submissions/{id}?download=true – original file with its name (staff, manager, owner)
/// GET /files/submissions/{id}/thumb         – small version for lists and galleries
/// GET /files/competitions/{id}/cover        – competition cover (public)
/// A missing image and an image the user may not see both return 404, so ids of private entries are not revealed.
/// Viewers other than the owner, STAFF, MANAGER and ADMIN receive a copy watermarked in memory (F-M13-02);
/// the stored file never changes.
/// </summary>
[AllowAnonymous]
[Route("files")]
public class FilesController : Controller
{
    /// <summary>Content type of every stored image.</summary>
    private const string JpegContentType = "image/jpeg";

    /// <summary>Finds images and checks permissions.</summary>
    private readonly IImageAccessService _access;

    /// <summary>Opens stored files.</summary>
    private readonly IFileStorageService _storage;

    /// <summary>Draws the institute watermark on public images (F-M13-02).</summary>
    private readonly IWatermarkService _watermark;

    /// <summary>Receives the required services through Dependency Injection.</summary>
    public FilesController(IImageAccessService access, IFileStorageService storage, IWatermarkService watermark)
    {
        _access = access;
        _storage = storage;
        _watermark = watermark;
    }

    /// <summary>Image of a submission (or the original as a download).</summary>
    /// <param name="id">Submission id</param>
    /// <param name="download">True to download the original file</param>
    [HttpGet("submissions/{id:long}")]
    public async Task<IActionResult> Submission(long id, bool download = false)
        => await ServeAsync(await _access.GetSubmissionImageAsync(id, User, thumbnail: false, download));

    /// <summary>Thumbnail of a submission.</summary>
    /// <param name="id">Submission id</param>
    [HttpGet("submissions/{id:long}/thumb")]
    public async Task<IActionResult> Thumbnail(long id)
        => await ServeAsync(await _access.GetSubmissionImageAsync(id, User, thumbnail: true, download: false));

    /// <summary>Cover image of a competition.</summary>
    /// <param name="id">Competition id</param>
    [HttpGet("competitions/{id:long}/cover")]
    public async Task<IActionResult> Cover(long id)
        => await ServeAsync(await _access.GetCompetitionCoverAsync(id));

    /// <summary>
    /// Streams the file with a FileStream (or a watermarked copy from memory), or 404 when not allowed / not on disk.
    /// </summary>
    private async Task<IActionResult> ServeAsync(ImageFile? image)
    {
        var stream = image == null ? null : _storage.OpenRead(image.RelativePath);
        if (image == null || stream == null)
        {
            return NotFound();
        }

        // Public images may be cached by any cache; private ones only by this user's browser, briefly
        Response.Headers.CacheControl = image.IsPublic ? "public, max-age=3600" : "private, max-age=300";
        Response.Headers.XContentTypeOptions = "nosniff";

        // Watermarked copy (F-M13-02): built in memory; an undecodable file is never sent without the watermark
        if (image.Watermark)
        {
            byte[]? watermarked;
            await using (stream)
            {
                watermarked = await _watermark.ApplyAsync(stream);
            }
            if (watermarked == null)
            {
                return NotFound();
            }
            return image.DownloadName == null
                ? File(watermarked, JpegContentType)
                : File(watermarked, JpegContentType, image.DownloadName);
        }

        // File(...) disposes the stream after sending. With a download name the browser shows "Save as".
        return image.DownloadName == null
            ? File(stream, JpegContentType)
            : File(stream, JpegContentType, image.DownloadName);
    }
}
