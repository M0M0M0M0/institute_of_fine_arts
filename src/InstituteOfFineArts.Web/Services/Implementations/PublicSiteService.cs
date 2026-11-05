using InstituteOfFineArts.Web.Data;
using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Helpers;
using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.Services.Interfaces;
using InstituteOfFineArts.Web.ViewModels.Common;
using InstituteOfFineArts.Web.ViewModels.Public;
using Microsoft.EntityFrameworkCore;

namespace InstituteOfFineArts.Web.Services.Implementations;

/// <summary>
/// Public pages (task BE2-11): home page, competition list / details and gallery. Statuses are always calculated
/// for today with CompetitionStatusResolver (BR-07, BR-42); results and winners are shown only for GRADED
/// competitions (US-02 AC4). Projections never select evaluation columns or submission texts (role matrix §2.3).
/// </summary>
public class PublicSiteService : IPublicSiteService
{
    /// <summary>Database access.</summary>
    private readonly AppDbContext _db;

    /// <summary>Receives the database context through Dependency Injection.</summary>
    public PublicSiteService(AppDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<HomeVm> GetHomeAsync()
    {
        var today = DateTimeHelper.Today;

        // Not graded and the deadline not passed = UPCOMING or ONGOING (same rule as CompetitionStatusResolver, run in SQL)
        var open = await _db.Competitions.AsNoTracking()
            .Where(c => c.Status != CompetitionStatus.GRADED && c.EndDate >= today)
            .Select(c => new HomeCompetitionVm
            {
                Id = c.Id,
                Title = c.Title,
                StartDate = c.StartDate,
                EndDate = c.EndDate,
                Status = c.Status,
                SubmissionCount = c.Submissions.Count,
            })
            .ToListAsync();

        // Status for today, not the stored value; days left only matter while ONGOING (0 = last day)
        foreach (var c in open)
        {
            c.Status = CompetitionStatusResolver.Resolve(
                new Competition { Status = c.Status, StartDate = c.StartDate, EndDate = c.EndDate }, today);
            c.DaysLeft = c.Status == CompetitionStatus.ONGOING ? c.EndDate.DayNumber - today.DayNumber : 0;
        }

        // Upcoming exhibitions: start date after today (BR-42), checked again with the resolver
        var exhibitions = await _db.Exhibitions.AsNoTracking()
            .Where(e => e.StartDate > today)
            .OrderBy(e => e.StartDate).ThenBy(e => e.Id)
            .Select(e => new HomeExhibitionVm(e.Name, e.City, e.Venue, e.StartDate, e.EndDate))
            .ToListAsync();

        return new HomeVm
        {
            Upcoming = open.Where(c => c.Status == CompetitionStatus.UPCOMING)
                .OrderBy(c => c.StartDate).ThenBy(c => c.Id).ToList(),
            Ongoing = open.Where(c => c.Status == CompetitionStatus.ONGOING)
                .OrderBy(c => c.EndDate).ThenBy(c => c.Id).ToList(),
            LatestResults = await GetLatestResultsAsync(),
            UpcomingExhibitions = exhibitions
                .Where(e => CompetitionStatusResolver.Resolve(
                    new Exhibition { StartDate = e.StartDate, EndDate = e.EndDate }, today) == ExhibitionStatus.UPCOMING)
                .ToList(),
        };
    }

    /// <inheritdoc />
    public async Task<PagedResult<PublicCompetitionListItemVm>> SearchCompetitionsAsync(PublicCompetitionSearchVm search, int pageSize = 10)
    {
        var today = DateTimeHelper.Today;
        var query = _db.Competitions.AsNoTracking();

        // Part of the title or the code
        if (!string.IsNullOrWhiteSpace(search.Keyword))
        {
            var keyword = search.Keyword.Trim();
            query = query.Where(c => c.Title.Contains(keyword) || c.Code.Contains(keyword));
        }

        // Status for today: same rule as CompetitionStatusResolver, run in SQL (GRADED is stored)
        query = search.Status switch
        {
            CompetitionStatus.GRADED => query.Where(c => c.Status == CompetitionStatus.GRADED),
            CompetitionStatus.UPCOMING => query.Where(c => c.Status != CompetitionStatus.GRADED && c.StartDate > today),
            CompetitionStatus.ONGOING => query.Where(c => c.Status != CompetitionStatus.GRADED && c.StartDate <= today && c.EndDate >= today),
            CompetitionStatus.CLOSED => query.Where(c => c.Status != CompetitionStatus.GRADED && c.EndDate < today),
            _ => query,
        };

        // Newest competitions first; summary only (no entries, no results)
        var rows = query
            .OrderByDescending(c => c.StartDate).ThenByDescending(c => c.Id)
            .Select(c => new PublicCompetitionListItemVm
            {
                Id = c.Id,
                Code = c.Code,
                Title = c.Title,
                StartDate = c.StartDate,
                EndDate = c.EndDate,
                Status = c.Status,
                AwardCount = c.Awards.Count,
                TopPrize = c.Awards.Select(a => (decimal?)a.PrizeValue).Max() ?? 0m,
            });

        var page = await PagedResult<PublicCompetitionListItemVm>.CreateAsync(rows, search.Page, pageSize);

        // Status for today, not the stored value (BR-07)
        foreach (var item in page.Items)
        {
            item.Status = CompetitionStatusResolver.Resolve(
                new Competition { Status = item.Status, StartDate = item.StartDate, EndDate = item.EndDate }, today);
        }
        return page;
    }

    /// <inheritdoc />
    public async Task<PublicCompetitionDetailsVm> GetCompetitionDetailsAsync(long id)
    {
        var c = await _db.Competitions.AsNoTracking()
            .Include(x => x.Awards)
            .FirstOrDefaultAsync(x => x.Id == id)
            ?? throw new NotFoundException($"Competition {id} was not found.");
        var status = CompetitionStatusResolver.Resolve(c);

        return new PublicCompetitionDetailsVm
        {
            Id = c.Id,
            Code = c.Code,
            Title = c.Title,
            Description = c.Description,
            Rules = c.Rules,
            StartDate = c.StartDate,
            EndDate = c.EndDate,
            Status = status,
            HasCover = c.CoverImagePath != null,
            SubmissionCount = await _db.Submissions.CountAsync(s => s.CompetitionId == id),
            Awards = c.Awards.OrderBy(a => a.Rank).ThenBy(a => a.AwardName)
                .Select(a => new PublicAwardVm(a.AwardName, a.Rank, a.PrizeValue, a.Quantity, a.PrizeDescription))
                .ToList(),
            // Results only after they are published (BR-24, US-02 AC4)
            ResultRemarks = status == CompetitionStatus.GRADED ? c.ResultRemarks : null,
            Winners = status == CompetitionStatus.GRADED ? await GetWinnersAsync(id) : new List<WinnerVm>(),
        };
    }

    /// <inheritdoc />
    public async Task<PagedResult<GalleryItemVm>> GetGalleryAsync(int page, int pageSize = 12)
    {
        // Awarded paintings of published competitions only: their images are public (BR-20)
        var rows = _db.AwardsIssued.AsNoTracking()
            .Where(a => a.Submission.Competition.Status == CompetitionStatus.GRADED)
            .OrderByDescending(a => a.Submission.Competition.PublishedAt).ThenByDescending(a => a.Submission.CompetitionId)
            .ThenBy(a => a.CompetitionAward.Rank).ThenBy(a => a.Id)
            .Select(a => new GalleryItemVm(
                a.SubmissionId,
                a.Submission.Title,
                a.Submission.ThumbnailPath != null,
                a.Submission.Student.Account.FullName,
                a.CompetitionAward.AwardName,
                a.Submission.CompetitionId,
                a.Submission.Competition.Title));

        return await PagedResult<GalleryItemVm>.CreateAsync(rows, page, pageSize);
    }

    /// <summary>
    /// Winners of the most recently published (GRADED) competition: latest PublishedAt, then latest end date and id
    /// as tie-breakers. Null when no competition is GRADED yet.
    /// </summary>
    private async Task<LatestResultsVm?> GetLatestResultsAsync()
    {
        var latest = await _db.Competitions.AsNoTracking()
            .Where(c => c.Status == CompetitionStatus.GRADED)
            .OrderByDescending(c => c.PublishedAt).ThenByDescending(c => c.EndDate).ThenByDescending(c => c.Id)
            .Select(c => new LatestResultsVm { CompetitionId = c.Id, CompetitionTitle = c.Title, PublishedAt = c.PublishedAt })
            .FirstOrDefaultAsync();
        if (latest == null)
        {
            return null;
        }

        latest.Winners = await GetWinnersAsync(latest.CompetitionId);
        return latest;
    }

    /// <summary>Issued awards of one competition, by rank then student name (only public columns).</summary>
    /// <param name="competitionId">Competition id (the caller has checked it is GRADED)</param>
    private Task<List<WinnerVm>> GetWinnersAsync(long competitionId)
        => _db.AwardsIssued.AsNoTracking()
            .Where(a => a.Submission.CompetitionId == competitionId)
            .OrderBy(a => a.CompetitionAward.Rank).ThenBy(a => a.Submission.Student.Account.FullName).ThenBy(a => a.Id)
            .Select(a => new WinnerVm(
                a.SubmissionId,
                a.Submission.Student.Account.FullName,
                a.CompetitionAward.AwardName,
                a.CompetitionAward.Rank,
                a.Submission.Title,
                a.Submission.ThumbnailPath != null))
            .ToListAsync();
}
