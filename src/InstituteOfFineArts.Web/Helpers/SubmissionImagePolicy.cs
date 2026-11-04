using System.Security.Claims;
using InstituteOfFineArts.Web.Infrastructure.Security;
using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Models.Enums;

namespace InstituteOfFineArts.Web.Helpers;

/// <summary>
/// Who may see or download the image of a submission (BR-20, role matrix in docs/plan/01 section 3):
/// STAFF and MANAGER: every image, view + download.
/// STUDENT: own images, view + download.
/// Everyone else (GUEST, other students, ADMIN): only public images – BEST or awarded entries of a GRADED
/// competition – view only.
/// The submission must be loaded with Competition, Evaluation and AwardIssued.
/// </summary>
public static class SubmissionImagePolicy
{
    /// <summary>True when the image is shown publicly: competition GRADED and the entry is BEST or won an award.</summary>
    public static bool IsPublic(Submission submission)
        => submission.Competition?.Status == CompetitionStatus.GRADED
           && (submission.Evaluation?.Rating == Rating.BEST || submission.AwardIssued != null);

    /// <summary>True when <paramref name="user"/> may view the image (inline in the page).</summary>
    public static bool CanView(Submission submission, ClaimsPrincipal user)
        => HasFullAccess(submission, user) || IsPublic(submission);

    /// <summary>True when <paramref name="user"/> may download the original file (staff, manager, owner).</summary>
    public static bool CanDownload(Submission submission, ClaimsPrincipal user)
        => HasFullAccess(submission, user);

    /// <summary>Staff and managers see everything; a student sees their own submissions.</summary>
    private static bool HasFullAccess(Submission submission, ClaimsPrincipal user)
    {
        if (user.Identity?.IsAuthenticated != true)
        {
            return false;
        }
        if (user.IsInRole(RoleNames.Staff) || user.IsInRole(RoleNames.Manager))
        {
            return true;
        }
        return user.IsInRole(RoleNames.Student)
               && long.TryParse(user.FindFirstValue(ClaimsPrincipalExtensions.ProfileIdClaim), out var profileId)
               && profileId == submission.StudentId;
    }
}
