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
public partial class CompetitionService : ICompetitionService
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

    /// <summary>Message when the end date is extended after grading has started (BA-01: the entry could otherwise be
    /// replaced after it was evaluated, so the evaluation would describe another picture).</summary>
    public const string ExtendAfterGradingMessage = "Grading has already started, so the end date can no longer be extended.";

    /// <summary>Message when the title changes after entries exist.</summary>
    public const string TitleLockedMessage = "The title cannot be changed after students have submitted entries.";

    /// <summary>Advice shown until at least one award exists (BR-09).</summary>
    public const string NoAwardsWarning = "No awards yet. Add at least one award before the competition starts.";

    /// <summary>Warning on the details page: nobody can grade without a jury.</summary>
    public const string NoJuryWarning = "This competition has no jury yet: choose the teachers who will grade it on the edit page.";

    /// <summary>Warning on the details page: grading needs criteria.</summary>
    public const string NoCriteriaWarning = "This competition has no grading criteria yet: choose them and their weights on the edit page.";

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
        // Winners come from the jury's scores when the results are published: nobody issues an award by hand
        vm.CanIssueAwards = false;

        // Jury (with how many entries each judge graded) and criteria
        vm.Jury = await _db.CompetitionJudges.AsNoTracking().Where(j => j.CompetitionId == id)
            .OrderByDescending(j => j.IsChair).ThenBy(j => j.Judge.Account.FullName)
            .Select(j => new JudgeRowVm(j.Judge.Account.FullName, j.IsChair,
                _db.JudgeEvaluations.Count(e => e.JudgeId == j.JudgeId && e.Submission.CompetitionId == id)))
            .ToListAsync();
        vm.Criteria = await _db.CompetitionCriteria.AsNoTracking().Where(c => c.CompetitionId == id)
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Id)
            .Select(c => new CriterionRowVm(c.Criterion.Name, c.Criterion.Description, c.WeightPercent))
            .ToListAsync();
        if (status != CompetitionStatus.GRADED && vm.Jury.Count == 0)
        {
            vm.Warnings.Add(NoJuryWarning);
        }
        if (status != CompetitionStatus.GRADED && vm.Criteria.Count == 0)
        {
            vm.Warnings.Add(NoCriteriaWarning);
        }
        if (status == CompetitionStatus.CLOSED && vm.Jury.Count == 0)
        {
            vm.PublishBlockedReason = CompetitionResultService.NoJuryMessage;
        }

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
        var form = new CompetitionFormVm
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
            Awards = await AwardRowsAsync(id),
            Milestones = await MilestoneRowsAsync(id),
        };
        await LoadJuryAndCriteriaAsync(form);
        await FillJuryOptionsAsync(form);
        return form;
    }

    /// <inheritdoc />
    public Task<List<CompetitionAwardRowVm>> AwardRowsAsync(long competitionId)
        => _db.CompetitionAwards.AsNoTracking()
            .Where(a => a.CompetitionId == competitionId)
            .OrderBy(a => a.Rank).ThenBy(a => a.Id)
            .Select(a => new CompetitionAwardRowVm
            {
                Id = a.Id, AwardName = a.AwardName, Rank = a.Rank, PrizeValue = a.PrizeValue, Quantity = a.Quantity,
                PrizeDescription = a.PrizeDescription, IssuedCount = a.IssuedAwards.Count,
            })
            .ToListAsync();

    /// <inheritdoc />
    public async Task<long> CreateAsync(CompetitionFormVm form, long staffProfileId)
    {
        var (start, end) = Dates(form);

        // BR-06: a new competition must still be open on its last day
        if (end < DateTimeHelper.Today)
        {
            throw new BusinessException(EndInPastMessage, nameof(CompetitionFormVm.EndDate));
        }

        // Awards typed on the same form: checked before anything is saved, then inserted with the competition
        var awards = CheckAwardRows(form.Awards, otherNames: []);

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
        // Timeline milestones typed on the same form (information only)
        AddMilestones(competition, form.Milestones);
        // Jury and criteria typed on the same form, checked and inserted with the competition
        await AddJuryAndCriteriaAsync(competition, form);
        foreach (var a in awards)
        {
            competition.Awards.Add(new CompetitionAward
            {
                AwardName = a.Name, Rank = a.Rank, PrizeValue = a.Prize, Quantity = a.Quantity, PrizeDescription = a.Description,
            });
        }
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
    public async Task<IReadOnlyList<string>> UpdateAsync(long id, CompetitionFormVm form)
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
            // BA-01: extending reopens the competition (ONGOING) and lets students replace their pictures, so it is only
            // allowed before the first evaluation; afterwards an evaluation could describe a picture that was replaced
            if (end > c.EndDate && await _db.Evaluations.AnyAsync(e => e.Submission.CompetitionId == id))
            {
                throw new BusinessException(ExtendAfterGradingMessage, nameof(CompetitionFormVm.EndDate));
            }
        }
        if (end != c.EndDate && end < DateTimeHelper.Today)
        {
            throw new BusinessException(EndInPastMessage, nameof(CompetitionFormVm.EndDate));
        }

        // What changed, for the competition's history (audit log): "End date: 20 Nov 2026 → 25 Nov 2026"
        var changes = new List<string>();
        void Changed(string field, string? before, string? after)
        {
            if (before != after)
            {
                changes.Add($"{field}: {Short(before)} → {Short(after)}");
            }
        }
        Changed("Title", c.Title, form.Title.Trim());
        Changed("Start date", c.StartDate.ToString(ChangeDate), start.ToString(ChangeDate));
        Changed("End date", c.EndDate.ToString(ChangeDate), end.ToString(ChangeDate));
        if (c.Description != Clean(form.Description)) changes.Add("Description changed");
        if (c.Rules != Clean(form.Rules)) changes.Add("Rules changed");

        c.Title = form.Title.Trim();
        c.Description = Clean(form.Description);
        c.Rules = Clean(form.Rules);
        c.StartDate = start;
        c.EndDate = end;
        c.Status = CompetitionStatusResolver.Resolve(c);

        // Awards of the form (added, changed, removed): checked first, saved in the same SaveChanges as the competition
        changes.AddRange(await SyncAwardsAsync(c.Id, form.Awards));

        // Timeline milestones (information only, BR-29)
        changes.AddRange(await SyncMilestonesAsync(c.Id, form.Milestones, start, end));

        // Jury and criteria (criteria locked once grading started)
        var juryChanges = await SyncJuryAndCriteriaAsync(c.Id, form);
        changes.AddRange(juryChanges);

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

        if (newCover != null) changes.Add(oldCover == null ? "Cover image added" : "Cover image replaced");
        else if (oldCover != null && c.CoverImagePath == null) changes.Add("Cover image removed");

        await SaveOrDeleteFileAsync(newCover);
        if (oldCover != null && oldCover != c.CoverImagePath)
        {
            _files.Delete(oldCover);
        }
        // A jury change while grading changes which entries are complete: recalculate their results
        if (juryChanges.Count > 0 && CompetitionStatusResolver.Resolve(c) == CompetitionStatus.CLOSED)
        {
            await JuryResults.RecomputeAsync(_db, c.Id);
        }
        _logger.LogInformation("Competition {Code} updated", c.Code);
        return changes;
    }

    /// <summary>Date format of the change list.</summary>
    private const string ChangeDate = "dd MMM yyyy";

    /// <summary>Value in the change list: "–" when empty, cut to 60 characters.</summary>
    private static string Short(string? value)
        => string.IsNullOrWhiteSpace(value) ? "–" : value.Length <= 60 ? value : value[..57] + "…";

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

    /// <summary>A valid award row: position in the form (for the error key), id (0 = new) and the cleaned values.</summary>
    private sealed record AwardRow(int Index, long Id, string Name, int Rank, decimal Prize, int Quantity, string? Description);

    /// <summary>Error key of one field of an award row, e.g. "Awards[2].PrizeValue" (shown next to that cell).</summary>
    private static string AwardField(int index, string? field) => $"{nameof(CompetitionFormVm.Awards)}[{index}].{field ?? nameof(CompetitionAwardRowVm.AwardName)}";

    /// <summary>
    /// Checks the award rows that are kept (not removed, not blank) with the rules of the award form and makes their
    /// names unique, also against <paramref name="otherNames"/> (awards of the competition not on the form).
    /// </summary>
    private static List<AwardRow> CheckAwardRows(IReadOnlyList<CompetitionAwardRowVm> rows, IEnumerable<string> otherNames)
    {
        var result = new List<AwardRow>();
        var names = new HashSet<string>(otherNames, StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            if (row.Remove || row.IsBlank) continue;
            (string Name, int Rank, decimal Prize, int Quantity) v;
            try
            {
                v = CompetitionAwardService.Validate(row.AwardName, row.Rank, row.PrizeValue, row.Quantity, row.PrizeDescription);
            }
            catch (BusinessException ex)
            {
                throw new BusinessException(ex.Message, AwardField(i, ex.Field));
            }
            if (!names.Add(v.Name))
            {
                throw new BusinessException(CompetitionAwardService.DuplicateNameMessage, AwardField(i, nameof(CompetitionAwardRowVm.AwardName)));
            }
            result.Add(new AwardRow(i, row.Id, v.Name, v.Rank, v.Prize, v.Quantity, Clean(row.PrizeDescription)));
        }
        return result;
    }

    /// <summary>
    /// Applies the award rows of the edit form to the competition (saved by the caller's SaveChanges, so together with
    /// the competition): new rows are added, rows of existing awards update them, removed rows delete them.
    /// Awards that are not on the form at all are left as they are. An award already given cannot be removed and its
    /// quantity cannot go below the number given (BR-31), like on the award form.
    /// </summary>
    /// <returns>The changes for the history: "Award added: Third Prize ($100)", "Award removed: …", "Award changed: …"</returns>
    private async Task<List<string>> SyncAwardsAsync(long competitionId, List<CompetitionAwardRowVm> rows)
    {
        var changes = new List<string>();
        var existing = await _db.CompetitionAwards.Where(a => a.CompetitionId == competitionId).ToListAsync();
        var issued = await _db.AwardsIssued.Where(i => i.CompetitionAward.CompetitionId == competitionId)
            .GroupBy(i => i.CompetitionAwardId).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);
        var posted = rows.Where(r => r.Id != 0).Select(r => r.Id).ToHashSet();

        // Rows of awards of another competition (tampered form) are refused
        for (var i = 0; i < rows.Count; i++)
        {
            if (rows[i].Id != 0 && existing.All(a => a.Id != rows[i].Id))
            {
                throw new BusinessException("This award does not belong to the competition. Reload the page and try again.", AwardField(i, null));
            }
        }

        var kept = CheckAwardRows(rows, otherNames: existing.Where(a => !posted.Contains(a.Id)).Select(a => a.AwardName));

        // Removed rows
        for (var i = 0; i < rows.Count; i++)
        {
            if (!rows[i].Remove || rows[i].Id == 0) continue;
            var award = existing.Single(a => a.Id == rows[i].Id);
            if (issued.GetValueOrDefault(award.Id) > 0)
            {
                throw new BusinessException(CompetitionAwardService.IssuedDeleteMessage, AwardField(i, null));
            }
            _db.CompetitionAwards.Remove(award);
            changes.Add($"Award removed: {award.AwardName}");
        }

        // Changed and new rows
        foreach (var row in kept)
        {
            if (row.Id == 0)
            {
                _db.CompetitionAwards.Add(new CompetitionAward
                {
                    CompetitionId = competitionId, AwardName = row.Name, Rank = row.Rank, PrizeValue = row.Prize,
                    Quantity = row.Quantity, PrizeDescription = row.Description,
                });
                changes.Add($"Award added: {row.Name} (rank {row.Rank}, {row.Prize:$#,##0.##} × {row.Quantity})");
                continue;
            }
            var award = existing.Single(a => a.Id == row.Id);
            var given = issued.GetValueOrDefault(award.Id);
            if (row.Quantity < given)
            {
                throw new BusinessException(
                    $"This award has already been given {given} time(s), so the quantity cannot be less than {given}.",
                    AwardField(row.Index, nameof(CompetitionAwardRowVm.Quantity)));
            }
            var before = $"{award.AwardName}, rank {award.Rank}, {award.PrizeValue:$#,##0.##} × {award.Quantity}";
            var after = $"{row.Name}, rank {row.Rank}, {row.Prize:$#,##0.##} × {row.Quantity}";
            if (before != after || award.PrizeDescription != row.Description)
            {
                changes.Add(before == after ? $"Award changed: {row.Name} (description)" : $"Award changed: {before} → {after}");
            }
            award.AwardName = row.Name;
            award.Rank = row.Rank;
            award.PrizeValue = row.Prize;
            award.Quantity = row.Quantity;
            award.PrizeDescription = row.Description;
        }
        return changes;
    }
}
