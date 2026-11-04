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

    /// <summary>Receives the required services through Dependency Injection.</summary>
    public FilesController(IImageAccessService access, IFileStorageService storage)
    {
        _access = access;
        _storage = storage;
    }

    /// <summary>Image of a submission (or the original as a download).</summary>
    /// <param name="id">Submission id</param>
    /// <param name="download">True to download the original file</param>
    [HttpGet("submissions/{id:long}")]
    public async Task<IActionResult> Submission(long id, bool download = false)
        => Serve(await _access.GetSubmissionImageAsync(id, User, thumbnail: false, download));

    /// <summary>Thumbnail of a submission.</summary>
    /// <param name="id">Submission id</param>
    [HttpGet("submissions/{id:long}/thumb")]
    public async Task<IActionResult> Thumbnail(long id)
        => Serve(await _access.GetSubmissionImageAsync(id, User, thumbnail: true, download: false));

    /// <summary>Cover image of a competition.</summary>
    /// <param name="id">Competition id</param>
    [HttpGet("competitions/{id:long}/cover")]
    public async Task<IActionResult> Cover(long id)
        => Serve(await _access.GetCompetitionCoverAsync(id));

    /// <summary>Streams the file with a FileStream, or 404 when not allowed / not on disk.</summary>
    private IActionResult Serve(ImageFile? image)
    {
        var stream = image == null ? null : _storage.OpenRead(image.RelativePath);
        if (image == null || stream == null)
        {
            return NotFound();
        }

        // Public images may be cached by any cache; private ones only by this user's browser, briefly
        Response.Headers.CacheControl = image.IsPublic ? "public, max-age=3600" : "private, max-age=300";
        Response.Headers.XContentTypeOptions = "nosniff";

        // File(...) disposes the stream after sending. With a download name the browser shows "Save as".
        return image.DownloadName == null
            ? File(stream, JpegContentType)
            : File(stream, JpegContentType, image.DownloadName);
    }
}
