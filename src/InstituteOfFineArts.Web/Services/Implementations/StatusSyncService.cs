using InstituteOfFineArts.Web.Data;
using InstituteOfFineArts.Web.Helpers;
using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace InstituteOfFineArts.Web.Services.Implementations;

/// <summary>
/// Writes the date-based statuses into the database (task BE2-05, CN37, BR-07, BR-42).
/// The application always calculates statuses with <see cref="CompetitionStatusResolver"/>, so this job only keeps
/// the stored column in line for reports, SQL queries and lists that filter on it.
/// </summary>
public class StatusSyncService : IStatusSyncService
{
    /// <summary>Database access.</summary>
    private readonly AppDbContext _db;

    /// <summary>Logs the number of changed rows.</summary>
    private readonly ILogger<StatusSyncService> _logger;

    /// <summary>Receives the required services through Dependency Injection.</summary>
    public StatusSyncService(AppDbContext db, ILogger<StatusSyncService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<StatusSyncResult> SyncAsync(DateOnly? today = null, CancellationToken cancellationToken = default)
    {
        var day = today ?? DateTimeHelper.Today;

        // Competitions: GRADED is set by staff when publishing results and is never changed here
        var competitionsChanged = 0;
        var competitions = await _db.Competitions
            .Where(c => c.Status != CompetitionStatus.GRADED)
            .ToListAsync(cancellationToken);
        foreach (var competition in competitions)
        {
            var status = CompetitionStatusResolver.Resolve(competition, day);
            if (competition.Status != status)
            {
                competition.Status = status;
                competitionsChanged++;
            }
        }

        // Exhibitions: the status follows the dates only
        var exhibitionsChanged = 0;
        var exhibitions = await _db.Exhibitions.ToListAsync(cancellationToken);
        foreach (var exhibition in exhibitions)
        {
            var status = CompetitionStatusResolver.Resolve(exhibition, day);
            if (exhibition.Status != status)
            {
                exhibition.Status = status;
                exhibitionsChanged++;
            }
        }

        // Entries of finished exhibitions: accepted artworks that were not sold become UNSOLD.
        // PENDING entries are left alone on purpose: they were never reviewed, and turning them into
        // UNSOLD would claim they were exhibited; staff can still reject them after the exhibition.
        var finishedIds = exhibitions.Where(x => x.Status == ExhibitionStatus.FINISHED).Select(x => x.Id).ToList();
        var entries = await _db.ExhibitionEntries
            .Where(e => finishedIds.Contains(e.ExhibitionId) && e.Status == EntryStatus.ACCEPTED)
            .ToListAsync(cancellationToken);
        foreach (var entry in entries)
        {
            entry.Status = EntryStatus.UNSOLD;
        }

        // One SaveChanges (one transaction) for the whole run; nothing is written when nothing changed
        var result = new StatusSyncResult(competitionsChanged, exhibitionsChanged, entries.Count);
        if (result.Total > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
        }

        _logger.LogInformation(
            "Status sync on {Day}: {Competitions} competition(s), {Exhibitions} exhibition(s), {Entries} entry(ies) set to UNSOLD",
            day.ToString("dd MMM yyyy"), result.Competitions, result.Exhibitions, result.EntriesUnsold);
        return result;
    }
}
