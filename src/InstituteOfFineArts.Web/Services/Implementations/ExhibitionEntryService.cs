using InstituteOfFineArts.Web.Data;
using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Helpers;
using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.Services.Interfaces;
using InstituteOfFineArts.Web.ViewModels.Common;
using InstituteOfFineArts.Web.ViewModels.Staff.ExhibitionEntries;
using Microsoft.EntityFrameworkCore;

namespace InstituteOfFineArts.Web.Services.Implementations;

/// <summary>
/// Exhibition entries managed by staff (tasks BE1-12, BE1-13; F-M09-05, 06, 08, 09; UC-07 steps 3–5). Every change
/// follows the state diagram §4.7c and is checked here at the moment of the request:
/// PENDING → ACCEPTED (only before the exhibition starts, BR-42) or REJECTED (reason required, any time);
/// staff direct create → ACCEPTED with the student's price and a note about its source (BR-41);
/// ACCEPTED → SOLD (price &gt; 0, date within the exhibition and not in the future, customer – BR-44) or UNSOLD
/// (only after the exhibition has finished, the same rule StatusSyncService applies every night);
/// SOLD → paid to the student (full sale price, date ≥ sale date – BR-45).
/// Decisions (TC-M09-09): a recorded sale or payment cannot be undone in the application, and SOLD / UNSOLD entries
/// are never deleted, so money and customer history are never lost silently; an entry can only be deleted before its
/// exhibition starts (a mistake or a painting withdrawn before the show).
/// </summary>
public class ExhibitionEntryService : IExhibitionEntryService
{
    /// <summary>Message when accept / reject is used on an entry that is not PENDING.</summary>
    public const string NotPendingMessage = "Only a pending proposal can be accepted or rejected.";

    /// <summary>Message when a proposal is accepted after the exhibition has started (BR-42).</summary>
    public const string AcceptStartedMessage = "The exhibition has already started; a proposal can only be accepted before the exhibition starts.";

    /// <summary>Message when a sale is recorded for an entry that is not ACCEPTED (or UNSOLD after the exhibition).</summary>
    public const string SellNotAcceptedMessage = "Only an accepted painting can be marked as sold.";

    /// <summary>Message when an UNSOLD painting has since been sent to / sold at another exhibition (BR-43).</summary>
    public const string SellMovedOnMessage = "This painting has since been sent to another exhibition, so this sale cannot be recorded.";

    /// <summary>Message when a sale is recorded before the exhibition starts.</summary>
    public const string SellNotStartedMessage = "The exhibition has not started yet, so no sale can be recorded.";

    /// <summary>Message when the sale date is outside the exhibition dates (BR-44).</summary>
    public const string SoldDateRangeMessage = "The sale date must be within the exhibition dates.";

    /// <summary>Message when the sale date is in the future.</summary>
    public const string SoldDateFutureMessage = "The sale date cannot be in the future.";

    /// <summary>Message when the chosen customer does not exist (e.g. deleted meanwhile).</summary>
    public const string CustomerNotFoundMessage = "The selected customer no longer exists. Please choose another customer.";

    /// <summary>Message when UNSOLD is set for an entry that is not ACCEPTED.</summary>
    public const string UnsoldNotAcceptedMessage = "Only an accepted painting can be marked as unsold.";

    /// <summary>Message when UNSOLD is set before the exhibition has finished.</summary>
    public const string UnsoldNotFinishedMessage = "A painting can only be marked as unsold after the exhibition has finished.";

    /// <summary>Message when the payment is recorded for a painting that is not sold (US-13 AC4, TC-M09-08).</summary>
    public const string NotSoldMessage = "Not sold yet: payment to the student can only be recorded for a sold painting.";

    /// <summary>Message when the payment was already recorded.</summary>
    public const string AlreadyPaidMessage = "The payment to the student has already been recorded for this painting.";

    /// <summary>Message when the amount differs from the sale price (BR-45: the student receives the whole price).</summary>
    public const string PaidAmountMessage = "The amount paid must be the full sale price: the student receives the whole sale price (no commission).";

    /// <summary>Message when the payment date is before the sale date (BR-45).</summary>
    public const string PaidDateBeforeSaleMessage = "The payment date cannot be before the sale date.";

    /// <summary>Message when the payment date is in the future.</summary>
    public const string PaidDateFutureMessage = "The payment date cannot be in the future.";

    /// <summary>Message when an entry that may not be deleted is deleted (F-M09-09, TC-M09-09).</summary>
    public const string DeleteBlockedMessage = "This entry can no longer be deleted: only entries of an exhibition that has not started yet can be deleted, and sold or unsold paintings are always kept.";

    /// <summary>Database access.</summary>
    private readonly AppDbContext _db;

    /// <summary>Logs every state change.</summary>
    private readonly ILogger<ExhibitionEntryService> _logger;

    /// <summary>Tells the student about review, sale and payment (optional feature M12).</summary>
    private readonly INotificationService _notifications;

    /// <summary>Receives the required services through Dependency Injection.</summary>
    public ExhibitionEntryService(AppDbContext db, ILogger<ExhibitionEntryService> logger, INotificationService notifications)
    {
        _db = db;
        _logger = logger;
        _notifications = notifications;
    }

    /// <inheritdoc />
    public async Task<PagedResult<EntryListItemVm>> SearchAsync(EntrySearchVm search, int pageSize = 10)
    {
        var query = _db.ExhibitionEntries.AsNoTracking().AsQueryable();

        // Exhibition and status
        if (search.ExhibitionId is { } exhibitionId)
        {
            query = query.Where(e => e.ExhibitionId == exhibitionId);
        }
        if (search.Status is { } status)
        {
            query = query.Where(e => e.Status == status);
        }

        // Paid to the student: "not paid" means sold but still owed to the student (RPT-05)
        if (search.Paid is { } paid)
        {
            query = paid
                ? query.Where(e => e.PaidToStudent)
                : query.Where(e => e.Status == EntryStatus.SOLD && !e.PaidToStudent);
        }

        // Student: part of the full name or the student code
        if (!string.IsNullOrWhiteSpace(search.Student))
        {
            var keyword = search.Student.Trim();
            query = query.Where(e => e.Submission.Student.Account.FullName.Contains(keyword)
                                     || e.Submission.Student.StudentCode.Contains(keyword));
        }

        var rows = query
            .OrderByDescending(e => e.CreatedAt).ThenByDescending(e => e.Id)
            .Select(e => new EntryListItemVm
            {
                Id = e.Id,
                SubmissionId = e.SubmissionId,
                Title = e.Submission.Title,
                HasThumbnail = e.Submission.ThumbnailPath != null,
                StudentName = e.Submission.Student.Account.FullName,
                StudentCode = e.Submission.Student.StudentCode,
                ExhibitionId = e.ExhibitionId,
                ExhibitionName = e.Exhibition.Name,
                AskingPrice = e.AskingPrice,
                Status = e.Status,
                SoldPrice = e.SoldPrice,
                PaidToStudent = e.PaidToStudent,
                PaidDate = e.PaidDate,
            });
        return await PagedResult<EntryListItemVm>.CreateAsync(rows, search.Page, pageSize);
    }

    /// <inheritdoc />
    public async Task<List<EntryExhibitionOptionVm>> GetExhibitionOptionsAsync()
        => await _db.Exhibitions.AsNoTracking()
            .OrderByDescending(x => x.StartDate).ThenByDescending(x => x.Id)
            .Select(x => new EntryExhibitionOptionVm(x.Id, x.Name, x.StartDate, x.EndDate))
            .ToListAsync();

    /// <inheritdoc />
    public async Task<EntryCreateFormVm> GetNewFormAsync(long exhibitionId)
    {
        var exhibition = await ExhibitionEntryRules.FindUpcomingExhibitionAsync(_db, exhibitionId);

        // Every student's paintings that may be sent (BR-40, BR-43), BEST first (F-M07-06)
        var candidates = await _db.Submissions.AsNoTracking()
            .WhereEligible(exhibition.Id)
            .OrderBy(s => s.Evaluation!.Rating).ThenByDescending(s => s.Competition.EndDate).ThenBy(s => s.Title)
            .Select(s => new EntryCandidateOptionVm(s.Id, s.Title, s.Student.Account.FullName, s.Student.StudentCode,
                s.Competition.Title, s.Evaluation!.Rating))
            .ToListAsync();
        return new EntryCreateFormVm
        {
            ExhibitionId = exhibition.Id,
            ExhibitionName = exhibition.Name,
            StartDate = exhibition.StartDate,
            EndDate = exhibition.EndDate,
            Candidates = candidates,
        };
    }

    /// <inheritdoc />
    public async Task<long> CreateAsync(EntryCreateFormVm form, long staffProfileId, long accountId)
    {
        // 1. Exhibition still upcoming (BR-42)
        var exhibition = await ExhibitionEntryRules.FindUpcomingExhibitionAsync(_db, form.ExhibitionId);

        // 2. Painting: exists, BEST/BETTER of a GRADED competition, no active or sold entry (BR-40, BR-43)
        if (form.SubmissionId is not { } submissionId)
        {
            throw new BusinessException("Please choose a painting", nameof(EntryCreateFormVm.SubmissionId));
        }
        var submission = await _db.Submissions.AsNoTracking()
            .Include(s => s.Competition)
            .Include(s => s.Evaluation)
            .FirstOrDefaultAsync(s => s.Id == submissionId)
            ?? throw new BusinessException(ExhibitionEntryRules.NotEligibleMessage, nameof(EntryCreateFormVm.SubmissionId));
        await ExhibitionEntryRules.EnsureCanSendAsync(_db, submission, exhibition.Id, nameof(EntryCreateFormVm.SubmissionId));

        // 3. The price is the one the student quoted (BR-41), with a note about where it comes from
        var price = ExhibitionEntryRules.CheckAmount(form.AskingPrice, nameof(EntryCreateFormVm.AskingPrice),
            ExhibitionEntryRules.PriceRequiredMessage, ExhibitionEntryRules.PriceMessage);
        var notes = ExhibitionEntryRules.Clean(form.Notes)
                    ?? throw new BusinessException(EntryCreateFormVm.NotesRequiredMessage, nameof(EntryCreateFormVm.Notes));
        if (notes.Length > 500)
        {
            throw new BusinessException("The note must be at most 500 characters", nameof(EntryCreateFormVm.Notes));
        }

        // 4. Insert: created by staff means accepted at once (state diagram §4.7c)
        var entry = new ExhibitionEntry
        {
            ExhibitionId = exhibition.Id,
            SubmissionId = submission.Id,
            AskingPrice = price,
            Status = EntryStatus.ACCEPTED,
            Notes = notes,
            ProposedBy = accountId,
            ReviewedBy = staffProfileId,
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
                throw new BusinessException(ExhibitionEntryRules.DuplicateMessage, nameof(EntryCreateFormVm.SubmissionId));
            }
            throw;
        }

        _logger.LogInformation("Exhibition entry {EntryId} created by staff {StaffId}: submission {SubmissionId} to exhibition {ExhibitionId} at {Price}",
            entry.Id, staffProfileId, submission.Id, exhibition.Id, price);
        return entry.Id;
    }

    /// <inheritdoc />
    public async Task<EntryDetailsVm> GetDetailsAsync(long id)
    {
        var vm = await _db.ExhibitionEntries.AsNoTracking()
            .Where(e => e.Id == id)
            .Select(e => new EntryDetailsVm
            {
                Id = e.Id,
                SubmissionId = e.SubmissionId,
                Title = e.Submission.Title,
                CompetitionTitle = e.Submission.Competition.Title,
                Rating = e.Submission.Evaluation != null ? e.Submission.Evaluation.Rating : null,
                StudentName = e.Submission.Student.Account.FullName,
                StudentCode = e.Submission.Student.StudentCode,
                ExhibitionId = e.ExhibitionId,
                ExhibitionName = e.Exhibition.Name,
                City = e.Exhibition.City,
                StartDate = e.Exhibition.StartDate,
                EndDate = e.Exhibition.EndDate,
                AskingPrice = e.AskingPrice,
                Status = e.Status,
                RejectReason = e.RejectReason,
                Notes = e.Notes,
                ProposedByName = e.ProposedByAccount.FullName,
                ProposedByRole = e.ProposedByAccount.Role,
                ReviewedByName = e.ReviewedByStaff != null ? e.ReviewedByStaff.Account.FullName : null,
                CreatedAt = e.CreatedAt,
                SoldPrice = e.SoldPrice,
                SoldDate = e.SoldDate,
                CustomerName = e.Customer != null ? e.Customer.FullName : null,
                CustomerPhone = e.Customer != null ? e.Customer.Phone : null,
                CustomerEmail = e.Customer != null ? e.Customer.Email : null,
                PaidToStudent = e.PaidToStudent,
                PaidAmount = e.PaidAmount,
                PaidDate = e.PaidDate,
            })
            .FirstOrDefaultAsync()
            ?? throw new NotFoundException($"Exhibition entry {id} was not found.");

        // Allowed actions for today (the same rules as the action methods below)
        var today = DateTimeHelper.Today;
        var exhibitionStatus = CompetitionStatusResolver.Resolve(new Exhibition { StartDate = vm.StartDate, EndDate = vm.EndDate }, today);
        vm.ExhibitionStatus = exhibitionStatus;
        vm.CanAccept = vm.Status == EntryStatus.PENDING && exhibitionStatus == ExhibitionStatus.UPCOMING;
        vm.CanReject = vm.Status == EntryStatus.PENDING;
        // A sale can also be entered for an UNSOLD painting (late entry after the status job), unless it has moved on
        vm.CanSell = (vm.Status == EntryStatus.ACCEPTED && exhibitionStatus != ExhibitionStatus.UPCOMING)
                     || (vm.Status == EntryStatus.UNSOLD && !await _db.ExhibitionEntries.AnyAsync(e => e.SubmissionId == vm.SubmissionId && e.Id != vm.Id
                         && (e.Status == EntryStatus.PENDING || e.Status == EntryStatus.ACCEPTED || e.Status == EntryStatus.SOLD)));
        vm.CanMarkUnsold = vm.Status == EntryStatus.ACCEPTED && exhibitionStatus == ExhibitionStatus.FINISHED;
        vm.CanPay = vm.Status == EntryStatus.SOLD && !vm.PaidToStudent;
        vm.CanDelete = CanDelete(vm.Status, exhibitionStatus);

        // Default values of the forms: sold price = asking price (BR-44), sale date = today or the last exhibition day,
        // amount paid = sold price (BR-45), payment date = today
        if (vm.CanSell)
        {
            vm.Customers = await _db.Customers.AsNoTracking()
                .OrderBy(c => c.FullName).ThenBy(c => c.Id)
                .Select(c => new EntryCustomerOptionVm(c.Id, c.FullName, c.Phone, c.Email))
                .ToListAsync();
            vm.SoldForm = new SoldFormVm { SoldPrice = vm.AskingPrice, SoldDate = today < vm.EndDate ? today : vm.EndDate };
        }
        if (vm.CanPay)
        {
            vm.PaidForm = new PaidFormVm { PaidAmount = vm.SoldPrice, PaidDate = today };
        }
        return vm;
    }

    /// <inheritdoc />
    public async Task AcceptAsync(long id, long staffProfileId)
    {
        var entry = await FindAsync(id);
        EnsureStatus(entry, EntryStatus.PENDING, NotPendingMessage);

        // BR-42: paintings are only sent to exhibitions that have not started
        if (CompetitionStatusResolver.Resolve(entry.Exhibition) != ExhibitionStatus.UPCOMING)
        {
            throw new BusinessException(AcceptStartedMessage);
        }

        entry.Status = EntryStatus.ACCEPTED;
        entry.ReviewedBy = staffProfileId;
        await _db.SaveChangesAsync();
        _logger.LogInformation("Exhibition entry {EntryId} accepted by staff {StaffId}", entry.Id, staffProfileId);

        // M12: the student is told in the application
        await _notifications.EntryReviewedAsync(entry.Id);
    }

    /// <inheritdoc />
    public async Task RejectAsync(long id, RejectFormVm form, long staffProfileId)
    {
        var entry = await FindAsync(id);
        EnsureStatus(entry, EntryStatus.PENDING, NotPendingMessage);

        // The reason is required (CK_entry_reject) and shown to the student
        var reason = ExhibitionEntryRules.Clean(form.Reason)
                     ?? throw new BusinessException(RejectFormVm.ReasonRequiredMessage, nameof(RejectFormVm.Reason));
        if (reason.Length > 500)
        {
            throw new BusinessException("The reason must be at most 500 characters", nameof(RejectFormVm.Reason));
        }

        entry.Status = EntryStatus.REJECTED;
        entry.RejectReason = reason;
        entry.ReviewedBy = staffProfileId;
        await _db.SaveChangesAsync();
        _logger.LogInformation("Exhibition entry {EntryId} rejected by staff {StaffId}", entry.Id, staffProfileId);

        // M12: the student is told in the application, with the reason
        await _notifications.EntryReviewedAsync(entry.Id);
    }

    /// <inheritdoc />
    public async Task MarkSoldAsync(long id, SoldFormVm form, long staffProfileId)
    {
        var entry = await FindAsync(id);

        // ACCEPTED, or UNSOLD: the status job turns accepted paintings into UNSOLD as soon as the exhibition ends,
        // so a sale made on the last day and entered the next morning must still be recordable
        if (entry.Status is not (EntryStatus.ACCEPTED or EntryStatus.UNSOLD))
        {
            throw new BusinessException(SellNotAcceptedMessage);
        }
        if (entry.Status == EntryStatus.UNSOLD && await IsInAnotherExhibitionAsync(entry))
        {
            throw new BusinessException(SellMovedOnMessage);
        }

        // The painting can only be sold at the exhibition, i.e. once it has started
        var today = DateTimeHelper.Today;
        if (CompetitionStatusResolver.Resolve(entry.Exhibition, today) == ExhibitionStatus.UPCOMING)
        {
            throw new BusinessException(SellNotStartedMessage);
        }

        // BR-44: sold price > 0 (the real price, may differ from the asking price)
        var price = ExhibitionEntryRules.CheckAmount(form.SoldPrice, nameof(SoldFormVm.SoldPrice),
            SoldFormVm.PriceRequiredMessage, SoldFormVm.PriceMessage);

        // BR-44: sale date within the exhibition dates and not in the future
        if (form.SoldDate is not { } soldDate)
        {
            throw new BusinessException(SoldFormVm.DateRequiredMessage, nameof(SoldFormVm.SoldDate));
        }
        if (soldDate < entry.Exhibition.StartDate || soldDate > entry.Exhibition.EndDate)
        {
            throw new BusinessException(SoldDateRangeMessage, nameof(SoldFormVm.SoldDate));
        }
        if (soldDate > today)
        {
            throw new BusinessException(SoldDateFutureMessage, nameof(SoldFormVm.SoldDate));
        }

        // BR-44, US-13 AC3: a customer is required and must exist
        if (form.CustomerId is not { } customerId)
        {
            throw new BusinessException(SoldFormVm.CustomerRequiredMessage, nameof(SoldFormVm.CustomerId));
        }
        if (!await _db.Customers.AnyAsync(c => c.Id == customerId))
        {
            throw new BusinessException(CustomerNotFoundMessage, nameof(SoldFormVm.CustomerId));
        }

        entry.Status = EntryStatus.SOLD;
        entry.SoldPrice = price;
        entry.SoldDate = soldDate;
        entry.CustomerId = customerId;
        entry.ReviewedBy = staffProfileId;
        await _db.SaveChangesAsync();
        _logger.LogInformation("Exhibition entry {EntryId} sold for {Price} on {Date} to customer {CustomerId}, recorded by staff {StaffId}",
            entry.Id, price, soldDate.ToString("dd MMM yyyy"), customerId, staffProfileId);

        // M12: the student is told (in-app + e-mail; never fails the save)
        await _notifications.EntrySoldAsync(entry.Id);
    }

    /// <inheritdoc />
    public async Task MarkUnsoldAsync(long id, long staffProfileId)
    {
        var entry = await FindAsync(id);
        EnsureStatus(entry, EntryStatus.ACCEPTED, UnsoldNotAcceptedMessage);

        // Same rule as StatusSyncService: an accepted painting is unsold only once the exhibition has finished
        if (CompetitionStatusResolver.Resolve(entry.Exhibition) != ExhibitionStatus.FINISHED)
        {
            throw new BusinessException(UnsoldNotFinishedMessage);
        }

        entry.Status = EntryStatus.UNSOLD;
        entry.ReviewedBy = staffProfileId;
        await _db.SaveChangesAsync();
        _logger.LogInformation("Exhibition entry {EntryId} marked unsold by staff {StaffId}", entry.Id, staffProfileId);
    }

    /// <inheritdoc />
    public async Task MarkPaidAsync(long id, PaidFormVm form, long staffProfileId)
    {
        var entry = await FindAsync(id);

        // US-13 AC4, TC-M09-08: only a sold painting can be paid, and only once
        EnsureStatus(entry, EntryStatus.SOLD, NotSoldMessage);
        if (entry.PaidToStudent)
        {
            throw new BusinessException(AlreadyPaidMessage);
        }

        // BR-45: the whole sale price goes to the student
        var amount = ExhibitionEntryRules.CheckAmount(form.PaidAmount, nameof(PaidFormVm.PaidAmount),
            PaidFormVm.AmountRequiredMessage, PaidFormVm.AmountMessage);
        if (amount != entry.SoldPrice)
        {
            throw new BusinessException(PaidAmountMessage, nameof(PaidFormVm.PaidAmount));
        }

        // BR-45: paid on or after the sale date, not in the future
        if (form.PaidDate is not { } paidDate)
        {
            throw new BusinessException(PaidFormVm.DateRequiredMessage, nameof(PaidFormVm.PaidDate));
        }
        if (entry.SoldDate is { } soldDate && paidDate < soldDate)
        {
            throw new BusinessException(PaidDateBeforeSaleMessage, nameof(PaidFormVm.PaidDate));
        }
        if (paidDate > DateTimeHelper.Today)
        {
            throw new BusinessException(PaidDateFutureMessage, nameof(PaidFormVm.PaidDate));
        }

        entry.PaidToStudent = true;
        entry.PaidAmount = amount;
        entry.PaidDate = paidDate;
        entry.ReviewedBy = staffProfileId;
        await _db.SaveChangesAsync();
        _logger.LogInformation("Exhibition entry {EntryId}: {Amount} paid to the student on {Date}, recorded by staff {StaffId}",
            entry.Id, amount, paidDate.ToString("dd MMM yyyy"), staffProfileId);

        // M12: the student is told in the application
        await _notifications.EntryPaidAsync(entry.Id);
    }

    /// <inheritdoc />
    public async Task<long> DeleteAsync(long id)
    {
        var entry = await FindAsync(id);
        if (!CanDelete(entry.Status, CompetitionStatusResolver.Resolve(entry.Exhibition)))
        {
            throw new BusinessException(DeleteBlockedMessage);
        }

        _db.ExhibitionEntries.Remove(entry);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Exhibition entry {EntryId} ({Status}) deleted from exhibition {ExhibitionId}",
            entry.Id, entry.Status, entry.ExhibitionId);
        return entry.ExhibitionId;
    }

    /// <summary>
    /// Delete rule (F-M09-09, TC-M09-09): before the exhibition starts any PENDING / ACCEPTED / REJECTED entry may be
    /// removed; SOLD and UNSOLD entries are history (money, customer) and are never deleted.
    /// </summary>
    private static bool CanDelete(EntryStatus status, ExhibitionStatus exhibitionStatus)
        => status is not (EntryStatus.SOLD or EntryStatus.UNSOLD) && exhibitionStatus == ExhibitionStatus.UPCOMING;

    /// <summary>Loads a tracked entry with its exhibition or throws NotFoundException.</summary>
    private async Task<ExhibitionEntry> FindAsync(long id)
        => await _db.ExhibitionEntries.Include(e => e.Exhibition).FirstOrDefaultAsync(e => e.Id == id)
           ?? throw new NotFoundException($"Exhibition entry {id} was not found.");

    /// <summary>Throws BusinessException with <paramref name="message"/> when the entry is not in <paramref name="expected"/>.</summary>
    private static void EnsureStatus(ExhibitionEntry entry, EntryStatus expected, string message)
    {
        if (entry.Status != expected)
        {
            throw new BusinessException(message);
        }
    }

    /// <summary>
    /// True when the painting of an UNSOLD entry has since been sent to another exhibition (PENDING / ACCEPTED) or sold
    /// there (BR-43); the old unsold entry can then no longer become a sale.
    /// </summary>
    /// <param name="entry">The unsold entry</param>
    private Task<bool> IsInAnotherExhibitionAsync(ExhibitionEntry entry)
        => _db.ExhibitionEntries.AnyAsync(e => e.SubmissionId == entry.SubmissionId && e.Id != entry.Id
            && (e.Status == EntryStatus.PENDING || e.Status == EntryStatus.ACCEPTED || e.Status == EntryStatus.SOLD));
}
