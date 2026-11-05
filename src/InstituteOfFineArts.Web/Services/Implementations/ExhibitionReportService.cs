using InstituteOfFineArts.Web.Data;
using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Helpers;
using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.Services.Interfaces;
using InstituteOfFineArts.Web.ViewModels.Reports;
using Microsoft.EntityFrameworkCore;

namespace InstituteOfFineArts.Web.Services.Implementations;

/// <summary>
/// Financial exhibition reports (task BE2-12; F-M10-04, F-M10-05; BA §7 RPT-04, RPT-05). Every query is
/// AsNoTracking with a projection; the rows are capped at <see cref="MaxRows"/> (a report is printed whole, not paged)
/// while the totals are always calculated in the database over every matching entry.
/// Decision (RPT-04 date range): the range selects the exhibitions whose period overlaps [From, To], so a report
/// "for a period" contains every painting shown in that period, including the unsold ones that have no sale date
/// (sent / sold / unsold counts stay meaningful). Filtering by sale date is what RPT-05 does.
/// </summary>
public class ExhibitionReportService : IExhibitionReportService
{
    /// <summary>Maximum number of rows shown in a report (a warning is shown above it).</summary>
    public const int MaxRows = 1000;

    /// <summary>Statuses of a painting actually sent to an exhibition (not a proposal, not rejected).</summary>
    private static readonly EntryStatus[] SentStatuses = { EntryStatus.ACCEPTED, EntryStatus.SOLD, EntryStatus.UNSOLD };

    /// <summary>Database access.</summary>
    private readonly AppDbContext _db;

    /// <summary>Receives the database context through Dependency Injection.</summary>
    public ExhibitionReportService(AppDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<ExhibitionSalesReportVm> GetSalesReportAsync(ExhibitionSalesSearchVm search)
    {
        DateTimeHelper.EnsureValidRange(search.From, search.To, nameof(ExhibitionSalesSearchVm.From));
        var query = WhereExhibitionAndStudent(_db.ExhibitionEntries.AsNoTracking(), search.ExhibitionId, search.Student);

        // Entry status
        if (search.Status is { } status)
        {
            query = query.Where(e => e.Status == status);
        }

        // Exhibitions whose period overlaps [From, To] (decision in the class summary)
        if (search.From is { } from)
        {
            query = query.Where(e => e.Exhibition.EndDate >= from);
        }
        if (search.To is { } to)
        {
            query = query.Where(e => e.Exhibition.StartDate <= to);
        }

        // Rows: newest exhibition first, then the painting title; one extra row tells whether the cap was reached
        var rows = await query
            .OrderByDescending(e => e.Exhibition.StartDate).ThenBy(e => e.Exhibition.Name)
            .ThenBy(e => e.Submission.Title).ThenBy(e => e.Id)
            .Select(e => new ExhibitionSalesRowVm
            {
                Id = e.Id,
                ExhibitionName = e.Exhibition.Name,
                Title = e.Submission.Title,
                StudentName = e.Submission.Student.Account.FullName,
                StudentCode = e.Submission.Student.StudentCode,
                Rating = e.Submission.Evaluation != null ? e.Submission.Evaluation.Rating : null,
                AskingPrice = e.AskingPrice,
                Status = e.Status,
                SoldPrice = e.SoldPrice,
                SoldDate = e.SoldDate,
                CustomerName = e.Customer != null ? e.Customer.FullName : null,
                PaidToStudent = e.PaidToStudent,
                PaidAmount = e.PaidAmount,
                PaidDate = e.PaidDate,
            })
            .Take(MaxRows + 1)
            .ToListAsync();

        // Totals in the database: one small row per (status, paid) group instead of loading every entry
        var groups = await query
            .GroupBy(e => new { e.Status, e.PaidToStudent })
            .Select(g => new
            {
                g.Key.Status,
                g.Key.PaidToStudent,
                Count = g.Count(),
                Asking = g.Sum(e => e.AskingPrice),
                Sold = g.Sum(e => e.SoldPrice ?? 0m),
                Paid = g.Sum(e => e.PaidAmount ?? 0m),
            })
            .ToListAsync();
        var sent = groups.Where(g => SentStatuses.Contains(g.Status)).ToList();
        var sold = groups.Where(g => g.Status == EntryStatus.SOLD).ToList();
        var totals = new ExhibitionSalesTotalsVm
        {
            Entries = groups.Sum(g => g.Count),
            Sent = sent.Sum(g => g.Count),
            Sold = sold.Sum(g => g.Count),
            Unsold = sent.Where(g => g.Status != EntryStatus.SOLD).Sum(g => g.Count),
            TotalAskingPrice = sent.Sum(g => g.Asking),
            SoldAskingPrice = sold.Sum(g => g.Asking),
            TotalSoldPrice = sold.Sum(g => g.Sold),
            PaidCount = sold.Where(g => g.PaidToStudent).Sum(g => g.Count),
            PaidAmount = sold.Where(g => g.PaidToStudent).Sum(g => g.Paid),
            UnpaidCount = sold.Where(g => !g.PaidToStudent).Sum(g => g.Count),
            UnpaidAmount = sold.Where(g => !g.PaidToStudent).Sum(g => g.Sold),
        };

        return new ExhibitionSalesReportVm
        {
            Rows = rows.Take(MaxRows).ToList(),
            Truncated = rows.Count > MaxRows,
            Totals = totals,
        };
    }

    /// <inheritdoc />
    public async Task<StudentDebtReportVm> GetStudentDebtReportAsync(StudentDebtSearchVm search, DateOnly? today = null)
    {
        DateTimeHelper.EnsureValidRange(search.From, search.To, nameof(StudentDebtSearchVm.From));
        var day = today ?? DateTimeHelper.Today;

        // Sold and not paid yet = money owed to the student (BR-45)
        var query = WhereExhibitionAndStudent(_db.ExhibitionEntries.AsNoTracking(), search.ExhibitionId, search.Student)
            .Where(e => e.Status == EntryStatus.SOLD && !e.PaidToStudent && e.SoldDate != null);

        // Sale date range, both days included (sold_date is a date column, no time part)
        if (search.From is { } from)
        {
            query = query.Where(e => e.SoldDate >= from);
        }
        if (search.To is { } to)
        {
            query = query.Where(e => e.SoldDate <= to);
        }

        // Oldest debt first: it is the most urgent one
        var rows = await query
            .OrderBy(e => e.SoldDate).ThenBy(e => e.Submission.Student.StudentCode).ThenBy(e => e.Id)
            .Select(e => new StudentDebtRowVm
            {
                Id = e.Id,
                StudentName = e.Submission.Student.Account.FullName,
                StudentCode = e.Submission.Student.StudentCode,
                Title = e.Submission.Title,
                ExhibitionName = e.Exhibition.Name,
                SoldDate = e.SoldDate!.Value,
                SoldPrice = e.SoldPrice ?? 0m,
            })
            .Take(MaxRows + 1)
            .ToListAsync();

        // Days unpaid = today − sale date, calculated here (DateOnly arithmetic is not translated to SQL)
        foreach (var row in rows)
        {
            row.DaysUnpaid = day.DayNumber - row.SoldDate.DayNumber;
        }

        return new StudentDebtReportVm
        {
            Rows = rows.Take(MaxRows).ToList(),
            Truncated = rows.Count > MaxRows,
            Count = await query.CountAsync(),
            TotalUnpaid = await query.SumAsync(e => e.SoldPrice ?? 0m),
        };
    }

    /// <summary>Filters shared by RPT-04 and RPT-05: exhibition and student (part of the name or the code).</summary>
    /// <param name="query">Entries</param>
    /// <param name="exhibitionId">Exhibition id (optional)</param>
    /// <param name="student">Student keyword (optional)</param>
    private static IQueryable<ExhibitionEntry> WhereExhibitionAndStudent(IQueryable<ExhibitionEntry> query, long? exhibitionId, string? student)
    {
        // Exhibition
        if (exhibitionId is { } id)
        {
            query = query.Where(e => e.ExhibitionId == id);
        }

        // Student: part of the full name or the student code
        if (!string.IsNullOrWhiteSpace(student))
        {
            var keyword = student.Trim();
            query = query.Where(e => e.Submission.Student.Account.FullName.Contains(keyword)
                                     || e.Submission.Student.StudentCode.Contains(keyword));
        }
        return query;
    }
}
