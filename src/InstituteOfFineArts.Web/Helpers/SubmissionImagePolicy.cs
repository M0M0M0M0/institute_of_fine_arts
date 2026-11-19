using System.Security.Claims;
using InstituteOfFineArts.Web.Infrastructure.Security;
using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Models.Enums;

namespace InstituteOfFineArts.Web.Helpers;

/// <summary>
/// Who may see or download the image of a submission (BR-20, role matrix in docs/plan/01 section 3):
/// STAFF: every image, view + download (they run the competitions and help the students, also while it is open).
/// TEACHER: every image of a competition whose submission period is over (CLOSED or GRADED), view + download;
/// before the deadline only what is public, so no painting leaks to the jury early.
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

    /// <summary>True when the submission period of the entry's competition is over: CLOSED or GRADED (today's date).</summary>
    public static bool IsClosedForEntries(Submission submission)
        => submission.Competition is { } c
           && CompetitionStatusResolver.Resolve(c) is CompetitionStatus.CLOSED or CompetitionStatus.GRADED;

    /// <summary>True when <paramref name="user"/> may view the image (inline in the page).</summary>
    public static bool CanView(Submission submission, ClaimsPrincipal user)
        => HasFullAccess(submission, user) || IsPublic(submission);

    /// <summary>True when <paramref name="user"/> may download the original file (staff, teacher after the deadline, owner).</summary>
    public static bool CanDownload(Submission submission, ClaimsPrincipal user)
        => HasFullAccess(submission, user);

    /// <summary>Staff see everything, teachers everything once the competition is closed, a student their own submissions.</summary>
    private static bool HasFullAccess(Submission submission, ClaimsPrincipal user)
    {
        if (user.Identity?.IsAuthenticated != true)
        {
            return false;
        }
        if (user.IsInRole(RoleNames.Staff))
        {
            return true;
        }
        if (user.IsInRole(RoleNames.Teacher))
        {
            return IsClosedForEntries(submission);
        }
        return user.IsInRole(RoleNames.Student)
               && long.TryParse(user.FindFirstValue(ClaimsPrincipalExtensions.ProfileIdClaim), out var profileId)
               && profileId == submission.StudentId;
    }
}
