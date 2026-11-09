using System.Security.Claims;
using InstituteOfFineArts.Web.Data;
using InstituteOfFineArts.Web.Helpers;
using InstituteOfFineArts.Web.Infrastructure.Security;
using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.Services.Implementations;
using Microsoft.EntityFrameworkCore;

namespace InstituteOfFineArts.Tests.Services;

/// <summary>
/// ImageAccessService: database lookup + BR-20 rule + thumbnail fallback + download name (task BE1-04).
/// </summary>
public class ImageAccessServiceTests
{
    /// <summary>In-memory database with one competition (with cover) and two submissions of student 10.</summary>
    private static (AppDbContext Db, long PrivateId, long PublicId, long CompetitionId) CreateDb()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var competition = new Competition { Code = "C1", Title = "Wildlife", Status = CompetitionStatus.GRADED, CoverImagePath = "competitions/1/cover-x.jpg" };
        var privateEntry = new Submission
        {
            Competition = competition, StudentId = 10, Title = "Fox", ImagePath = "submissions/1/a.jpg",
            ThumbnailPath = "thumbnails/1/a.jpg", OriginalFileName = "My Fox.jpeg",
            Evaluation = new Evaluation { Rating = Rating.GOOD },
        };
        var publicEntry = new Submission
        {
            Competition = competition, StudentId = 10, Title = "Owl", ImagePath = "submissions/1/b.jpg",
            ThumbnailPath = null, OriginalFileName = "owl.jpg",
            Evaluation = new Evaluation { Rating = Rating.BEST },
        };
        db.Submissions.AddRange(privateEntry, publicEntry);
        db.SaveChanges();
        return (db, privateEntry.Id, publicEntry.Id, competition.Id);
    }

    /// <summary>Anonymous visitor.</summary>
    private static readonly ClaimsPrincipal Guest = new(new ClaimsIdentity());

    /// <summary>The owner (student profile 10).</summary>
    private static readonly ClaimsPrincipal Owner = new(new ClaimsIdentity(new[]
    {
        new Claim(ClaimTypes.Role, RoleNames.Student), new Claim(ClaimsPrincipalExtensions.ProfileIdClaim, "10"),
    }, "Test"));

    /// <summary>Unknown submission → null (404).</summary>
    [Fact]
    public async Task UnknownSubmission_Null()
    {
        var (db, _, _, _) = CreateDb();
        using var _ = db;
        Assert.Null(await new ImageAccessService(db).GetSubmissionImageAsync(999, Owner, false, false));
    }

    /// <summary>Guest: private entry hidden; public BEST entry visible, falling back to the original without thumbnail.</summary>
    [Fact]
    public async Task Guest_SeesOnlyPublic()
    {
        var (db, privateId, publicId, _) = CreateDb();
        using var _ = db;
        var service = new ImageAccessService(db);

        Assert.Null(await service.GetSubmissionImageAsync(privateId, Guest, true, false));
        var image = await service.GetSubmissionImageAsync(publicId, Guest, thumbnail: true, download: false);
        Assert.NotNull(image);
        Assert.Equal("submissions/1/b.jpg", image!.RelativePath);
        Assert.True(image.IsPublic);
        Assert.Null(await service.GetSubmissionImageAsync(publicId, Guest, false, download: true));
    }

    /// <summary>Owner: thumbnail path when available; download uses the original name with .jpg.</summary>
    [Fact]
    public async Task Owner_ThumbnailAndDownload()
    {
        var (db, privateId, _, _) = CreateDb();
        using var _ = db;
        var service = new ImageAccessService(db);

        Assert.Equal("thumbnails/1/a.jpg", (await service.GetSubmissionImageAsync(privateId, Owner, true, false))!.RelativePath);
        var download = await service.GetSubmissionImageAsync(privateId, Owner, false, true);
        Assert.Equal("submissions/1/a.jpg", download!.RelativePath);
        Assert.Equal("My Fox.jpg", download.DownloadName);
        Assert.False(download.IsPublic);
    }

    /// <summary>Signed-in user with the given role and student profile id.</summary>
    private static ClaimsPrincipal UserWith(string role, string profileId = "99") => new(new ClaimsIdentity(new[]
    {
        new Claim(ClaimTypes.Role, role), new Claim(ClaimsPrincipalExtensions.ProfileIdClaim, profileId),
    }, "Test"));

    /// <summary>F-M13-02: guests and other students get the public image watermarked (view and thumbnail).</summary>
    [Fact]
    public async Task Watermark_GuestAndOtherStudent()
    {
        var (db, _, publicId, _) = CreateDb();
        using var _ = db;
        var service = new ImageAccessService(db);

        Assert.True((await service.GetSubmissionImageAsync(publicId, Guest, false, false))!.Watermark);
        Assert.True((await service.GetSubmissionImageAsync(publicId, Guest, true, false))!.Watermark);
        Assert.True((await service.GetSubmissionImageAsync(publicId, UserWith(RoleNames.Student), false, false))!.Watermark);
    }

    /// <summary>F-M13-02: owner, staff, manager and admin get the original (no watermark), downloads included.</summary>
    [Fact]
    public async Task Watermark_NotForOwnerStaffManagerAdmin()
    {
        var (db, privateId, publicId, _) = CreateDb();
        using var _ = db;
        var service = new ImageAccessService(db);

        Assert.False((await service.GetSubmissionImageAsync(publicId, Owner, false, false))!.Watermark);
        Assert.False((await service.GetSubmissionImageAsync(privateId, Owner, false, true))!.Watermark);
        Assert.False((await service.GetSubmissionImageAsync(privateId, UserWith(RoleNames.Staff), false, true))!.Watermark);
        Assert.False((await service.GetSubmissionImageAsync(privateId, UserWith(RoleNames.Manager), true, false))!.Watermark);
        Assert.False((await service.GetSubmissionImageAsync(publicId, UserWith(RoleNames.Admin), false, false))!.Watermark);
    }

    /// <summary>Cover: returned when set, null for an unknown competition.</summary>
    [Fact]
    public async Task Cover()
    {
        var (db, _, _, competitionId) = CreateDb();
        using var _ = db;
        var service = new ImageAccessService(db);
        Assert.Equal("competitions/1/cover-x.jpg", (await service.GetCompetitionCoverAsync(competitionId))!.RelativePath);
        Assert.Null(await service.GetCompetitionCoverAsync(999));
    }
}
