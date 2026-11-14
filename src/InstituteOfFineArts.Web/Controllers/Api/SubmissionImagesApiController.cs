using InstituteOfFineArts.Web.Helpers;
using InstituteOfFineArts.Web.Infrastructure.Api;
using InstituteOfFineArts.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InstituteOfFineArts.Web.Controllers.Api;

/// <summary>
/// GET /api/submissions/{id}/image[?download=true] – JPEG of an entry for the mobile app (JWT instead of the web cookie).
/// Same viewing, download and watermark rules as FilesController (BR-20, F-M13-02), decided by IImageAccessService.
/// A missing image and an image the user may not see both return 404.
/// </summary>
[Route("api/submissions")]
[Authorize(Roles = $"{RoleNames.Student},{RoleNames.Staff},{RoleNames.Manager},{RoleNames.Admin}")]
public class SubmissionImagesApiController : ApiControllerBase
{
    private const string JpegContentType = "image/jpeg";

    private readonly IImageAccessService _access;
    private readonly IFileStorageService _storage;
    private readonly IWatermarkService _watermark;

    public SubmissionImagesApiController(IImageAccessService access, IFileStorageService storage, IWatermarkService watermark)
    {
        _access = access;
        _storage = storage;
        _watermark = watermark;
    }

    /// <summary>Image bytes (original, or a watermarked copy when the viewer is not the owner/staff).</summary>
    [HttpGet("{id:long}/image")]
    public async Task<IActionResult> Image(long id, bool download = false)
    {
        var image = await _access.GetSubmissionImageAsync(id, User, thumbnail: false, download);
        var stream = image == null ? null : _storage.OpenRead(image.RelativePath);
        if (image == null || stream == null)
        {
            return Problem(StatusCodes.Status404NotFound, "Image not found.");
        }

        Response.Headers.CacheControl = "private, max-age=300";
        Response.Headers.XContentTypeOptions = "nosniff";

        if (image.Watermark)
        {
            byte[]? watermarked;
            await using (stream)
            {
                watermarked = await _watermark.ApplyAsync(stream);
            }
            if (watermarked == null)
            {
                return Problem(StatusCodes.Status404NotFound, "Image not found.");
            }
            return image.DownloadName == null
                ? File(watermarked, JpegContentType)
                : File(watermarked, JpegContentType, image.DownloadName);
        }
        return image.DownloadName == null
            ? File(stream, JpegContentType)
            : File(stream, JpegContentType, image.DownloadName);
    }
}
