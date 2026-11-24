using InstituteOfFineArts.Web.Infrastructure.Audit;
using InstituteOfFineArts.Web.Helpers;
using InstituteOfFineArts.Web.Infrastructure.Api;
using InstituteOfFineArts.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InstituteOfFineArts.Web.Controllers.Api;

/// <summary>
/// Pictures of an entry for the mobile app (JWT instead of the web cookie):
/// GET /api/submissions/{id}/image[?download=true|?thumb=true] – the cover picture;
/// GET /api/submission-images/{imageId}[?download=true|?thumb=true] – one picture of the entry (BR-18).
/// Same viewing, download and watermark rules as FilesController (BR-20, F-M13-02), decided by IImageAccessService.
/// A missing image and an image the user may not see both return 404.
/// </summary>
[Route("api/submissions")]
[Authorize(Roles = $"{RoleNames.Student},{RoleNames.Staff},{RoleNames.Teacher},{RoleNames.Admin}")]
public class SubmissionImagesApiController : ApiControllerBase
{
    /// <summary>Content type of thumbnails, watermarked copies and older entries.</summary>
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

    /// <summary>Cover picture bytes (original, or a watermarked copy when the viewer is not the owner/staff).</summary>
    [HttpGet("{id:long}/image")]
    [AuditView]
    public async Task<IActionResult> Image(long id, bool download = false, bool thumb = false)
        // thumb=true: the 400 px copy (the original when there is none); same permission rules
        => await ServeAsync(await _access.GetSubmissionImageAsync(id, User, thumb && !download, download));

    /// <summary>One picture of an entry.</summary>
    /// <param name="imageId">submission_image id</param>
    /// <param name="download">True to download the original</param>
    /// <param name="thumb">True for the 400 px copy</param>
    [HttpGet("~/api/submission-images/{imageId:long}")]
    [AuditView]
    public async Task<IActionResult> Picture(long imageId, bool download = false, bool thumb = false)
        => await ServeAsync(await _access.GetPictureAsync(imageId, User, thumb && !download, download));

    /// <summary>Streams the file (or a watermarked copy), or 404.</summary>
    private async Task<IActionResult> ServeAsync(ImageFile? image)
    {
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
        var type = image.ContentType ?? JpegContentType;
        return image.DownloadName == null
            ? File(stream, type)
            : File(stream, type, image.DownloadName);
    }
}
