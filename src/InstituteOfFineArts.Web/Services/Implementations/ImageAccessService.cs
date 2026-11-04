using System.Security.Claims;
using InstituteOfFineArts.Web.Data;
using InstituteOfFineArts.Web.Helpers;
using InstituteOfFineArts.Web.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace InstituteOfFineArts.Web.Services.Implementations;

/// <summary>
/// Looks up image paths in the database and checks who may see them (BR-20).
/// </summary>
public class ImageAccessService : IImageAccessService
{
    /// <summary>Database access.</summary>
    private readonly AppDbContext _db;

    /// <summary>Receives the database context through Dependency Injection.</summary>
    public ImageAccessService(AppDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<ImageFile?> GetSubmissionImageAsync(long submissionId, ClaimsPrincipal user, bool thumbnail, bool download)
    {
        // Load the submission with what the rule needs: competition status, rating, award
        var submission = await _db.Submissions
            .AsNoTracking()
            .Include(s => s.Competition)
            .Include(s => s.Evaluation)
            .Include(s => s.AwardIssued)
            .FirstOrDefaultAsync(s => s.Id == submissionId);
        if (submission == null)
        {
            return null;
        }

        // Viewing and downloading have different rules
        var allowed = download
            ? SubmissionImagePolicy.CanDownload(submission, user)
            : SubmissionImagePolicy.CanView(submission, user);
        if (!allowed)
        {
            return null;
        }

        var isPublic = SubmissionImagePolicy.IsPublic(submission);
        if (download)
        {
            return new ImageFile(submission.ImagePath, DownloadName(submission.OriginalFileName), isPublic);
        }

        // Thumbnail when asked and available, otherwise the original
        var path = thumbnail && !string.IsNullOrEmpty(submission.ThumbnailPath) ? submission.ThumbnailPath : submission.ImagePath;
        return new ImageFile(path, null, isPublic);
    }

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

    /// <summary>Download name: the original name with a .jpg extension.</summary>
    private static string DownloadName(string originalFileName)
    {
        var name = Path.GetFileNameWithoutExtension(originalFileName);
        return (string.IsNullOrWhiteSpace(name) ? "image" : name) + ".jpg";
    }
}
