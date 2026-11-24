using InstituteOfFineArts.Web.Data;
using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Helpers;
using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.Services.Interfaces;
using InstituteOfFineArts.Web.ViewModels.Admin.Grading;
using Microsoft.EntityFrameworkCore;

namespace InstituteOfFineArts.Web.Services.Implementations;

/// <summary>
/// Administrator – the grading library (decided 16/11/2026): the criteria competitions can pick and the rating
/// thresholds of the whole institute.
/// <list type="bullet">
/// <item>A criterion has a unique name (case-insensitive) and a description. One used by a competition is never
/// deleted, only hidden: it cannot be picked for new competitions, old score sheets keep its name.</item>
/// <item>Thresholds: the lowest average for BEST, BETTER, GOOD and MODERATE (below = NORMAL), each 0–100, strictly going
/// down. A change applies to results calculated from now on; published results keep their rating.</item>
/// </list>
/// </summary>
public class GradingCriterionService : IGradingCriterionService
{
    /// <summary>Name already used.</summary>
    public const string DuplicateNameMessage = "A criterion with this name already exists.";

    /// <summary>Name missing.</summary>
    public const string NameRequiredMessage = "Please enter the name of the criterion.";

    /// <summary>Database access.</summary>
    private readonly AppDbContext _db;

    /// <summary>Receives the database context through Dependency Injection.</summary>
    public GradingCriterionService(AppDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<GradingLibraryVm> GetLibraryAsync()
    {
        var usage = await _db.CompetitionCriteria.AsNoTracking().GroupBy(c => c.CriterionId)
            .Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count);
        var criteria = await _db.GradingCriteria.AsNoTracking().OrderByDescending(c => c.Active).ThenBy(c => c.Name).ToListAsync();
        var thresholds = await JuryResults.ThresholdsAsync(_db);
        return new GradingLibraryVm
        {
            Criteria = criteria.Select(c => new CriterionListItemVm(c.Id, c.Name, c.Description, c.Active, usage.GetValueOrDefault(c.Id))).ToList(),
            Thresholds = new ThresholdsFormVm
            {
                Best = thresholds[Rating.BEST], Better = thresholds[Rating.BETTER], Good = thresholds[Rating.GOOD], Moderate = thresholds[Rating.MODERATE],
            },
        };
    }

    /// <inheritdoc />
    public async Task<CriterionFormVm> GetForEditAsync(long id)
    {
        var c = await _db.GradingCriteria.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id)
            ?? throw new NotFoundException($"Criterion {id} was not found.");
        return new CriterionFormVm { Id = c.Id, Name = c.Name, Description = c.Description, Active = c.Active };
    }

    /// <inheritdoc />
    public async Task<long> SaveAsync(CriterionFormVm form)
    {
        var name = form.Name?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            throw new BusinessException(NameRequiredMessage, nameof(CriterionFormVm.Name));
        }
        var lower = name.ToLower();
        if (await _db.GradingCriteria.AnyAsync(c => c.Id != form.Id && c.Name.ToLower() == lower))
        {
            throw new BusinessException(DuplicateNameMessage, nameof(CriterionFormVm.Name));
        }
        var description = string.IsNullOrWhiteSpace(form.Description) ? null : form.Description.Trim();

        GradingCriterion criterion;
        if (form.Id == 0)
        {
            criterion = new GradingCriterion();
            _db.GradingCriteria.Add(criterion);
        }
        else
        {
            criterion = await _db.GradingCriteria.FirstOrDefaultAsync(c => c.Id == form.Id)
                ?? throw new NotFoundException($"Criterion {form.Id} was not found.");
        }
        criterion.Name = name;
        criterion.Description = description;
        criterion.Active = form.Active;
        await _db.SaveChangesAsync();
        return criterion.Id;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteOrHideAsync(long id)
    {
        var criterion = await _db.GradingCriteria.FirstOrDefaultAsync(c => c.Id == id)
            ?? throw new NotFoundException($"Criterion {id} was not found.");
        if (await _db.CompetitionCriteria.AnyAsync(c => c.CriterionId == id))
        {
            // Used by a competition: keep it for the score sheets, just hide it
            criterion.Active = false;
            await _db.SaveChangesAsync();
            return false;
        }
        _db.GradingCriteria.Remove(criterion);
        await _db.SaveChangesAsync();
        return true;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> SaveThresholdsAsync(ThresholdsFormVm form)
    {
        var values = new Dictionary<Rating, decimal>();
        if (form.Best is { } best) values[Rating.BEST] = best;
        if (form.Better is { } better) values[Rating.BETTER] = better;
        if (form.Good is { } good) values[Rating.GOOD] = good;
        if (form.Moderate is { } moderate) values[Rating.MODERATE] = moderate;
        if (GradingMath.CheckThresholds(values) is { } problem)
        {
            throw new BusinessException(problem);
        }

        var stored = await _db.RatingThresholds.ToDictionaryAsync(t => t.Rating);
        var before = await JuryResults.ThresholdsAsync(_db);
        var changes = new List<string>();
        foreach (var (rating, value) in values)
        {
            if (before[rating] != value)
            {
                changes.Add($"{rating}: {before[rating]:0.##} → {value:0.##}");
            }
            if (stored.TryGetValue(rating, out var row))
            {
                row.MinScore = value;
            }
            else
            {
                _db.RatingThresholds.Add(new RatingThreshold { Rating = rating, MinScore = value });
            }
        }
        await _db.SaveChangesAsync();
        return changes;
    }
}
