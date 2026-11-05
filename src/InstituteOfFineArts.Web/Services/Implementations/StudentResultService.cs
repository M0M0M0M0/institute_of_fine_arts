using InstituteOfFineArts.Web.Data;
using InstituteOfFineArts.Web.Helpers;
using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.Services.Interfaces;
using InstituteOfFineArts.Web.ViewModels.Common;
using InstituteOfFineArts.Web.ViewModels.Student.Results;
using Microsoft.EntityFrameworkCore;

namespace InstituteOfFineArts.Web.Services.Implementations;

/// <summary>
/// Results pages of the Student area (task BE2-08): "My Results", "Best Works" and the issued awards.
/// GRADED is a stored status (set when staff publish the results), so "results published" is checked in SQL
/// with <c>Competition.Status == GRADED</c>; evaluation and award columns are only read for those rows (BR-24).
/// </summary>
public class StudentResultService : IStudentResultService
{
    /// <summary>Text shown instead of the evaluation while the competition is not GRADED (US-10 AC2, BR-24).</summary>
    public const string AwaitingResultsMessage = "Awaiting results";

    /// <summary>Database access.</summary>
    private readonly AppDbContext _db;

    /// <summary>Receives the database context through Dependency Injection.</summary>
    public StudentResultService(AppDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<PagedResult<MyResultItemVm>> SearchMyResultsAsync(long studentProfileId, MyResultSearchVm search, int pageSize = 10)
    {
        // Only the signed-in student's entries (BR-20)
        var query = _db.Submissions.AsNoTracking().Where(s => s.StudentId == studentProfileId);

        if (search.CompetitionId is { } competitionId)
        {
            query = query.Where(s => s.CompetitionId == competitionId);
        }

        // Rating: only published results (BR-24), so an unpublished evaluation is never revealed by the filter
        if (search.Rating is { } rating)
        {
            query = query.Where(s => s.Competition.Status == CompetitionStatus.GRADED
                                     && s.Evaluation != null && s.Evaluation.Rating == rating);
        }

        // Evaluation and award columns are null unless the competition is GRADED (BR-24)
        var rows = query
            .OrderByDescending(s => s.SubmittedAt).ThenByDescending(s => s.Id)
            .Select(s => new MyResultItemVm
            {
                Id = s.Id,
                Title = s.Title,
                HasThumbnail = s.ThumbnailPath != null,
                CompetitionId = s.CompetitionId,
                CompetitionTitle = s.Competition.Title,
                StartDate = s.Competition.StartDate,
                EndDate = s.Competition.EndDate,
                CompetitionStatus = s.Competition.Status,
                SubmittedAt = s.SubmittedAt,
                Rating = s.Competition.Status == CompetitionStatus.GRADED && s.Evaluation != null ? s.Evaluation.Rating : null,
                PositivePoints = s.Competition.Status == CompetitionStatus.GRADED && s.Evaluation != null ? s.Evaluation.PositivePoints : null,
                NegativePoints = s.Competition.Status == CompetitionStatus.GRADED && s.Evaluation != null ? s.Evaluation.NegativePoints : null,
                ImprovementAreas = s.Competition.Status == CompetitionStatus.GRADED && s.Evaluation != null ? s.Evaluation.ImprovementAreas : null,
                AwardName = s.Competition.Status == CompetitionStatus.GRADED && s.AwardIssued != null ? s.AwardIssued.CompetitionAward.AwardName : null,
                AwardRank = s.Competition.Status == CompetitionStatus.GRADED && s.AwardIssued != null ? s.AwardIssued.CompetitionAward.Rank : null,
                PrizeValue = s.Competition.Status == CompetitionStatus.GRADED && s.AwardIssued != null ? s.AwardIssued.CompetitionAward.PrizeValue : null,
            });

        var page = await PagedResult<MyResultItemVm>.CreateAsync(rows, search.Page, pageSize);

        // Status for today: the stored value may be stale until the nightly job runs (BR-07)
        foreach (var item in page.Items)
        {
            item.CompetitionStatus = CompetitionStatusResolver.Resolve(
                new Competition { Status = item.CompetitionStatus, StartDate = item.StartDate, EndDate = item.EndDate });
        }
        return page;
    }

    /// <inheritdoc />
    public async Task<PagedResult<BestWorkItemVm>> SearchBestWorksAsync(BestWorkSearchVm search, int pageSize = 10)
    {
        // BEST entries of every student, only once the results are published (A-13, BR-20, BR-24)
        var query = _db.Submissions.AsNoTracking()
            .Where(s => s.Competition.Status == CompetitionStatus.GRADED
                        && s.Evaluation != null && s.Evaluation.Rating == Rating.BEST);

        if (search.CompetitionId is { } competitionId)
        {
            query = query.Where(s => s.CompetitionId == competitionId);
        }

        // Latest published competition first, then by painting title
        var rows = query
            .OrderByDescending(s => s.Competition.PublishedAt).ThenByDescending(s => s.CompetitionId)
            .ThenBy(s => s.Title).ThenBy(s => s.Id)
            .Select(s => new BestWorkItemVm
            {
                Id = s.Id,
                Title = s.Title,
                HasThumbnail = s.ThumbnailPath != null,
                StudentName = s.Student.Account.FullName,
                CompetitionId = s.CompetitionId,
                CompetitionTitle = s.Competition.Title,
                PositivePoints = s.Evaluation!.PositivePoints,
                NegativePoints = s.Evaluation.NegativePoints,
                ImprovementAreas = s.Evaluation.ImprovementAreas,
                AwardName = s.AwardIssued != null ? s.AwardIssued.CompetitionAward.AwardName : null,
            });

        return await PagedResult<BestWorkItemVm>.CreateAsync(rows, search.Page, pageSize);
    }

    /// <inheritdoc />
    public async Task<PagedResult<IssuedAwardItemVm>> SearchAwardsAsync(IssuedAwardSearchVm search, int pageSize = 10)
    {
        // Awards are public only after the results are published (role matrix §2.3: AwardIssued R, S (pub))
        var query = _db.AwardsIssued.AsNoTracking()
            .Where(a => a.Submission.Competition.Status == CompetitionStatus.GRADED);

        // Competition, part of the student name, part of the award name
        if (search.CompetitionId is { } competitionId)
        {
            query = query.Where(a => a.Submission.CompetitionId == competitionId);
        }
        if (!string.IsNullOrWhiteSpace(search.StudentName))
        {
            var name = search.StudentName.Trim();
            query = query.Where(a => a.Submission.Student.Account.FullName.Contains(name));
        }
        if (!string.IsNullOrWhiteSpace(search.Award))
        {
            var award = search.Award.Trim();
            query = query.Where(a => a.CompetitionAward.AwardName.Contains(award));
        }

        // Issued date range, both days included
        if (search.From is { } from)
        {
            query = query.Where(a => a.IssuedDate >= from);
        }
        if (search.To is { } to)
        {
            query = query.Where(a => a.IssuedDate <= to);
        }

        // Latest published competition first, then by award rank
        var rows = query
            .OrderByDescending(a => a.Submission.Competition.PublishedAt).ThenByDescending(a => a.Submission.CompetitionId)
            .ThenBy(a => a.CompetitionAward.Rank).ThenBy(a => a.Submission.Student.Account.FullName).ThenBy(a => a.Id)
            .Select(a => new IssuedAwardItemVm
            {
                Id = a.Id,
                CompetitionId = a.Submission.CompetitionId,
                CompetitionTitle = a.Submission.Competition.Title,
                AwardName = a.CompetitionAward.AwardName,
                Rank = a.CompetitionAward.Rank,
                PrizeValue = a.CompetitionAward.PrizeValue,
                PrizeDescription = a.CompetitionAward.PrizeDescription,
                StudentName = a.Submission.Student.Account.FullName,
                SubmissionId = a.SubmissionId,
                ArtworkTitle = a.Submission.Title,
                HasThumbnail = a.Submission.ThumbnailPath != null,
                IssuedDate = a.IssuedDate,
                Remarks = a.Remarks,
            });

        return await PagedResult<IssuedAwardItemVm>.CreateAsync(rows, search.Page, pageSize);
    }

    /// <inheritdoc />
    public Task<List<GradedCompetitionOptionVm>> GetGradedCompetitionsAsync()
        => _db.Competitions.AsNoTracking()
            .Where(c => c.Status == CompetitionStatus.GRADED)
            .OrderByDescending(c => c.PublishedAt).ThenByDescending(c => c.Id)
            .Select(c => new GradedCompetitionOptionVm(c.Id, c.Title))
            .ToListAsync();
}
