using InstituteOfFineArts.Web.Data;
using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Helpers;
using InstituteOfFineArts.Web.Infrastructure.Api;
using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.Services.Implementations;
using InstituteOfFineArts.Web.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace InstituteOfFineArts.Web.Controllers.Api.Dto;

// JSON shapes of the exhibition endpoints (the keys are the ones read by mobile/lib/features/exhibitions/exhibitions.dart).
// The mobile app knows more than this system keeps (CANCELLED exhibitions, WITHDRAWN entries, consent and payment
// details, due dates): those fields are null / false and the matching endpoints answer NotSupported.

/// <summary>ExhibitionSummary.fromJson.</summary>
public sealed record ExhibitionSummaryDto(long Id, string Name, string Venue, string City, DateOnly StartDate, DateOnly EndDate,
    ExhibitionStatus Status, int DaysUntilStart, int EntryCount, int PendingCount);

/// <summary>ExhibitionDetail.fromJson.</summary>
public sealed record ExhibitionDetailDto(long Id, string Name, string Venue, string City, string? Organizer, string? Description,
    DateOnly StartDate, DateOnly EndDate, ExhibitionStatus Status, string? CancelReason, int EntryCount, int PendingCount,
    int AcceptedCount, int SoldCount, bool CanEdit, bool CanDelete, bool CanCancel, bool CanFinish, bool CanSubmitEntries,
    string RowVersion);

/// <summary>EligibleSubmission.fromJson.</summary>
public sealed record EligibleSubmissionDto(long Id, string Title, string StudentName, string StudentCode, string CompetitionTitle);

/// <summary>StudentEntry.fromJson.</summary>
public sealed record StudentEntryDto(long Id, long ExhibitionId, string ExhibitionName, string Venue, string City,
    DateOnly ExhibitionStartDate, DateOnly ExhibitionEndDate, ExhibitionStatus ExhibitionStatus, string SubmissionTitle,
    string CompetitionTitle, decimal AskingPrice, EntryStatus Status, string? StatusReason, decimal? SoldPrice,
    DateOnly? SoldDate, bool PaidToStudent, decimal? PaidAmount, DateOnly? PaidDate, bool CanEdit, string RowVersion);

/// <summary>EntryListItem.fromJson.</summary>
public sealed record EntryListItemDto(long Id, long ExhibitionId, string ExhibitionName, string SubmissionTitle, string StudentName,
    string StudentCode, decimal AskingPrice, EntryStatus Status, bool PaidToStudent, decimal? SoldPrice, string? StatusReason);

/// <summary>EntryDetail.fromJson.</summary>
public sealed record EntryDetailDto(long Id, long ExhibitionId, string ExhibitionName, ExhibitionStatus ExhibitionStatus,
    DateOnly ExhibitionStartDate, DateOnly ExhibitionEndDate, string SubmissionTitle, string StudentName, string StudentCode,
    string CompetitionTitle, decimal AskingPrice, string? PriceSourceNote, EntryStatus Status, string? StatusReason,
    decimal? SoldPrice, DateOnly? SoldDate, long? CustomerId, string? CustomerName, DateOnly? ConsentDate, string? ConsentMethod,
    string? ConsentNote, bool PaidToStudent, decimal? PaidAmount, DateOnly? PaidDate, string? PaymentMethod,
    string? PaymentReference, DateOnly? PayDueDate, int OverdueDays, bool CanReview, bool CanSell, bool IsLateSale,
    bool CanAdjustSale, bool AdjustLimitedToPayment, bool CanPay, string RowVersion);

/// <summary>Customer.fromJson.</summary>
public sealed record CustomerDto(long Id, string FullName, string? Phone, string? Email);

/// <summary>Read side of the exhibition endpoints (AsNoTracking queries + mapping); every write goes through the services.</summary>
public static class ExhibitionApiQueries
{
    /// <summary>Exhibition status for today, calculated from the dates (the stored value may be stale).</summary>
    public static ExhibitionStatus StatusOf(DateOnly start, DateOnly end)
        => CompetitionStatusResolver.Resolve(new Exhibition { StartDate = start, EndDate = end });

    /// <summary>Parses a query value such as "Upcoming" (case-insensitive). False when it is not a value of this system (e.g. "Cancelled").</summary>
    public static bool TryParse<T>(string? text, out T? value) where T : struct, Enum
    {
        value = null;
        if (string.IsNullOrWhiteSpace(text)) return true;
        if (Enum.TryParse<T>(text.Trim(), true, out var parsed) && Enum.IsDefined(parsed))
        {
            value = parsed;
            return true;
        }
        return false;
    }

    /// <summary>Exhibitions with entry counts; <paramref name="studentProfileId"/> limits the counts to the student's own entries.</summary>
    public static async Task<List<ExhibitionSummaryDto>> ExhibitionsAsync(AppDbContext db, string? q, ExhibitionStatus? status, long? studentProfileId)
    {
        var today = DateTimeHelper.Today;
        var query = db.Exhibitions.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var keyword = q.Trim();
            query = query.Where(e => e.Name.Contains(keyword) || e.City.Contains(keyword) || e.Venue.Contains(keyword));
        }
        query = query.WhereStatus(status, today);

        var rows = await query
            .Select(e => new
            {
                e.Id, e.Name, e.Venue, e.City, e.StartDate, e.EndDate,
                Count = e.Entries.Count(x => studentProfileId == null || x.Submission.StudentId == studentProfileId),
                Pending = e.Entries.Count(x => x.Status == EntryStatus.PENDING
                                               && (studentProfileId == null || x.Submission.StudentId == studentProfileId)),
            })
            .ToListAsync();

        var items = rows.Select(r =>
        {
            var s = StatusOf(r.StartDate, r.EndDate);
            return (Status: s, Row: r, Dto: new ExhibitionSummaryDto(r.Id, r.Name, r.Venue, r.City, r.StartDate, r.EndDate, s,
                Math.Max(0, r.StartDate.DayNumber - today.DayNumber), r.Count, r.Pending));
        });
        // Students: upcoming first, then ongoing, then finished; staff: newest start first
        var ordered = studentProfileId == null
            ? items.OrderByDescending(i => i.Row.StartDate).ThenByDescending(i => i.Row.Id)
            : items.OrderBy(i => i.Status).ThenBy(i => i.Status == ExhibitionStatus.FINISHED ? -i.Row.StartDate.DayNumber : i.Row.StartDate.DayNumber);
        return ordered.Select(i => i.Dto).ToList();
    }

    /// <summary>Exhibition detail; counts are the student's own when <paramref name="studentProfileId"/> is given.</summary>
    public static async Task<ExhibitionDetailDto> ExhibitionAsync(AppDbContext db, long id, long? studentProfileId)
    {
        var e = await db.Exhibitions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id)
                ?? throw new NotFoundException($"Exhibition {id} was not found.");
        var counts = await db.ExhibitionEntries.AsNoTracking()
            .Where(x => x.ExhibitionId == id && (studentProfileId == null || x.Submission.StudentId == studentProfileId))
            .GroupBy(x => x.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();
        int Of(EntryStatus s) => counts.FirstOrDefault(c => c.Status == s)?.Count ?? 0;
        var total = counts.Sum(c => c.Count);
        var status = StatusOf(e.StartDate, e.EndDate);
        var staff = studentProfileId == null;
        return new ExhibitionDetailDto(e.Id, e.Name, e.Venue, e.City, e.Organizer, e.Description, e.StartDate, e.EndDate, status,
            null, total, Of(EntryStatus.PENDING), Of(EntryStatus.ACCEPTED), Of(EntryStatus.SOLD),
            CanEdit: staff, CanDelete: staff && total == 0, CanCancel: false,
            // "Finish" = turn the accepted, unsold paintings of a finished exhibition into UNSOLD (the status job does the same)
            CanFinish: staff && status == ExhibitionStatus.FINISHED && Of(EntryStatus.ACCEPTED) > 0,
            CanSubmitEntries: !staff && status == ExhibitionStatus.UPCOMING,
            RowVersion: RowVersion.Of(e.CreatedAt, e.UpdatedAt));
    }

    /// <summary>Paintings that can be sent to an exhibition (BEST/BETTER of a graded competition, no active or sold entry).</summary>
    public static async Task<List<EligibleSubmissionDto>> EligibleAsync(AppDbContext db, long? studentProfileId, string? q)
    {
        var query = db.Submissions.AsNoTracking().AsQueryable();
        if (studentProfileId is { } sid) query = query.Where(s => s.StudentId == sid);
        query = query.WhereEligible(0);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var k = q.Trim();
            query = query.Where(s => s.Title.Contains(k) || s.Student.Account.FullName.Contains(k) || s.Student.StudentCode.Contains(k)
                                     || s.Competition.Title.Contains(k));
        }
        return await query
            .OrderBy(s => s.Evaluation!.Rating).ThenByDescending(s => s.Competition.EndDate).ThenBy(s => s.Title)
            .Select(s => new EligibleSubmissionDto(s.Id, s.Title, s.Student.Account.FullName, s.Student.StudentCode, s.Competition.Title))
            .ToListAsync();
    }

    /// <summary>Entries of one student (or just the entry <paramref name="id"/> when given); only the student's own.</summary>
    public static async Task<List<StudentEntryDto>> StudentEntriesAsync(AppDbContext db, long studentProfileId, long? id = null)
    {
        var query = db.ExhibitionEntries.AsNoTracking().Where(e => e.Submission.StudentId == studentProfileId);
        if (id is { } entryId) query = query.Where(e => e.Id == entryId);
        var rows = await query
            .OrderByDescending(e => e.CreatedAt).ThenByDescending(e => e.Id)
            .Select(e => new
            {
                e.Id, e.ExhibitionId, ExhibitionName = e.Exhibition.Name, e.Exhibition.Venue, e.Exhibition.City,
                e.Exhibition.StartDate, e.Exhibition.EndDate, Title = e.Submission.Title,
                Competition = e.Submission.Competition.Title, e.AskingPrice, e.Status, e.RejectReason, e.SoldPrice, e.SoldDate,
                e.PaidToStudent, e.PaidAmount, e.PaidDate, e.CreatedAt, e.UpdatedAt,
            })
            .ToListAsync();
        return rows.Select(r => new StudentEntryDto(r.Id, r.ExhibitionId, r.ExhibitionName, r.Venue, r.City, r.StartDate, r.EndDate,
            StatusOf(r.StartDate, r.EndDate), r.Title, r.Competition, r.AskingPrice, r.Status, r.RejectReason, r.SoldPrice, r.SoldDate,
            r.PaidToStudent, r.PaidAmount, r.PaidDate, r.Status == EntryStatus.PENDING, RowVersion.Of(r.CreatedAt, r.UpdatedAt))).ToList();
    }

    /// <summary>One entry of the student; 404 when unknown or someone else's.</summary>
    public static async Task<StudentEntryDto> StudentEntryAsync(AppDbContext db, long studentProfileId, long id)
        => (await StudentEntriesAsync(db, studentProfileId, id)).FirstOrDefault()
           ?? throw new NotFoundException($"Exhibition entry {id} was not found.");

    /// <summary>Staff list of entries with filters; <paramref name="q"/> matches the student's name or code, or the painting title.</summary>
    public static async Task<List<EntryListItemDto>> StaffEntriesAsync(AppDbContext db, long? exhibitionId, EntryStatus? status, bool? paid, string? q)
    {
        var query = db.ExhibitionEntries.AsNoTracking().AsQueryable();
        if (exhibitionId is { } xid) query = query.Where(e => e.ExhibitionId == xid);
        if (status is { } st) query = query.Where(e => e.Status == st);
        if (paid is { } p)
        {
            // "Not paid" = sold but still owed to the student (same as the web list)
            query = p ? query.Where(e => e.PaidToStudent) : query.Where(e => e.Status == EntryStatus.SOLD && !e.PaidToStudent);
        }
        if (!string.IsNullOrWhiteSpace(q))
        {
            var k = q.Trim();
            query = query.Where(e => e.Submission.Student.Account.FullName.Contains(k) || e.Submission.Student.StudentCode.Contains(k)
                                     || e.Submission.Title.Contains(k));
        }
        return await query
            .OrderByDescending(e => e.CreatedAt).ThenByDescending(e => e.Id)
            .Select(e => new EntryListItemDto(e.Id, e.ExhibitionId, e.Exhibition.Name, e.Submission.Title,
                e.Submission.Student.Account.FullName, e.Submission.Student.StudentCode, e.AskingPrice, e.Status, e.PaidToStudent,
                e.SoldPrice, e.RejectReason))
            .ToListAsync();
    }

    /// <summary>Staff entry detail: the service's view model (allowed actions) plus the fields the VM lacks.</summary>
    public static async Task<EntryDetailDto> StaffEntryAsync(AppDbContext db, IExhibitionEntryService entries, long id)
    {
        var vm = await entries.GetDetailsAsync(id);
        var row = await db.ExhibitionEntries.AsNoTracking()
            .Where(e => e.Id == id)
            .Select(e => new { e.CustomerId, e.CreatedAt, e.UpdatedAt })
            .FirstAsync();
        return new EntryDetailDto(vm.Id, vm.ExhibitionId, vm.ExhibitionName, vm.ExhibitionStatus, vm.StartDate, vm.EndDate, vm.Title,
            vm.StudentName, vm.StudentCode, vm.CompetitionTitle, vm.AskingPrice, vm.Notes, vm.Status, vm.RejectReason, vm.SoldPrice,
            vm.SoldDate, row.CustomerId, vm.CustomerName, null, null, null, vm.PaidToStudent, vm.PaidAmount, vm.PaidDate, null, null,
            null, 0, vm.CanAccept || vm.CanReject, vm.CanSell, vm.Status == EntryStatus.UNSOLD && vm.CanSell, false, false, vm.CanPay,
            RowVersion.Of(row.CreatedAt, row.UpdatedAt));
    }

    /// <summary>True when the client's rowVersion is the current one of the exhibition (or none was sent).</summary>
    public static async Task<bool> ExhibitionVersionOkAsync(AppDbContext db, long id, string? clientVersion)
    {
        var row = await db.Exhibitions.AsNoTracking().Where(e => e.Id == id).Select(e => new { e.CreatedAt, e.UpdatedAt }).FirstOrDefaultAsync()
                  ?? throw new NotFoundException($"Exhibition {id} was not found.");
        return RowVersion.Matches(clientVersion, row.CreatedAt, row.UpdatedAt);
    }

    /// <summary>True when the client's rowVersion is the current one of the entry (or none was sent).</summary>
    public static async Task<bool> EntryVersionOkAsync(AppDbContext db, long id, string? clientVersion)
    {
        var row = await db.ExhibitionEntries.AsNoTracking().Where(e => e.Id == id).Select(e => new { e.CreatedAt, e.UpdatedAt }).FirstOrDefaultAsync()
                  ?? throw new NotFoundException($"Exhibition entry {id} was not found.");
        return RowVersion.Matches(clientVersion, row.CreatedAt, row.UpdatedAt);
    }
}
