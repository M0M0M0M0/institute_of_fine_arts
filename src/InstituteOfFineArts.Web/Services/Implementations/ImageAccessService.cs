using System.Security.Claims;
using InstituteOfFineArts.Web.Data;
using InstituteOfFineArts.Web.Helpers;
using InstituteOfFineArts.Web.Infrastructure.FileStorage;
using InstituteOfFineArts.Web.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace InstituteOfFineArts.Web.Services.Implementations;

/// <summary>
/// Looks up image paths in the database, checks who may see them (BR-20) and who gets a watermarked copy (F-M13-02).
/// </summary>
public class ImageAccessService : IImageAccessService
{
    /// <summary>Database access.</summary>
    private readonly AppDbContext _db;

    /// <summary>Whether public images get the watermark (Upload:PublicWatermark, off by default).</summary>
    private readonly bool _publicWatermark;

    /// <summary>Receives the database context and the upload settings through Dependency Injection.</summary>
    public ImageAccessService(AppDbContext db, IOptions<UploadOptions>? options = null)
    {
        _db = db;
        _publicWatermark = options?.Value.PublicWatermark ?? false;
    }

    /// <inheritdoc />
    public async Task<ImageFile?> GetSubmissionImageAsync(long submissionId, ClaimsPrincipal user, bool thumbnail, bool download)
    {
        var submission = await LoadForRuleAsync(submissionId);
        return submission == null
            ? null
            : Resolve(submission, user, thumbnail, download, submission.ImagePath, submission.ThumbnailPath, submission.OriginalFileName);
    }

    /// <inheritdoc />
    public async Task<ImageFile?> GetPictureAsync(long imageId, ClaimsPrincipal user, bool thumbnail, bool download)
    {
        var picture = await _db.SubmissionImages.AsNoTracking().FirstOrDefaultAsync(i => i.Id == imageId);
        if (picture == null)
        {
            return null;
        }
        var submission = await LoadForRuleAsync(picture.SubmissionId);
        return submission == null
            ? null
            : Resolve(submission, user, thumbnail, download, picture.ImagePath, picture.ThumbnailPath, picture.OriginalFileName);
    }

    /// <summary>The submission with what the rule needs: competition status and jury, rating, award.</summary>
    private Task<Models.Entities.Submission?> LoadForRuleAsync(long submissionId)
        => _db.Submissions
            .AsNoTracking()
            .Include(s => s.Competition).ThenInclude(c => c.Judges)
            .Include(s => s.Evaluation)
            .Include(s => s.AwardIssued)
            .FirstOrDefaultAsync(s => s.Id == submissionId);

    /// <summary>Applies BR-20 / F-M13-02 to one stored picture of <paramref name="submission"/>.</summary>
    private ImageFile? Resolve(Models.Entities.Submission submission, ClaimsPrincipal user, bool thumbnail, bool download,
        string imagePath, string? thumbnailPath, string originalFileName)
    {
        // Viewing and downloading have different rules
        var allowed = download
            ? SubmissionImagePolicy.CanDownload(submission, user)
            : SubmissionImagePolicy.CanView(submission, user);
        if (!allowed)
        {
            return null;
        }

        var isPublic = SubmissionImagePolicy.IsPublic(submission);
        var watermark = _publicWatermark && NeedsWatermark(submission, user);
        if (download)
        {
            // A watermarked copy is always a JPEG; the original keeps its own format
            var type = watermark ? "image/jpeg" : ImageValidator.ContentTypeOf(imagePath);
            return new ImageFile(imagePath, DownloadName(originalFileName, type), isPublic, watermark, type);
        }

        // Thumbnail when asked and available (always JPEG), otherwise the original
        var path = thumbnail && !string.IsNullOrEmpty(thumbnailPath) ? thumbnailPath : imagePath;
        return new ImageFile(path, null, isPublic, watermark, watermark ? "image/jpeg" : ImageValidator.ContentTypeOf(path));
    }

    /// <summary>
    /// F-M13-02 (when Upload:PublicWatermark is on): the original goes to the owner, STAFF, TEACHER (after the deadline)
    /// and ADMIN; everyone else (guests, other students) receives a watermarked copy. Full access is exactly who may download (BR-20).
    /// </summary>
    private static bool NeedsWatermark(Models.Entities.Submission submission, ClaimsPrincipal user)
        => !SubmissionImagePolicy.CanDownload(submission, user)
           && !(user.Identity?.IsAuthenticated == true && user.IsInRole(RoleNames.Admin));

    /// <inheritdoc />
    public async Task<ImageFile?> GetCompetitionCoverAsync(long competitionId)
    {
        // Competitions are listed publicly, so their cover is public too
        var path = await _db.Competitions
            .AsNoTracking()
            .Where(c => c.Id == competitionId)
            .Select(c => c.CoverImagePath)
            .FirstOrDefaultAsync();
        return string.IsNullOrEmpty(path) ? null : new ImageFile(path, null, true);
    }

    /// <summary>Download name: the original name with the extension of the file actually sent.</summary>
    private static string DownloadName(string originalFileName, string contentType)
    {
        var name = Path.GetFileNameWithoutExtension(originalFileName);
        var ext = contentType switch { "image/png" => ".png", "image/webp" => ".webp", _ => ".jpg" };
        return (string.IsNullOrWhiteSpace(name) ? "image" : name) + ext;
    }
}
