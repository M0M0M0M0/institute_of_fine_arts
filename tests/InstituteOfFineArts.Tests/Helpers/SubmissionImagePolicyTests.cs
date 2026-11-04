using System.Security.Claims;
using InstituteOfFineArts.Web.Helpers;
using InstituteOfFineArts.Web.Infrastructure.Security;
using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Models.Enums;

namespace InstituteOfFineArts.Tests.Helpers;

/// <summary>
/// Who may view / download a submission image (BR-20, task BE1-04).
/// </summary>
public class SubmissionImagePolicyTests
{
    /// <summary>Student profile id of the owner.</summary>
    private const long OwnerId = 10;

    /// <summary>Submission of student 10 in a competition with the given status / rating / award.</summary>
    private static Submission Entry(CompetitionStatus status, Rating? rating = null, bool awarded = false) => new()
    {
        StudentId = OwnerId,
        Competition = new Competition { Status = status },
        Evaluation = rating == null ? null : new Evaluation { Rating = rating.Value },
        AwardIssued = awarded ? new AwardIssued() : null,
    };

    /// <summary>Signed-in user with a role and optional profile id; null role = anonymous guest.</summary>
    private static ClaimsPrincipal User(string? role, long? profileId = null)
    {
        if (role == null)
        {
            return new ClaimsPrincipal(new ClaimsIdentity());
        }
        var claims = new List<Claim> { new(ClaimTypes.Role, role) };
        if (profileId != null)
        {
            claims.Add(new Claim(ClaimsPrincipalExtensions.ProfileIdClaim, profileId.Value.ToString()));
        }
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }

    /// <summary>Staff and managers view and download every image, even unpublished ones.</summary>
    [Theory]
    [InlineData(RoleNames.Staff)]
    [InlineData(RoleNames.Manager)]
    public void StaffAndManager_FullAccess(string role)
    {
        var entry = Entry(CompetitionStatus.ONGOING);
        Assert.True(SubmissionImagePolicy.CanView(entry, User(role, 1)));
        Assert.True(SubmissionImagePolicy.CanDownload(entry, User(role, 1)));
    }

    /// <summary>The owner views and downloads their own image at any time.</summary>
    [Fact]
    public void Owner_FullAccess()
    {
        var entry = Entry(CompetitionStatus.ONGOING);
        Assert.True(SubmissionImagePolicy.CanView(entry, User(RoleNames.Student, OwnerId)));
        Assert.True(SubmissionImagePolicy.CanDownload(entry, User(RoleNames.Student, OwnerId)));
    }

    /// <summary>Another student, a guest and the admin cannot see an image that is not public.</summary>
    [Theory]
    [InlineData(RoleNames.Student, 11L)]
    [InlineData(RoleNames.Admin, 1L)]
    [InlineData(null, null)]
    public void Others_PrivateImage_Denied(string? role, long? profileId)
    {
        var user = User(role, profileId);
        foreach (var entry in new[]
                 {
                     Entry(CompetitionStatus.ONGOING),
                     Entry(CompetitionStatus.CLOSED, Rating.BEST),          // BEST but results not published yet
                     Entry(CompetitionStatus.GRADED, Rating.GOOD),          // published but not BEST, no award
                 })
        {
            Assert.False(SubmissionImagePolicy.CanView(entry, user));
            Assert.False(SubmissionImagePolicy.CanDownload(entry, user));
        }
    }

    /// <summary>BEST or awarded entries of a GRADED competition are public: view yes, download no.</summary>
    [Theory]
    [InlineData(Rating.BEST, false)]
    [InlineData(Rating.BETTER, true)]
    public void Others_PublicImage_ViewOnly(Rating rating, bool awarded)
    {
        var entry = Entry(CompetitionStatus.GRADED, rating, awarded);
        Assert.True(SubmissionImagePolicy.IsPublic(entry));
        foreach (var user in new[] { User(null), User(RoleNames.Student, 11), User(RoleNames.Admin, 1) })
        {
            Assert.True(SubmissionImagePolicy.CanView(entry, user));
            Assert.False(SubmissionImagePolicy.CanDownload(entry, user));
        }
    }

    /// <summary>A student account without a profile id claim is not treated as the owner.</summary>
    [Fact]
    public void StudentWithoutProfileClaim_NotOwner()
    {
        Assert.False(SubmissionImagePolicy.CanView(Entry(CompetitionStatus.ONGOING), User(RoleNames.Student)));
    }
}
