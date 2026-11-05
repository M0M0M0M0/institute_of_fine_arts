using InstituteOfFineArts.Web.Data;
using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Helpers;
using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.Services.Interfaces;
using InstituteOfFineArts.Web.ViewModels.Common;
using InstituteOfFineArts.Web.ViewModels.Student.ExhibitionEntries;
using Microsoft.EntityFrameworkCore;

namespace InstituteOfFineArts.Web.Services.Implementations;

/// <summary>
/// A student's own exhibition entries (task BE1-12; F-M09-04, F-M09-10, F-M09-12; UC-07 steps 1, 2 and 6).
/// The student proposes one of their own BEST/BETTER paintings of a GRADED competition (BR-40) to an UPCOMING
/// exhibition (BR-42) at their own asking price (BR-41); the entry starts PENDING. While it is PENDING the student
/// may change the price or withdraw it; after that every change is made by staff (BR-46). Other students' entries
/// look exactly like unknown ids (404).
/// </summary>
public class StudentExhibitionEntryService : IStudentExhibitionEntryService
{
    /// <summary>Message when the price is changed or the proposal withdrawn after it was reviewed (BR-46).</summary>
    public const string NotPendingMessage = "This entry has already been reviewed by staff; only a pending proposal can be changed or withdrawn.";

    /// <summary>Message when the chosen painting is not one of the student's own paintings.</summary>
    public const string NotOwnMessage = "Please choose one of your own paintings";

    /// <summary>Database access.</summary>
    private readonly AppDbContext _db;

    /// <summary>Logs proposed / changed / withdrawn entries.</summary>
    private readonly ILogger<StudentExhibitionEntryService> _logger;

    /// <summary>Receives the required services through Dependency Injection.</summary>
    public StudentExhibitionEntryService(AppDbContext db, ILogger<StudentExhibitionEntryService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<PagedResult<MyEntryListItemVm>> SearchMineAsync(long studentProfileId, MyEntrySearchVm search, int pageSize = 10)
    {
        // Only entries of the signed-in student's own paintings
        var query = _db.ExhibitionEntries.AsNoTracking().Where(e => e.Submission.StudentId == studentProfileId);

        // Status and exhibition filters
        if (search.Status is { } status)
        {
            query = query.Where(e => e.Status == status);
        }
        if (search.ExhibitionId is { } exhibitionId)
        {
            query = query.Where(e => e.ExhibitionId == exhibitionId);
        }

        var rows = query
            .OrderByDescending(e => e.CreatedAt).ThenByDescending(e => e.Id)
            .Select(e => new MyEntryListItemVm
            {
                Id = e.Id,
                SubmissionId = e.SubmissionId,
                Title = e.Submission.Title,
                HasThumbnail = e.Submission.ThumbnailPath != null,
                ExhibitionId = e.ExhibitionId,
                ExhibitionName = e.Exhibition.Name,
                StartDate = e.Exhibition.StartDate,
                EndDate = e.Exhibition.EndDate,
                AskingPrice = e.AskingPrice,
                Status = e.Status,
                SoldPrice = e.SoldPrice,
                PaidToStudent = e.PaidToStudent,
                PaidDate = e.PaidDate,
            });
        return await PagedResult<MyEntryListItemVm>.CreateAsync(rows, search.Page, pageSize);
    }

    /// <inheritdoc />
    public async Task<List<MyExhibitionOptionVm>> GetMyExhibitionsAsync(long studentProfileId)
        => await _db.Exhibitions.AsNoTracking()
            .Where(x => x.Entries.Any(e => e.Submission.StudentId == studentProfileId))
            .OrderByDescending(x => x.StartDate).ThenByDescending(x => x.Id)
            .Select(x => new MyExhibitionOptionVm(x.Id, x.Name))
            .ToListAsync();

    /// <inheritdoc />
    public async Task<ProposeEntryFormVm> GetNewFormAsync(long exhibitionId, long studentProfileId)
    {
        var exhibition = await ExhibitionEntryRules.FindUpcomingExhibitionAsync(_db, exhibitionId);
        return new ProposeEntryFormVm
        {
            ExhibitionId = exhibition.Id,
            ExhibitionName = exhibition.Name,
            City = exhibition.City,
            StartDate = exhibition.StartDate,
            EndDate = exhibition.EndDate,
            Candidates = await GetCandidatesAsync(exhibition.Id, studentProfileId),
        };
    }

    /// <inheritdoc />
    public async Task<long> CreateAsync(ProposeEntryFormVm form, long studentProfileId, long accountId)
    {
        // 1. Exhibition still upcoming at the moment of the request (BR-42)
        var exhibition = await ExhibitionEntryRules.FindUpcomingExhibitionAsync(_db, form.ExhibitionId);

        // 2. Painting: required, own (another student's id is treated like an unknown one), BR-40 + BR-43
        if (form.SubmissionId is not { } submissionId)
        {
            throw new BusinessException("Please choose a painting", nameof(ProposeEntryFormVm.SubmissionId));
        }
        var submission = await _db.Submissions.AsNoTracking()
            .Include(s => s.Competition)
            .Include(s => s.Evaluation)
            .FirstOrDefaultAsync(s => s.Id == submissionId && s.StudentId == studentProfileId)
            ?? throw new BusinessException(NotOwnMessage, nameof(ProposeEntryFormVm.SubmissionId));
        await ExhibitionEntryRules.EnsureCanSendAsync(_db, submission, exhibition.Id, nameof(ProposeEntryFormVm.SubmissionId));

        // 3. Asking price decided by the student, > 0 USD (BR-41)
        var price = ExhibitionEntryRules.CheckAmount(form.AskingPrice, nameof(ProposeEntryFormVm.AskingPrice),
            ExhibitionEntryRules.PriceRequiredMessage, ExhibitionEntryRules.PriceMessage);

        // 4. Insert the PENDING proposal (state diagram §4.7c)
        var entry = new ExhibitionEntry
        {
            ExhibitionId = exhibition.Id,
            SubmissionId = submission.Id,
            AskingPrice = price,
            Status = EntryStatus.PENDING,
            ProposedBy = accountId,
        };
        _db.ExhibitionEntries.Add(entry);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            _db.Entry(entry).State = EntityState.Detached;

            // Two requests at the same time: the unique index UX_entry_active_submission (BR-43) stopped the second one
            if (await ExhibitionEntryRules.HasActiveEntryAsync(_db, submission.Id))
            {
                throw new BusinessException(ExhibitionEntryRules.DuplicateMessage, nameof(ProposeEntryFormVm.SubmissionId));
            }
            throw;
        }

        _logger.LogInformation("Exhibition entry {EntryId} proposed by student {StudentId}: submission {SubmissionId} to exhibition {ExhibitionId} at {Price}",
            entry.Id, studentProfileId, submission.Id, exhibition.Id, price);
        return entry.Id;
    }

    /// <inheritdoc />
    public async Task<MyEntryDetailsVm> GetDetailsAsync(long id, long studentProfileId)
    {
        // Owner only: another student's entry looks exactly like an unknown id
        var vm = await _db.ExhibitionEntries.AsNoTracking()
            .Where(e => e.Id == id && e.Submission.StudentId == studentProfileId)
            .Select(e => new MyEntryDetailsVm
            {
                Id = e.Id,
                SubmissionId = e.SubmissionId,
                Title = e.Submission.Title,
                CompetitionTitle = e.Submission.Competition.Title,
                Rating = e.Submission.Evaluation != null ? e.Submission.Evaluation.Rating : null,
                ExhibitionId = e.ExhibitionId,
                ExhibitionName = e.Exhibition.Name,
                Venue = e.Exhibition.Venue,
                City = e.Exhibition.City,
                StartDate = e.Exhibition.StartDate,
                EndDate = e.Exhibition.EndDate,
                AskingPrice = e.AskingPrice,
                Status = e.Status,
                RejectReason = e.RejectReason,
                SoldPrice = e.SoldPrice,
                SoldDate = e.SoldDate,
                PaidToStudent = e.PaidToStudent,
                PaidAmount = e.PaidAmount,
                PaidDate = e.PaidDate,
                CreatedAt = e.CreatedAt,
                CreatedByStaff = e.ProposedByAccount.Role != Role.STUDENT,
            })
            .FirstOrDefaultAsync()
            ?? throw new NotFoundException($"Exhibition entry {id} was not found.");

        // Exhibition status for today (the stored value may be stale)
        vm.ExhibitionStatus = CompetitionStatusResolver.Resolve(new Exhibition { StartDate = vm.StartDate, EndDate = vm.EndDate });
        return vm;
    }

    /// <inheritdoc />
    public async Task<EditPriceFormVm> GetForEditAsync(long id, long studentProfileId)
    {
        var entry = await FindPendingAsync(id, studentProfileId);
        return new EditPriceFormVm
        {
            Id = entry.Id,
            Title = entry.Submission.Title,
            ExhibitionName = entry.Exhibition.Name,
            AskingPrice = entry.AskingPrice,
        };
    }

    /// <inheritdoc />
    public async Task UpdatePriceAsync(long id, EditPriceFormVm form, long studentProfileId)
    {
        // Owner + still PENDING at the moment of the request (BR-46), price > 0 (BR-41)
        var entry = await FindPendingAsync(id, studentProfileId);
        var price = ExhibitionEntryRules.CheckAmount(form.AskingPrice, nameof(EditPriceFormVm.AskingPrice),
            ExhibitionEntryRules.PriceRequiredMessage, ExhibitionEntryRules.PriceMessage);

        var old = entry.AskingPrice;
        entry.AskingPrice = price;
        await _db.SaveChangesAsync();
        _logger.LogInformation("Exhibition entry {EntryId}: asking price changed by student {StudentId} from {Old} to {New}",
            entry.Id, studentProfileId, old, price);
    }

    /// <inheritdoc />
    public async Task WithdrawAsync(long id, long studentProfileId)
    {
        // Withdrawing removes the proposal (state diagram §4.7c: PENDING → [deleted])
        var entry = await FindPendingAsync(id, studentProfileId);
        _db.ExhibitionEntries.Remove(entry);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Exhibition entry {EntryId} withdrawn by student {StudentId}", entry.Id, studentProfileId);
    }

    /// <summary>
    /// Paintings of this student that may be proposed to the exhibition (BR-40, BR-43): BEST first, then BETTER,
    /// newest competition first. A GOOD painting never appears (US-13 AC2).
    /// </summary>
    private async Task<List<MyCandidateOptionVm>> GetCandidatesAsync(long exhibitionId, long studentProfileId)
        => await _db.Submissions.AsNoTracking()
            .Where(s => s.StudentId == studentProfileId)
            .WhereEligible(exhibitionId)
            .OrderBy(s => s.Evaluation!.Rating).ThenByDescending(s => s.Competition.EndDate).ThenBy(s => s.Title)
            .Select(s => new MyCandidateOptionVm(s.Id, s.Title, s.Competition.Title, s.Evaluation!.Rating))
            .ToListAsync();

    /// <summary>
    /// Loads a tracked own entry (with painting and exhibition) and checks it is still PENDING (BR-46).
    /// Other students' entries throw NotFoundException.
    /// </summary>
    private async Task<ExhibitionEntry> FindPendingAsync(long id, long studentProfileId)
    {
        var entry = await _db.ExhibitionEntries
            .Include(e => e.Submission)
            .Include(e => e.Exhibition)
            .FirstOrDefaultAsync(e => e.Id == id && e.Submission.StudentId == studentProfileId)
            ?? throw new NotFoundException($"Exhibition entry {id} was not found.");
        if (entry.Status != EntryStatus.PENDING)
        {
            throw new BusinessException(NotPendingMessage);
        }
        return entry;
    }
}
