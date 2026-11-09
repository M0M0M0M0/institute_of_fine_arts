using InstituteOfFineArts.Web.Data;
using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Helpers;
using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.Services.Interfaces;
using InstituteOfFineArts.Web.ViewModels.Common;
using InstituteOfFineArts.Web.ViewModels.Staff.Competitions;
using Microsoft.EntityFrameworkCore;

namespace InstituteOfFineArts.Web.Services.Implementations;

/// <summary>
/// Competition management for staff. The stored status column is refreshed on every save; screens always show
/// the status calculated from the dates (CompetitionStatusResolver), so they are right even before the
/// nightly status job (BE2-05) runs.
/// </summary>
public class CompetitionService : ICompetitionService
{
    /// <summary>Message when a competition with entries is deleted (TC-M05-06).</summary>
    public const string HasSubmissionsDeleteMessage = "A competition with submissions cannot be deleted.";

    /// <summary>Message when a GRADED competition is edited.</summary>
    public const string GradedReadOnlyMessage = "Results have been published; this competition can no longer be edited.";

    /// <summary>Message when the end date is in the past.</summary>
    public const string EndInPastMessage = "The end date cannot be in the past.";

    /// <summary>Message when the start date changes after entries exist.</summary>
    public const string StartLockedMessage = "The start date cannot be changed after students have submitted entries.";

    /// <summary>Message when the end date is moved earlier after entries exist.</summary>
    public const string EndOnlyExtendMessage = "Entries already exist, so the end date can only be extended.";

    /// <summary>Message when the title changes after entries exist.</summary>
    public const string TitleLockedMessage = "The title cannot be changed after students have submitted entries.";

    /// <summary>Advice shown until at least one award exists (BR-09).</summary>
    public const string NoAwardsWarning = "No awards yet. Add at least one award before the competition starts.";

    /// <summary>Database access.</summary>
    private readonly AppDbContext _db;

    /// <summary>Stores cover images.</summary>
    private readonly IFileStorageService _files;

    /// <summary>Logs created / changed / deleted competitions.</summary>
    private readonly ILogger<CompetitionService> _logger;

    /// <summary>Tells the students about a new competition (optional feature M12).</summary>
    private readonly INotificationService _notifications;

    /// <summary>Receives the required services through Dependency Injection.</summary>
    public CompetitionService(AppDbContext db, IFileStorageService files, ILogger<CompetitionService> logger,
        INotificationService notifications)
    {
        _db = db;
        _files = files;
        _logger = logger;
        _notifications = notifications;
    }

    /// <inheritdoc />
    public async Task<PagedResult<CompetitionListItemVm>> SearchAsync(CompetitionSearchVm search, int pageSize = 10)
    {
        var today = DateTimeHelper.Today;
        var query = _db.Competitions.AsNoTracking().AsQueryable();

        // Keyword: part of the title or the code
        if (!string.IsNullOrWhiteSpace(search.Keyword))
        {
            var keyword = search.Keyword.Trim();
            query = query.Where(c => c.Title.Contains(keyword) || c.Code.Contains(keyword));
        }

        // Status: the same rule as CompetitionStatusResolver, written as a query so SQL Server filters it
        query = query.WhereStatus(search.Status, today);

        // Date range: competitions whose period overlaps [From, To]
        if (search.From is { } from)
        {
            query = query.Where(c => c.EndDate >= from);
        }
        if (search.To is { } to)
        {
            query = query.Where(c => c.StartDate <= to);
        }

        var rows = query
            .OrderByDescending(c => c.StartDate).ThenByDescending(c => c.Id)
            .Select(c => new CompetitionListItemVm
            {
                Id = c.Id,
                Code = c.Code,
                Title = c.Title,
                StartDate = c.StartDate,
                EndDate = c.EndDate,
                Status = c.Status,
                SubmissionCount = c.Submissions.Count,
                AwardCount = c.Awards.Count,
                HasCover = c.CoverImagePath != null,
            });

        var page = await PagedResult<CompetitionListItemVm>.CreateAsync(rows, search.Page, pageSize);

        // Show the status for today, not the possibly stale stored value
        foreach (var item in page.Items)
        {
            item.Status = CompetitionStatusResolver.Resolve(
                new Competition { Status = item.Status, StartDate = item.StartDate, EndDate = item.EndDate }, today);
        }
        return page;
    }

    /// <inheritdoc />
    public async Task<CompetitionDetailsVm> GetDetailsAsync(long id)
    {
        var c = await _db.Competitions
            .AsNoTracking()
            .Include(x => x.CreatedByStaff).ThenInclude(s => s.Account)
            .Include(x => x.PublishedByStaff).ThenInclude(s => s!.Account)
            .Include(x => x.Awards).ThenInclude(a => a.IssuedAwards)
            .FirstOrDefaultAsync(x => x.Id == id)
            ?? throw new NotFoundException($"Competition {id} was not found.");
        var submissionCount = await _db.Submissions.CountAsync(s => s.CompetitionId == id);
        var evaluatedCount = await _db.Submissions.CountAsync(s => s.CompetitionId == id && s.Evaluation != null);
        var status = CompetitionStatusResolver.Resolve(c);

        // Winners of each award (task BE1-11): student and artwork, grouped by award below
        var winners = await _db.AwardsIssued.AsNoTracking()
            .Where(i => i.CompetitionAward.CompetitionId == id)
            .OrderBy(i => i.IssuedDate).ThenBy(i => i.Id)
            .Select(i => new
            {
                i.CompetitionAwardId,
                Vm = new AwardWinnerVm(i.Id, i.SubmissionId, i.Submission.Title, i.Submission.Student.Account.FullName,
                    i.Submission.Student.StudentCode, i.IssuedDate),
            })
            .ToListAsync();

        var vm = new CompetitionDetailsVm
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
            CreatedByName = c.CreatedByStaff?.Account?.FullName ?? string.Empty,
            CreatedAt = c.CreatedAt,
            PublishedAt = c.PublishedAt,
            PublishedByName = c.PublishedByStaff?.Account?.FullName,
            ResultRemarks = c.ResultRemarks,
            SubmissionCount = submissionCount,
            EvaluatedCount = evaluatedCount,
            Awards = c.Awards.OrderBy(a => a.Rank).ThenBy(a => a.AwardName)
                .Select(a => new AwardRowVm(a.Id, a.AwardName, a.Rank, a.PrizeValue, a.Quantity, a.PrizeDescription, a.IssuedAwards.Count)
                {
                    Winners = winners.Where(w => w.CompetitionAwardId == a.Id).Select(w => w.Vm).ToList(),
                })
                .ToList(),
            CanEdit = status != CompetitionStatus.GRADED,
            CanManageAwards = status != CompetitionStatus.GRADED,
            CanDelete = submissionCount == 0,
            // Task BE1-11: awards are issued when CLOSED or GRADED (BR-32); results are published from CLOSED (BR-08)
            CanPublish = status == CompetitionStatus.CLOSED,
            PublishBlockedReason = submissionCount > evaluatedCount
                ? CompetitionResultService.NotGradedMessage(submissionCount - evaluatedCount)
                : null,
        };
        vm.CanIssueAwards = status is CompetitionStatus.CLOSED or CompetitionStatus.GRADED
            && vm.Awards.Any(a => a.IssuedCount < a.Quantity);

        // BR-09: awards must exist before the competition opens
        if (vm.Awards.Count == 0 && status is CompetitionStatus.UPCOMING or CompetitionStatus.ONGOING)
        {
            vm.Warnings.Add(NoAwardsWarning);
        }
        return vm;
    }

    /// <inheritdoc />
    public async Task<CompetitionFormVm> GetForEditAsync(long id)
    {
        var c = await FindAsync(id);
        EnsureEditable(c);
        return new CompetitionFormVm
        {
            Id = c.Id,
            Code = c.Code,
            Title = c.Title,
            Description = c.Description,
            Rules = c.Rules,
            StartDate = c.StartDate,
            EndDate = c.EndDate,
            HasCover = c.CoverImagePath != null,
            HasSubmissions = await _db.Submissions.AnyAsync(s => s.CompetitionId == id),
        };
    }

    /// <inheritdoc />
    public async Task<long> CreateAsync(CompetitionFormVm form, long staffProfileId)
    {
        var (start, end) = Dates(form);

        // BR-06: a new competition must still be open on its last day
        if (end < DateTimeHelper.Today)
        {
            throw new BusinessException(EndInPastMessage, nameof(CompetitionFormVm.EndDate));
        }

        var competition = new Competition
        {
            Code = await NextCodeAsync(start),
            Title = form.Title.Trim(),
            Description = Clean(form.Description),
            Rules = Clean(form.Rules),
            StartDate = start,
            EndDate = end,
            CreatedBy = staffProfileId,
        };
        competition.Status = CompetitionStatusResolver.Resolve(competition);
        _db.Competitions.Add(competition);
        await _db.SaveChangesAsync();

        // Cover after the insert: the file goes in the folder of the new id.
        // A rejected or failed cover undoes the insert, so the user can correct the form and save again.
        if (form.CoverImage is { Length: > 0 })
        {
            try
            {
                competition.CoverImagePath = await _files.SaveCompetitionCoverAsync(form.CoverImage, competition.Id,
                    nameof(CompetitionFormVm.CoverImage));
                await SaveOrDeleteFileAsync(competition.CoverImagePath);
            }
            catch
            {
                _db.Competitions.Remove(competition);
                await _db.SaveChangesAsync();
                throw;
            }
        }

        _logger.LogInformation("Competition {Code} created by staff {StaffId}", competition.Code, staffProfileId);

        // M12: every active student hears about the new competition (in-app + e-mail; never fails the save)
        await _notifications.CompetitionCreatedAsync(competition.Id);
        return competition.Id;
    }

    /// <inheritdoc />
    public async Task UpdateAsync(long id, CompetitionFormVm form)
    {
        var c = await FindAsync(id);
        EnsureEditable(c);
        var (start, end) = Dates(form);
        var hasSubmissions = await _db.Submissions.AnyAsync(s => s.CompetitionId == id);

        if (hasSubmissions)
        {
            // BR-10: entries exist → title and start date are fixed, the end date can only move later
            if (form.Title.Trim() != c.Title)
            {
                throw new BusinessException(TitleLockedMessage, nameof(CompetitionFormVm.Title));
            }
            if (start != c.StartDate)
            {
                throw new BusinessException(StartLockedMessage, nameof(CompetitionFormVm.StartDate));
            }
            if (end < c.EndDate)
            {
                throw new BusinessException(EndOnlyExtendMessage, nameof(CompetitionFormVm.EndDate));
            }
        }
        if (end != c.EndDate && end < DateTimeHelper.Today)
        {
            throw new BusinessException(EndInPastMessage, nameof(CompetitionFormVm.EndDate));
        }

        c.Title = form.Title.Trim();
        c.Description = Clean(form.Description);
        c.Rules = Clean(form.Rules);
        c.StartDate = start;
        c.EndDate = end;
        c.Status = CompetitionStatusResolver.Resolve(c);

        // Cover: a new file replaces the old one; "remove" clears it. Old files are deleted only after saving.
        var oldCover = c.CoverImagePath;
        string? newCover = null;
        if (form.CoverImage is { Length: > 0 })
        {
            newCover = await _files.SaveCompetitionCoverAsync(form.CoverImage, c.Id, nameof(CompetitionFormVm.CoverImage));
            c.CoverImagePath = newCover;
        }
        else if (form.RemoveCover)
        {
            c.CoverImagePath = null;
        }

        await SaveOrDeleteFileAsync(newCover);
        if (oldCover != null && oldCover != c.CoverImagePath)
        {
            _files.Delete(oldCover);
        }
        _logger.LogInformation("Competition {Code} updated", c.Code);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(long id)
    {
        var c = await FindAsync(id);

        // BR-11: only without entries (their images and evaluations must never be lost)
        if (await _db.Submissions.AnyAsync(s => s.CompetitionId == id))
        {
            throw new BusinessException(HasSubmissionsDeleteMessage);
        }

        // Awards are removed by the cascade rule; the cover file after the delete succeeded
        _db.Competitions.Remove(c);
        await _db.SaveChangesAsync();
        _files.Delete(c.CoverImagePath);
        _logger.LogInformation("Competition {Code} deleted", c.Code);
    }

    /// <summary>
    /// Next code for the start year: CT{year}-{nn}, nn = highest existing number + 1 (CT2026-01, CT2026-02…).
    /// </summary>
    private async Task<string> NextCodeAsync(DateOnly start)
    {
        var prefix = $"CT{start.Year}-";
        var codes = await _db.Competitions.Where(c => c.Code.StartsWith(prefix)).Select(c => c.Code).ToListAsync();
        var max = codes.Select(code => int.TryParse(code[prefix.Length..], out var n) ? n : 0).DefaultIfEmpty(0).Max();
        return $"{prefix}{max + 1:D2}";
    }

    /// <summary>Saves pending changes; when that fails, deletes the file just written so it is not orphaned.</summary>
    private async Task SaveOrDeleteFileAsync(string? newFile)
    {
        try
        {
            await _db.SaveChangesAsync();
        }
        catch
        {
            _files.Delete(newFile);
            throw;
        }
    }

    /// <summary>Loads a tracked competition or throws NotFoundException.</summary>
    private async Task<Competition> FindAsync(long id)
        => await _db.Competitions.FirstOrDefaultAsync(c => c.Id == id)
           ?? throw new NotFoundException($"Competition {id} was not found.");

    /// <summary>GRADED competitions are read only (BR-10).</summary>
    private static void EnsureEditable(Competition c)
    {
        if (c.Status == CompetitionStatus.GRADED)
        {
            throw new BusinessException(GradedReadOnlyMessage);
        }
    }

    /// <summary>Both dates from a validated form; checked again here so no caller can skip BR-06.</summary>
    private static (DateOnly Start, DateOnly End) Dates(CompetitionFormVm form)
    {
        if (form.StartDate is not { } start || form.EndDate is not { } end)
        {
            throw new BusinessException("Please choose the start and end dates.", nameof(CompetitionFormVm.StartDate));
        }
        if (end < start)
        {
            throw new BusinessException(CompetitionFormVm.EndBeforeStartMessage, nameof(CompetitionFormVm.EndDate));
        }
        return (start, end);
    }

    /// <summary>Trims text; blank becomes null.</summary>
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
