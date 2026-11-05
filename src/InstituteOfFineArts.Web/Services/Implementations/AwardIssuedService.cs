using InstituteOfFineArts.Web.Data;
using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Helpers;
using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.Services.Interfaces;
using InstituteOfFineArts.Web.ViewModels.Common;
using InstituteOfFineArts.Web.ViewModels.Staff.Awards;
using InstituteOfFineArts.Web.ViewModels.Staff.Submissions;
using Microsoft.EntityFrameworkCore;

namespace InstituteOfFineArts.Web.Services.Implementations;

/// <summary>
/// Awards given to submissions (F-M08-01..04, task BE1-11). Any staff member may issue, edit or revoke any award.
/// Rules: only in a CLOSED or GRADED competition (BR-32); only to an entry of that competition evaluated BEST,
/// never to a DISQUALIFIED one (BR-30); at most <c>quantity</c> times per defined award and one award per entry,
/// i.e. one per student per competition (BR-31, unique index UX_award_issued_submission); a revoke needs a reason,
/// which is logged because the table has no column for it (BR-33).
/// Decision: issuing, editing and revoking stay allowed after GRADED. BR-32 explicitly allows issuing in a GRADED
/// competition and the role matrix gives STAFF C/R/U/D on AwardIssued; only the competition, its defined awards
/// (BR-10) and the evaluations (BR-21) become read only, so the BEST entries that are eligible no longer change.
/// </summary>
public class AwardIssuedService : IAwardIssuedService
{
    /// <summary>Message when the competition is UPCOMING / ONGOING (TC-M08-02).</summary>
    public const string NotClosedMessage = "Awards can only be issued after the competition has closed.";

    /// <summary>Message when the award has been given as many times as its quantity (US-11 AC1, TC-M08-04).</summary>
    public const string NoSlotsMessage = "No award slots left.";

    /// <summary>Message when the entry was not evaluated BEST (BR-30, US-11 AC2).</summary>
    public const string NotBestMessage = "Only entries evaluated as BEST can receive an award.";

    /// <summary>Message when the entry is DISQUALIFIED (BR-30, TC-M08-03).</summary>
    public const string DisqualifiedMessage = "A disqualified entry can never receive an award.";

    /// <summary>Message when the entry / student already has an award in this competition (BR-31, US-11 AC3).</summary>
    public const string AlreadyAwardedMessage = "This student has already received an award in this competition.";

    /// <summary>Message when the chosen award does not belong to the competition.</summary>
    public const string AwardNotInCompetitionMessage = "Please choose an award of this competition.";

    /// <summary>Message when the chosen entry does not belong to the competition.</summary>
    public const string SubmissionNotInCompetitionMessage = "Please choose an entry of this competition.";

    /// <summary>Message when the chosen competition does not exist.</summary>
    public const string UnknownCompetitionMessage = "Please choose a competition.";

    /// <summary>Message when the issued date is in the future.</summary>
    public const string FutureDateMessage = "The issued date cannot be in the future.";

    /// <summary>Message when the issued date is not after the end date (entries are evaluated after the deadline).</summary>
    public const string DateBeforeEndMessage = "The issued date must be after the competition end date.";

    /// <summary>Database access.</summary>
    private readonly AppDbContext _db;

    /// <summary>Logs issued / changed / revoked awards (with the revoke reason, BR-33).</summary>
    private readonly ILogger<AwardIssuedService> _logger;

    /// <summary>Receives the required services through Dependency Injection.</summary>
    public AwardIssuedService(AppDbContext db, ILogger<AwardIssuedService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<PagedResult<AwardIssuedListItemVm>> SearchAsync(AwardIssuedSearchVm search, int pageSize = 10)
    {
        // From after To is refused like in the reports (TC-M10-03)
        DateTimeHelper.EnsureValidRange(search.From, search.To, nameof(search.From));

        var query = _db.AwardsIssued.AsNoTracking().AsQueryable();

        // Competition
        if (search.CompetitionId is { } competitionId)
        {
            query = query.Where(a => a.CompetitionAward.CompetitionId == competitionId);
        }

        // Student: part of the full name or the student code
        if (!string.IsNullOrWhiteSpace(search.Student))
        {
            var keyword = search.Student.Trim();
            query = query.Where(a => a.Submission.Student.Account.FullName.Contains(keyword)
                || a.Submission.Student.StudentCode.Contains(keyword));
        }

        // Award: part of the award name
        if (!string.IsNullOrWhiteSpace(search.Award))
        {
            var award = search.Award.Trim();
            query = query.Where(a => a.CompetitionAward.AwardName.Contains(award));
        }

        // Issued date range, both days included (issued_date is a date column, so no time part to handle)
        if (search.From is { } from)
        {
            query = query.Where(a => a.IssuedDate >= from);
        }
        if (search.To is { } to)
        {
            query = query.Where(a => a.IssuedDate <= to);
        }

        // Newest first, then by competition and rank (First Prize before Second Prize)
        var rows = query
            .OrderByDescending(a => a.IssuedDate).ThenBy(a => a.CompetitionAward.CompetitionId)
            .ThenBy(a => a.CompetitionAward.Rank).ThenBy(a => a.Id)
            .Select(a => new AwardIssuedListItemVm
            {
                Id = a.Id,
                CompetitionId = a.CompetitionAward.CompetitionId,
                CompetitionCode = a.CompetitionAward.Competition.Code,
                CompetitionTitle = a.CompetitionAward.Competition.Title,
                AwardName = a.CompetitionAward.AwardName,
                Rank = a.CompetitionAward.Rank,
                PrizeValue = a.CompetitionAward.PrizeValue,
                StudentId = a.Submission.StudentId,
                StudentName = a.Submission.Student.Account.FullName,
                StudentCode = a.Submission.Student.StudentCode,
                SubmissionId = a.SubmissionId,
                SubmissionTitle = a.Submission.Title,
                IssuedDate = a.IssuedDate,
                Remarks = a.Remarks,
                IssuedByName = a.IssuedByStaff.Account.FullName,
            });
        return await PagedResult<AwardIssuedListItemVm>.CreateAsync(rows, search.Page, pageSize);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CompetitionOptionVm>> GetCompetitionOptionsAsync()
    {
        // CLOSED or GRADED (same rule as CompetitionStatusResolver, written as a query)
        var today = DateTimeHelper.Today;
        return await _db.Competitions.AsNoTracking()
            .Where(c => c.Status == CompetitionStatus.GRADED || c.EndDate < today)
            .OrderByDescending(c => c.StartDate).ThenByDescending(c => c.Id)
            .Select(c => new CompetitionOptionVm(c.Id, c.Code, c.Title))
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<AwardIssuedFormVm> GetForCreateAsync(long? competitionId, long? competitionAwardId = null)
    {
        var form = new AwardIssuedFormVm
        {
            CompetitionId = competitionId,
            CompetitionAwardId = competitionAwardId,
            IssuedDate = DateTimeHelper.Today,
            Competitions = await GetCompetitionOptionsAsync(),
        };
        if (competitionId is { } id)
        {
            // A competition given in the URL must exist and accept awards (TC-M08-02)
            var competition = await _db.Competitions.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id)
                ?? throw new NotFoundException($"Competition {id} was not found.");
            EnsureAcceptsAwards(competition);
            await FillCompetitionAsync(form, competition, exceptIssuedId: null);
        }
        return form;
    }

    /// <inheritdoc />
    public async Task<AwardIssuedFormVm> GetForEditAsync(long id)
    {
        var issued = await _db.AwardsIssued.AsNoTracking()
            .Include(a => a.CompetitionAward).ThenInclude(a => a.Competition)
            .FirstOrDefaultAsync(a => a.Id == id)
            ?? throw new NotFoundException($"Issued award {id} was not found.");
        var form = new AwardIssuedFormVm
        {
            Id = issued.Id,
            CompetitionId = issued.CompetitionAward.CompetitionId,
            CompetitionAwardId = issued.CompetitionAwardId,
            SubmissionId = issued.SubmissionId,
            IssuedDate = issued.IssuedDate,
            Remarks = issued.Remarks,
        };
        await FillCompetitionAsync(form, issued.CompetitionAward.Competition, exceptIssuedId: id);
        return form;
    }

    /// <inheritdoc />
    public async Task FillOptionsAsync(AwardIssuedFormVm form)
    {
        if (form.Id != 0)
        {
            // Edit: the competition always comes from the stored award, never from the browser
            var current = await GetForEditAsync(form.Id);
            CopyDisplay(current, form);
            return;
        }

        // Issue: competition list, plus the options of the chosen competition when it accepts awards
        form.Competitions = await GetCompetitionOptionsAsync();
        if (form.CompetitionId is { } id
            && await _db.Competitions.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id) is { } competition
            && IsOpenForAwards(competition))
        {
            await FillCompetitionAsync(form, competition, exceptIssuedId: null);
        }
    }

    /// <inheritdoc />
    public async Task<long> CreateAsync(AwardIssuedFormVm form, long staffProfileId)
    {
        // Competition chosen in the form
        if (form.CompetitionId is not { } competitionId)
        {
            throw new BusinessException(AwardIssuedFormVm.CompetitionRequiredMessage, nameof(AwardIssuedFormVm.CompetitionId));
        }
        var competition = await _db.Competitions.AsNoTracking().FirstOrDefaultAsync(c => c.Id == competitionId)
            ?? throw new BusinessException(UnknownCompetitionMessage, nameof(AwardIssuedFormVm.CompetitionId));

        var (award, submission, date, remarks) = await ValidateAsync(competition, form, exceptIssuedId: null);

        // Valid → insert
        var issued = new AwardIssued
        {
            CompetitionAwardId = award.Id,
            SubmissionId = submission.Id,
            IssuedBy = staffProfileId,
            IssuedDate = date,
            Remarks = remarks,
        };
        _db.AwardsIssued.Add(issued);
        await SaveAsync();
        _logger.LogInformation("Award {AwardName} of competition {Code} issued to submission {SubmissionId} by staff {StaffId}",
            award.AwardName, competition.Code, submission.Id, staffProfileId);
        return issued.Id;
    }

    /// <inheritdoc />
    public async Task<long> UpdateAsync(long id, AwardIssuedFormVm form)
    {
        var issued = await _db.AwardsIssued
            .Include(a => a.CompetitionAward).ThenInclude(a => a.Competition)
            .FirstOrDefaultAsync(a => a.Id == id)
            ?? throw new NotFoundException($"Issued award {id} was not found.");

        // The competition of an issued award never changes; the same rules apply, without counting this award itself
        var competition = issued.CompetitionAward.Competition;
        var (award, submission, date, remarks) = await ValidateAsync(competition, form, exceptIssuedId: id);

        issued.CompetitionAwardId = award.Id;
        issued.SubmissionId = submission.Id;
        issued.IssuedDate = date;
        issued.Remarks = remarks;
        await SaveAsync();
        _logger.LogInformation("Issued award {IssuedId} of competition {Code} updated: award {AwardName}, submission {SubmissionId}",
            id, competition.Code, award.AwardName, submission.Id);
        return competition.Id;
    }

    /// <inheritdoc />
    public async Task<long> RevokeAsync(long id, string? reason, long staffProfileId)
    {
        var issued = await _db.AwardsIssued
            .Include(a => a.CompetitionAward).ThenInclude(a => a.Competition)
            .FirstOrDefaultAsync(a => a.Id == id)
            ?? throw new NotFoundException($"Issued award {id} was not found.");

        // BR-33: the reason is required (there is no column for it, so it goes to the log)
        var cleanReason = Clean(reason);
        if (cleanReason == null)
        {
            throw new BusinessException(RevokeAwardVm.ReasonRequiredMessage, nameof(RevokeAwardVm.Reason));
        }
        if (cleanReason.Length > RevokeAwardVm.MaxReasonLength)
        {
            throw new BusinessException(RevokeAwardVm.ReasonTooLongMessage, nameof(RevokeAwardVm.Reason));
        }

        _db.AwardsIssued.Remove(issued);
        await _db.SaveChangesAsync();
        _logger.LogInformation(
            "Award {AwardName} of competition {Code} (issued award {IssuedId}, submission {SubmissionId}) revoked by staff {StaffId}. Reason: {Reason}",
            issued.CompetitionAward.AwardName, issued.CompetitionAward.Competition.Code, id, issued.SubmissionId, staffProfileId, cleanReason);
        return issued.CompetitionAward.CompetitionId;
    }

    /// <summary>
    /// Checks every rule of an issue / edit (BR-30..32) against the database and returns the chosen award and entry,
    /// the date and the trimmed remarks. <paramref name="exceptIssuedId"/> = the award being edited (not counted).
    /// </summary>
    private async Task<(CompetitionAward Award, Submission Submission, DateOnly Date, string? Remarks)> ValidateAsync(
        Competition competition, AwardIssuedFormVm form, long? exceptIssuedId)
    {
        // BR-32: only after the deadline (CLOSED) or after publishing (GRADED)
        EnsureAcceptsAwards(competition);

        // Award: one of this competition
        if (form.CompetitionAwardId is not { } awardId)
        {
            throw new BusinessException(AwardIssuedFormVm.AwardRequiredMessage, nameof(AwardIssuedFormVm.CompetitionAwardId));
        }
        var award = await _db.CompetitionAwards.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == awardId && a.CompetitionId == competition.Id)
            ?? throw new BusinessException(AwardNotInCompetitionMessage, nameof(AwardIssuedFormVm.CompetitionAwardId));

        // BR-31: no more than quantity (the award being edited does not take an extra slot)
        var issuedCount = await _db.AwardsIssued.CountAsync(i => i.CompetitionAwardId == awardId && i.Id != exceptIssuedId);
        if (issuedCount >= award.Quantity)
        {
            throw new BusinessException(NoSlotsMessage, nameof(AwardIssuedFormVm.CompetitionAwardId));
        }

        // Entry: one of this competition, with its evaluation
        if (form.SubmissionId is not { } submissionId)
        {
            throw new BusinessException(AwardIssuedFormVm.SubmissionRequiredMessage, nameof(AwardIssuedFormVm.SubmissionId));
        }
        var submission = await _db.Submissions.AsNoTracking()
            .Include(s => s.Evaluation)
            .FirstOrDefaultAsync(s => s.Id == submissionId && s.CompetitionId == competition.Id)
            ?? throw new BusinessException(SubmissionNotInCompetitionMessage, nameof(AwardIssuedFormVm.SubmissionId));

        // BR-30: never DISQUALIFIED, only BEST
        if (submission.Evaluation?.Rating == Rating.DISQUALIFIED)
        {
            throw new BusinessException(DisqualifiedMessage, nameof(AwardIssuedFormVm.SubmissionId));
        }
        if (submission.Evaluation?.Rating != Rating.BEST)
        {
            throw new BusinessException(NotBestMessage, nameof(AwardIssuedFormVm.SubmissionId));
        }

        // BR-31: one award per entry; one entry per student per competition, so also one award per student
        var studentAwarded = await _db.AwardsIssued.AnyAsync(i => i.Id != exceptIssuedId
            && i.CompetitionAward.CompetitionId == competition.Id
            && (i.SubmissionId == submission.Id || i.Submission.StudentId == submission.StudentId));
        if (studentAwarded)
        {
            throw new BusinessException(AlreadyAwardedMessage, nameof(AwardIssuedFormVm.SubmissionId));
        }

        // Issued date: after the deadline (entries are evaluated after it) and not in the future
        if (form.IssuedDate is not { } date)
        {
            throw new BusinessException(AwardIssuedFormVm.IssuedDateRequiredMessage, nameof(AwardIssuedFormVm.IssuedDate));
        }
        if (date > DateTimeHelper.Today)
        {
            throw new BusinessException(FutureDateMessage, nameof(AwardIssuedFormVm.IssuedDate));
        }
        if (date <= competition.EndDate)
        {
            throw new BusinessException(DateBeforeEndMessage, nameof(AwardIssuedFormVm.IssuedDate));
        }

        // Remarks: optional, limited length
        var remarks = Clean(form.Remarks);
        if (remarks?.Length > AwardIssuedFormVm.MaxRemarksLength)
        {
            throw new BusinessException(AwardIssuedFormVm.RemarksTooLongMessage, nameof(AwardIssuedFormVm.Remarks));
        }
        return (award, submission, date, remarks);
    }

    /// <summary>
    /// Fills the competition data and its options: awards with a slot left and the eligible entries
    /// (BEST, no award yet). The award / entry of <paramref name="exceptIssuedId"/> stays selectable when editing.
    /// </summary>
    private async Task FillCompetitionAsync(AwardIssuedFormVm form, Competition competition, long? exceptIssuedId)
    {
        form.CompetitionId = competition.Id;
        form.CompetitionTitle = competition.Title;
        form.CompetitionCode = competition.Code;
        form.CompetitionStatus = CompetitionStatusResolver.Resolve(competition);

        // Awards: best rank first; full ones are hidden (UC-06 2a)
        var awards = await _db.CompetitionAwards.AsNoTracking()
            .Where(a => a.CompetitionId == competition.Id)
            .OrderBy(a => a.Rank).ThenBy(a => a.AwardName)
            .Select(a => new AwardOptionVm(a.Id, a.AwardName, a.Rank, a.PrizeValue, a.Quantity,
                a.IssuedAwards.Count(i => i.Id != exceptIssuedId)))
            .ToListAsync();
        form.Awards = awards.Where(a => a.SlotsLeft > 0).ToList();

        // Entries: BEST in this competition and not awarded yet (UC-06 2b), oldest entry first
        form.Submissions = await _db.Submissions.AsNoTracking()
            .Where(s => s.CompetitionId == competition.Id
                && s.Evaluation != null && s.Evaluation.Rating == Rating.BEST
                && (s.AwardIssued == null || s.AwardIssued.Id == exceptIssuedId))
            .OrderBy(s => s.SubmittedAt).ThenBy(s => s.Id)
            .Select(s => new EligibleSubmissionVm(s.Id, s.Title, s.Student.Account.FullName, s.Student.StudentCode))
            .ToListAsync();
    }

    /// <summary>Copies the display data and options from the database version of the form.</summary>
    private static void CopyDisplay(AwardIssuedFormVm from, AwardIssuedFormVm to)
    {
        to.CompetitionId = from.CompetitionId;
        to.CompetitionTitle = from.CompetitionTitle;
        to.CompetitionCode = from.CompetitionCode;
        to.CompetitionStatus = from.CompetitionStatus;
        to.Competitions = from.Competitions;
        to.Awards = from.Awards;
        to.Submissions = from.Submissions;
    }

    /// <summary>Saves; a duplicate entry caught by the unique index (two staff at the same moment) becomes BR-31.</summary>
    private async Task SaveAsync()
    {
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            throw new BusinessException(AlreadyAwardedMessage, nameof(AwardIssuedFormVm.SubmissionId));
        }
    }

    /// <summary>True when the competition is CLOSED or GRADED (BR-32).</summary>
    private static bool IsOpenForAwards(Competition competition)
        => CompetitionStatusResolver.Resolve(competition) is CompetitionStatus.CLOSED or CompetitionStatus.GRADED;

    /// <summary>BR-32 / TC-M08-02: refuses awards while the competition is UPCOMING or ONGOING.</summary>
    private static void EnsureAcceptsAwards(Competition competition)
    {
        if (!IsOpenForAwards(competition))
        {
            throw new BusinessException(NotClosedMessage);
        }
    }

    /// <summary>Trims text; blank becomes null.</summary>
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
