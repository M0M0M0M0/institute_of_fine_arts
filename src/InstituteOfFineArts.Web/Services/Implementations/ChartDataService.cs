using InstituteOfFineArts.Web.Data;
using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Helpers;
using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.Services.Interfaces;
using InstituteOfFineArts.Web.ViewModels.Charts;
using Microsoft.EntityFrameworkCore;

namespace InstituteOfFineArts.Web.Services.Implementations;

/// <summary>
/// Dashboard chart data (task BE2-OPT 2; F-M15-01). Each method is one grouped AsNoTracking query; the optional
/// status filters use <see cref="CompetitionStatusResolver"/> (GRADED stored, the other statuses from the dates),
/// so the charts count the same competitions / exhibitions as the lists and reports.
/// </summary>
public class ChartDataService : IChartDataService
{
    /// <summary>Label of the submissions without an evaluation (same text as the reports).</summary>
    public const string NotGradedLabel = "Not graded";

    /// <summary>Message when the competition of the ratings chart does not exist.</summary>
    public const string CompetitionNotFoundMessage = "Competition not found.";

    /// <summary>Database access.</summary>
    private readonly AppDbContext _db;

    /// <summary>Receives the database context through Dependency Injection.</summary>
    public ChartDataService(AppDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<ChartDataVm> GetSubmissionsByCompetitionAsync(CompetitionStatus? status = null, DateOnly? today = null)
    {
        var day = today ?? DateTimeHelper.Today;

        // One row per competition (also those without entries, so the chart shows a 0 bar)
        var rows = await _db.Competitions.AsNoTracking()
            .WhereStatus(status, day)
            .OrderBy(c => c.StartDate).ThenBy(c => c.Id)
            .Select(c => new
            {
                c.Code,
                Submissions = c.Submissions.Count(),
                Evaluated = c.Submissions.Count(s => s.Evaluation != null),
            })
            .ToListAsync();

        return new ChartDataVm
        {
            Labels = rows.Select(r => r.Code).ToList(),
            Datasets =
            {
                new ChartDatasetVm { Label = "Submissions", Data = rows.Select(r => (decimal)r.Submissions).ToList() },
                new ChartDatasetVm { Label = "Evaluated", Data = rows.Select(r => (decimal)r.Evaluated).ToList() },
            },
        };
    }

    /// <inheritdoc />
    public async Task<ChartDataVm> GetRatingsAsync(long? competitionId = null, CompetitionStatus? status = null, DateOnly? today = null)
    {
        var day = today ?? DateTimeHelper.Today;

        // A chosen competition must exist (404 for a wrong id)
        if (competitionId is { } id && !await _db.Competitions.AnyAsync(c => c.Id == id))
        {
            throw new NotFoundException(CompetitionNotFoundMessage);
        }

        // Submissions of the matching competitions, grouped by rating (null = not evaluated yet)
        var competitions = _db.Competitions.AsNoTracking().WhereStatus(status, day);
        if (competitionId is { } chosen)
        {
            competitions = competitions.Where(c => c.Id == chosen);
        }
        var counts = await _db.Submissions.AsNoTracking()
            .Where(s => competitions.Any(c => c.Id == s.CompetitionId))
            .GroupBy(s => s.Evaluation == null ? (Rating?)null : s.Evaluation.Rating)
            .Select(g => new { Rating = g.Key, Count = g.Count() })
            .ToListAsync();

        // Every rating listed (0 when none) in rating order, then "Not graded"
        var ratings = Enum.GetValues<Rating>();
        var data = ratings.Select(r => (decimal)counts.Where(c => c.Rating == r).Sum(c => c.Count)).ToList();
        data.Add(counts.Where(c => c.Rating == null).Sum(c => c.Count));
        return new ChartDataVm
        {
            Labels = ratings.Select(r => r.ToString()).Append(NotGradedLabel).ToList(),
            Datasets = { new ChartDatasetVm { Label = "Submissions", Data = data } },
        };
    }

    /// <inheritdoc />
    public async Task<ChartDataVm> GetSalesByExhibitionAsync(ExhibitionStatus? status = null, DateOnly? today = null)
    {
        var day = today ?? DateTimeHelper.Today;

        // Revenue = sum of the sold prices of the SOLD entries (same rule as RPT-04 and the manager dashboard)
        var rows = await _db.Exhibitions.AsNoTracking()
            .WhereStatus(status, day)
            .OrderBy(e => e.StartDate).ThenBy(e => e.Id)
            .Select(e => new
            {
                e.Name,
                Revenue = e.Entries.Where(x => x.Status == EntryStatus.SOLD).Sum(x => x.SoldPrice ?? 0m),
                Sold = e.Entries.Count(x => x.Status == EntryStatus.SOLD),
            })
            .ToListAsync();

        return new ChartDataVm
        {
            Labels = rows.Select(r => r.Name).ToList(),
            Datasets =
            {
                new ChartDatasetVm { Label = "Revenue (USD)", Data = rows.Select(r => r.Revenue).ToList() },
                new ChartDatasetVm { Label = "Paintings sold", Data = rows.Select(r => (decimal)r.Sold).ToList() },
            },
        };
    }
}
