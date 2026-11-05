using InstituteOfFineArts.Web.Data;
using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Helpers;
using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.Services.Interfaces;
using InstituteOfFineArts.Web.ViewModels.Common;
using InstituteOfFineArts.Web.ViewModels.Staff.Exhibitions;
using Microsoft.EntityFrameworkCore;

namespace InstituteOfFineArts.Web.Services.Implementations;

/// <summary>
/// Exhibition management shared by the Staff and Manager areas (task BE2-09). The stored status column is
/// refreshed on every save; screens always show the status calculated from the dates (CompetitionStatusResolver).
/// </summary>
/// <remarks>
/// Edit rules (decision of BE2-09, based on BR-42 / BR-44): name, venue, city, organizer and description can always
/// be changed. Dates: start ≤ end; a changed end date cannot be in the past; once artworks have been sent the period
/// must still contain every sale date (BR-44: sold date within the exhibition dates); a FINISHED exhibition with
/// entries keeps its dates (they are history used by the reports).
/// </remarks>
public class ExhibitionService : IExhibitionService
{
    /// <summary>Message when an exhibition with entries is deleted (BR-42, US-12 AC2).</summary>
    public const string HasEntriesDeleteMessage = "An exhibition that already has artworks cannot be deleted.";

    /// <summary>Message when the end date is in the past.</summary>
    public const string EndInPastMessage = "The end date cannot be in the past.";

    /// <summary>Message when the dates of a finished exhibition with entries change.</summary>
    public const string DatesLockedMessage = "This exhibition has finished and has artworks, so its dates can no longer be changed.";

    /// <summary>Message when the new period no longer contains a sale date (BR-44).</summary>
    public const string SoldOutsidePeriodMessage = "Some artworks were sold on dates outside the new period. The period must include every sale date.";

    /// <summary>Message when the name, venue or city is blank.</summary>
    public const string TextRequiredMessage = "Please enter the name, venue and city.";

    /// <summary>Message when a date is missing.</summary>
    public const string DatesRequiredMessage = "Please choose the start and end dates.";

    /// <summary>Database access.</summary>
    private readonly AppDbContext _db;

    /// <summary>Logs created / changed / deleted exhibitions.</summary>
    private readonly ILogger<ExhibitionService> _logger;

    /// <summary>Receives the required services through Dependency Injection.</summary>
    public ExhibitionService(AppDbContext db, ILogger<ExhibitionService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<PagedResult<ExhibitionListItemVm>> SearchAsync(ExhibitionSearchVm search, int pageSize = 10)
    {
        var today = DateTimeHelper.Today;
        var query = Filter(_db.Exhibitions.AsNoTracking(), search, today);

        var rows = query
            .OrderByDescending(e => e.StartDate).ThenByDescending(e => e.Id)
            .Select(e => new ExhibitionListItemVm
            {
                Id = e.Id,
                Name = e.Name,
                Venue = e.Venue,
                City = e.City,
                StartDate = e.StartDate,
                EndDate = e.EndDate,
                EntryCount = e.Entries.Count,
            });

        var page = await PagedResult<ExhibitionListItemVm>.CreateAsync(rows, search.Page, pageSize);

        // Show the status for today, not the possibly stale stored value
        foreach (var item in page.Items)
        {
            item.Status = CompetitionStatusResolver.Resolve(
                new Exhibition { StartDate = item.StartDate, EndDate = item.EndDate }, today);
        }
        return page;
    }

    /// <summary>
    /// Applies the name / city / status / date-range filters. Shared with StudentExhibitionService so both lists
    /// filter the same way. The status rule is the one of CompetitionStatusResolver, written as a query for SQL Server.
    /// </summary>
    /// <param name="query">Exhibitions to filter</param>
    /// <param name="search">Filters</param>
    /// <param name="today">Current date</param>
    public static IQueryable<Exhibition> Filter(IQueryable<Exhibition> query, ExhibitionSearchVm search, DateOnly today)
    {
        // Name: part of the exhibition name
        if (!string.IsNullOrWhiteSpace(search.Keyword))
        {
            var keyword = search.Keyword.Trim();
            query = query.Where(e => e.Name.Contains(keyword));
        }

        // City: part of the city / province
        if (!string.IsNullOrWhiteSpace(search.City))
        {
            var city = search.City.Trim();
            query = query.Where(e => e.City.Contains(city));
        }

        // Status calculated from the dates (BR-42)
        query = search.Status switch
        {
            ExhibitionStatus.UPCOMING => query.Where(e => e.StartDate > today),
            ExhibitionStatus.ONGOING => query.Where(e => e.StartDate <= today && e.EndDate >= today),
            ExhibitionStatus.FINISHED => query.Where(e => e.EndDate < today),
            _ => query,
        };

        // Date range: exhibitions whose period overlaps [From, To]
        if (search.From is { } from)
        {
            query = query.Where(e => e.EndDate >= from);
        }
        if (search.To is { } to)
        {
            query = query.Where(e => e.StartDate <= to);
        }
        return query;
    }

    /// <inheritdoc />
    public async Task<ExhibitionDetailsVm> GetDetailsAsync(long id)
    {
        var e = await _db.Exhibitions
            .AsNoTracking()
            .Include(x => x.CreatedByStaff).ThenInclude(s => s.Account)
            .FirstOrDefaultAsync(x => x.Id == id)
            ?? throw new NotFoundException($"Exhibition {id} was not found.");

        // Entry counts per status in one grouped query; every status is listed, missing ones as 0
        var counts = await _db.ExhibitionEntries.AsNoTracking()
            .Where(x => x.ExhibitionId == id)
            .GroupBy(x => x.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();
        var soldTotal = await _db.ExhibitionEntries.AsNoTracking()
            .Where(x => x.ExhibitionId == id && x.Status == EntryStatus.SOLD)
            .SumAsync(x => x.SoldPrice ?? 0m);

        return new ExhibitionDetailsVm
        {
            Id = e.Id,
            Name = e.Name,
            Description = e.Description,
            Venue = e.Venue,
            City = e.City,
            Organizer = e.Organizer,
            StartDate = e.StartDate,
            EndDate = e.EndDate,
            Status = CompetitionStatusResolver.Resolve(e),
            CreatedByName = e.CreatedByStaff?.Account?.FullName ?? string.Empty,
            CreatedAt = e.CreatedAt,
            EntryCounts = Enum.GetValues<EntryStatus>()
                .Select(s => new EntryCountVm(s, counts.FirstOrDefault(c => c.Status == s)?.Count ?? 0))
                .ToList(),
            SoldTotal = soldTotal,
        };
    }

    /// <inheritdoc />
    public async Task<ExhibitionFormVm> GetForEditAsync(long id)
    {
        var e = await FindAsync(id);
        var hasEntries = await _db.ExhibitionEntries.AnyAsync(x => x.ExhibitionId == id);
        return new ExhibitionFormVm
        {
            Id = e.Id,
            Name = e.Name,
            Description = e.Description,
            Venue = e.Venue,
            City = e.City,
            Organizer = e.Organizer,
            StartDate = e.StartDate,
            EndDate = e.EndDate,
            HasEntries = hasEntries,
            DatesLocked = hasEntries && CompetitionStatusResolver.Resolve(e) == ExhibitionStatus.FINISHED,
        };
    }

    /// <inheritdoc />
    public async Task<long> CreateAsync(ExhibitionFormVm form, long staffProfileId)
    {
        CheckText(form);
        var (start, end) = Dates(form);

        // A new exhibition must still be running on its last day (it is announced to students)
        if (end < DateTimeHelper.Today)
        {
            throw new BusinessException(EndInPastMessage, nameof(ExhibitionFormVm.EndDate));
        }

        var exhibition = new Exhibition
        {
            Name = form.Name.Trim(),
            Description = Clean(form.Description),
            Venue = form.Venue.Trim(),
            City = form.City.Trim(),
            Organizer = Clean(form.Organizer),
            StartDate = start,
            EndDate = end,
            CreatedBy = staffProfileId,
        };
        exhibition.Status = CompetitionStatusResolver.Resolve(exhibition);
        _db.Exhibitions.Add(exhibition);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Exhibition {ExhibitionId} created by staff {StaffId}", exhibition.Id, staffProfileId);
        return exhibition.Id;
    }

    /// <inheritdoc />
    public async Task UpdateAsync(long id, ExhibitionFormVm form)
    {
        var e = await FindAsync(id);
        CheckText(form);
        var (start, end) = Dates(form);
        var datesChanged = start != e.StartDate || end != e.EndDate;

        if (datesChanged)
        {
            await CheckNewDatesAsync(e, start, end);
        }

        e.Name = form.Name.Trim();
        e.Description = Clean(form.Description);
        e.Venue = form.Venue.Trim();
        e.City = form.City.Trim();
        e.Organizer = Clean(form.Organizer);
        e.StartDate = start;
        e.EndDate = end;
        e.Status = CompetitionStatusResolver.Resolve(e);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Exhibition {ExhibitionId} updated", e.Id);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(long id)
    {
        var e = await FindAsync(id);

        // BR-42: only without entries (their sale and payment records must never be lost)
        if (await _db.ExhibitionEntries.AnyAsync(x => x.ExhibitionId == id))
        {
            throw new BusinessException(HasEntriesDeleteMessage);
        }

        _db.Exhibitions.Remove(e);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Exhibition {ExhibitionId} ({Name}) deleted", e.Id, e.Name);
    }

    /// <summary>Checks changed dates against the end-in-past rule and the existing entries (see the class remarks).</summary>
    private async Task CheckNewDatesAsync(Exhibition e, DateOnly start, DateOnly end)
    {
        var hasEntries = await _db.ExhibitionEntries.AnyAsync(x => x.ExhibitionId == e.Id);

        // Finished with entries: the dates are history (sales, payments, reports)
        if (hasEntries && CompetitionStatusResolver.Resolve(e) == ExhibitionStatus.FINISHED)
        {
            throw new BusinessException(DatesLockedMessage, nameof(ExhibitionFormVm.StartDate));
        }

        // Moving the end into the past would finish the exhibition by mistake
        if (end != e.EndDate && end < DateTimeHelper.Today)
        {
            throw new BusinessException(EndInPastMessage, nameof(ExhibitionFormVm.EndDate));
        }

        // BR-44: every sale date must stay inside the exhibition period
        if (hasEntries && await _db.ExhibitionEntries.AnyAsync(x => x.ExhibitionId == e.Id && x.SoldDate != null
                && (x.SoldDate < start || x.SoldDate > end)))
        {
            throw new BusinessException(SoldOutsidePeriodMessage, nameof(ExhibitionFormVm.StartDate));
        }
    }

    /// <summary>Loads a tracked exhibition or throws NotFoundException.</summary>
    private async Task<Exhibition> FindAsync(long id)
        => await _db.Exhibitions.FirstOrDefaultAsync(e => e.Id == id)
           ?? throw new NotFoundException($"Exhibition {id} was not found.");

    /// <summary>Required texts, checked again here so no caller can save a blank name / venue / city.</summary>
    private static void CheckText(ExhibitionFormVm form)
    {
        if (string.IsNullOrWhiteSpace(form.Name) || string.IsNullOrWhiteSpace(form.Venue) || string.IsNullOrWhiteSpace(form.City))
        {
            throw new BusinessException(TextRequiredMessage);
        }
    }

    /// <summary>Both dates from a validated form; checked again here so no caller can skip BR-42 (start ≤ end).</summary>
    private static (DateOnly Start, DateOnly End) Dates(ExhibitionFormVm form)
    {
        if (form.StartDate is not { } start || form.EndDate is not { } end)
        {
            throw new BusinessException(DatesRequiredMessage, nameof(ExhibitionFormVm.StartDate));
        }
        if (end < start)
        {
            throw new BusinessException(ExhibitionFormVm.EndBeforeStartMessage, nameof(ExhibitionFormVm.EndDate));
        }
        return (start, end);
    }

    /// <summary>Trims text; blank becomes null.</summary>
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
