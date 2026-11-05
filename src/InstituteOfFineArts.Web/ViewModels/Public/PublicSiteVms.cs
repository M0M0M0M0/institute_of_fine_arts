using System.ComponentModel.DataAnnotations;
using InstituteOfFineArts.Web.Models.Enums;

namespace InstituteOfFineArts.Web.ViewModels.Public;

/// <summary>
/// Home page "/" (F-M02-01..04, 06; SCR-01, US-02). Only public data: no evaluation and no submission text or
/// description (role matrix §2.3, GUEST column).
/// </summary>
public class HomeVm
{
    /// <summary>UPCOMING competitions, soonest start first (F-M02-01).</summary>
    public List<HomeCompetitionVm> Upcoming { get; set; } = new();

    /// <summary>ONGOING competitions with days left and number of entries, nearest deadline first (F-M02-02, 03).</summary>
    public List<HomeCompetitionVm> Ongoing { get; set; } = new();

    /// <summary>Award winners of the most recent GRADED competition, or null when nothing is published (F-M02-04).</summary>
    public LatestResultsVm? LatestResults { get; set; }

    /// <summary>UPCOMING exhibitions, soonest start first (F-M02-06).</summary>
    public List<HomeExhibitionVm> UpcomingExhibitions { get; set; } = new();
}

/// <summary>A competition card of the home page.</summary>
public class HomeCompetitionVm
{
    /// <summary>Competition id.</summary>
    public long Id { get; set; }

    /// <summary>Title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>First day of the competition.</summary>
    public DateOnly StartDate { get; set; }

    /// <summary>Last day to submit (deadline 23:59:59).</summary>
    public DateOnly EndDate { get; set; }

    /// <summary>Stored status until it is resolved for today (BR-07).</summary>
    public CompetitionStatus Status { get; set; }

    /// <summary>Number of entries (shown for ONGOING competitions, F-M02-03).</summary>
    public int SubmissionCount { get; set; }

    /// <summary>Whole days left until the deadline: 0 = today is the last day (ONGOING only).</summary>
    public int DaysLeft { get; set; }
}

/// <summary>Winners of the most recent GRADED competition (F-M02-04, US-02 AC3).</summary>
public class LatestResultsVm
{
    /// <summary>Competition id.</summary>
    public long CompetitionId { get; set; }

    /// <summary>Competition title.</summary>
    public string CompetitionTitle { get; set; } = string.Empty;

    /// <summary>Time the results were published.</summary>
    public DateTime? PublishedAt { get; set; }

    /// <summary>Winners ordered by award rank.</summary>
    public List<WinnerVm> Winners { get; set; } = new();
}

/// <summary>One award winner (public data only: student, award, painting title, thumbnail).</summary>
/// <param name="SubmissionId">Winning submission id (its image is public once GRADED, BR-20)</param>
/// <param name="StudentName">Full name of the student</param>
/// <param name="AwardName">Award name</param>
/// <param name="Rank">Award rank (1 = highest)</param>
/// <param name="ArtworkTitle">Painting title</param>
/// <param name="HasThumbnail">True when a thumbnail exists</param>
public record WinnerVm(long SubmissionId, string StudentName, string AwardName, int Rank, string ArtworkTitle, bool HasThumbnail);

/// <summary>An upcoming exhibition on the home page (F-M02-06).</summary>
/// <param name="Name">Exhibition name</param>
/// <param name="City">City / province</param>
/// <param name="Venue">Venue</param>
/// <param name="StartDate">Start date</param>
/// <param name="EndDate">End date</param>
public record HomeExhibitionVm(string Name, string City, string Venue, DateOnly StartDate, DateOnly EndDate);

/// <summary>Filters of the public competition list /competitions (F-M02-05, SCR-02, TC-M02-05): title, status, page.</summary>
public class PublicCompetitionSearchVm
{
    /// <summary>Part of the title or the code.</summary>
    [StringLength(100)]
    [Display(Name = "Title")]
    public string? Keyword { get; set; }

    /// <summary>Status calculated for today (BR-07); GRADED = results published.</summary>
    [Display(Name = "Status")]
    public CompetitionStatus? Status { get; set; }

    /// <summary>Page number (1-based).</summary>
    public int Page { get; set; } = 1;
}

/// <summary>One row of the public competition list (summary only).</summary>
public class PublicCompetitionListItemVm
{
    /// <summary>Competition id.</summary>
    public long Id { get; set; }

    /// <summary>Competition code.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>First day.</summary>
    public DateOnly StartDate { get; set; }

    /// <summary>Last day to submit.</summary>
    public DateOnly EndDate { get; set; }

    /// <summary>Stored status until it is resolved for today (BR-07).</summary>
    public CompetitionStatus Status { get; set; }

    /// <summary>Number of awards defined.</summary>
    public int AwardCount { get; set; }

    /// <summary>Highest prize value in USD (0 when no award is defined).</summary>
    public decimal TopPrize { get; set; }
}

/// <summary>
/// Public details of a competition /competitions/{id} (F-M02-05, SCR-02): description, rules, dates, awards and,
/// once GRADED, the winners. Never contains entries' texts or evaluations.
/// </summary>
public class PublicCompetitionDetailsVm
{
    /// <summary>Competition id.</summary>
    public long Id { get; set; }

    /// <summary>Competition code.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Description.</summary>
    public string? Description { get; set; }

    /// <summary>Rules / conditions.</summary>
    public string? Rules { get; set; }

    /// <summary>First day.</summary>
    public DateOnly StartDate { get; set; }

    /// <summary>Last day to submit (deadline 23:59:59).</summary>
    public DateOnly EndDate { get; set; }

    /// <summary>Status calculated for today (BR-07).</summary>
    public CompetitionStatus Status { get; set; }

    /// <summary>True when a cover image exists (/files/competitions/{id}/cover).</summary>
    public bool HasCover { get; set; }

    /// <summary>Number of entries so far.</summary>
    public int SubmissionCount { get; set; }

    /// <summary>Awards defined for the competition, by rank.</summary>
    public List<PublicAwardVm> Awards { get; set; } = new();

    /// <summary>General remarks about the results, only when GRADED.</summary>
    public string? ResultRemarks { get; set; }

    /// <summary>Winners, only when GRADED (empty otherwise, US-02 AC4).</summary>
    public List<WinnerVm> Winners { get; set; } = new();
}

/// <summary>An award defined for a competition (public).</summary>
/// <param name="Name">Award name</param>
/// <param name="Rank">Rank (1 = highest)</param>
/// <param name="PrizeValue">Prize value in USD</param>
/// <param name="Quantity">Number of slots</param>
/// <param name="Description">Prize description</param>
public record PublicAwardVm(string Name, int Rank, decimal PrizeValue, int Quantity, string? Description);

/// <summary>One awarded painting of the public gallery /gallery (F-M02-07, SCR-06).</summary>
/// <param name="SubmissionId">Submission id (public image once GRADED, BR-20)</param>
/// <param name="ArtworkTitle">Painting title</param>
/// <param name="HasThumbnail">True when a thumbnail exists</param>
/// <param name="StudentName">Full name of the student</param>
/// <param name="AwardName">Award name</param>
/// <param name="CompetitionId">Competition id</param>
/// <param name="CompetitionTitle">Competition title</param>
public record GalleryItemVm(long SubmissionId, string ArtworkTitle, bool HasThumbnail, string StudentName, string AwardName,
    long CompetitionId, string CompetitionTitle);
