using System.Security.Claims;

namespace InstituteOfFineArts.Web.Services.Interfaces;

/// <summary>
/// An image the current user is allowed to receive.
/// </summary>
/// <param name="RelativePath">Path below the upload root</param>
/// <param name="DownloadName">File name for "Save as" when downloading; null = show inline</param>
/// <param name="IsPublic">True when anyone may see it (allows public browser caching)</param>
/// <param name="Watermark">
/// True when the image must be sent with the institute watermark (F-M13-02): the viewer is neither the owner
/// nor STAFF / MANAGER / ADMIN
/// </param>
public record ImageFile(string RelativePath, string? DownloadName, bool IsPublic, bool Watermark = false);

/// <summary>
/// Finds stored images and applies the viewing rules (BR-20) before FilesController streams them,
/// and tells which images must be watermarked (F-M13-02).
/// </summary>
public interface IImageAccessService
{
    /// <summary>
    /// Image of a submission for <paramref name="user"/>, or null when the submission does not exist or the user
    /// may not see it (or may not download it, when <paramref name="download"/> is true).
    /// </summary>
    /// <param name="submissionId">Submission id</param>
    /// <param name="user">Current user (may be anonymous)</param>
    /// <param name="thumbnail">True for the small version (falls back to the original when there is none)</param>
    /// <param name="download">True to download the original with its file name</param>
    Task<ImageFile?> GetSubmissionImageAsync(long submissionId, ClaimsPrincipal user, bool thumbnail, bool download);

    /// <summary>Cover image of a competition (public), or null when there is none.</summary>
    /// <param name="competitionId">Competition id</param>
    Task<ImageFile?> GetCompetitionCoverAsync(long competitionId);
}
