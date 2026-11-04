using InstituteOfFineArts.Web.Data;
using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.Services.Interfaces;
using InstituteOfFineArts.Web.ViewModels.Staff.Competitions;
using Microsoft.EntityFrameworkCore;

namespace InstituteOfFineArts.Web.Services.Implementations;

/// <summary>
/// Defined awards of a competition (F-M05-06, task BE1-06). Any staff member may manage the awards of any
/// competition. Rules: rank ≥ 1, prize ≥ 0 USD, quantity ≥ 1 (BR-09); the name is unique within the competition
/// (index UX_competition_award_name, compared here without case); GRADED competitions are read only (BR-10);
/// an award that has been issued cannot be deleted and its quantity cannot go below the issued count (BR-31).
/// </summary>
public class CompetitionAwardService : ICompetitionAwardService
{
    /// <summary>Message when the competition results are published.</summary>
    public const string GradedReadOnlyMessage = "Results have been published; the awards of this competition can no longer be changed.";

    /// <summary>Message when the award name already exists in the competition.</summary>
    public const string DuplicateNameMessage = "This competition already has an award with this name.";

    /// <summary>Message when an issued award is deleted.</summary>
    public const string IssuedDeleteMessage = "This award has already been given to a submission and cannot be deleted.";

    /// <summary>Message when the prize has more than 2 decimal places (column decimal(18,2)).</summary>
    public const string PrizeDecimalsMessage = "Prize value can have at most 2 decimal places";

    /// <summary>Largest prize the column decimal(18,2) can hold.</summary>
    private const decimal MaxPrize = 9999999999999999.99m;

    /// <summary>Database access.</summary>
    private readonly AppDbContext _db;

    /// <summary>Logs added / changed / deleted awards.</summary>
    private readonly ILogger<CompetitionAwardService> _logger;

    /// <summary>Receives the required services through Dependency Injection.</summary>
    public CompetitionAwardService(AppDbContext db, ILogger<CompetitionAwardService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<CompetitionAwardFormVm> GetForCreateAsync(long competitionId)
    {
        var competition = await FindCompetitionAsync(competitionId);
        EnsureEditable(competition);

        // New award: suggest the next rank after the existing ones
        var nextRank = await _db.CompetitionAwards.Where(a => a.CompetitionId == competitionId)
            .Select(a => (int?)a.Rank).MaxAsync() ?? 0;
        return new CompetitionAwardFormVm
        {
            CompetitionId = competition.Id,
            CompetitionTitle = competition.Title,
            CompetitionCode = competition.Code,
            Rank = nextRank + 1,
            Quantity = 1,
        };
    }

    /// <inheritdoc />
    public async Task<CompetitionAwardFormVm> GetForEditAsync(long competitionId, long awardId)
    {
        var competition = await FindCompetitionAsync(competitionId);
        EnsureEditable(competition);
        var award = await FindAwardAsync(competitionId, awardId);
        return new CompetitionAwardFormVm
        {
            Id = award.Id,
            CompetitionId = competition.Id,
            CompetitionTitle = competition.Title,
            CompetitionCode = competition.Code,
            AwardName = award.AwardName,
            Rank = award.Rank,
            PrizeValue = award.PrizeValue,
            Quantity = award.Quantity,
            PrizeDescription = award.PrizeDescription,
            IssuedCount = await IssuedCountAsync(award.Id),
        };
    }

    /// <inheritdoc />
    public async Task<long> CreateAsync(long competitionId, CompetitionAwardFormVm form)
    {
        var competition = await FindCompetitionAsync(competitionId);
        EnsureEditable(competition);
        var (name, rank, prize, quantity) = Validate(form);
        await EnsureUniqueNameAsync(competitionId, name, exceptAwardId: null);

        // Valid → insert
        var award = new CompetitionAward
        {
            CompetitionId = competitionId,
            AwardName = name,
            Rank = rank,
            PrizeValue = prize,
            Quantity = quantity,
            PrizeDescription = Clean(form.PrizeDescription),
        };
        _db.CompetitionAwards.Add(award);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Award {Name} added to competition {Code}", name, competition.Code);
        return award.Id;
    }

    /// <inheritdoc />
    public async Task UpdateAsync(long competitionId, long awardId, CompetitionAwardFormVm form)
    {
        var competition = await FindCompetitionAsync(competitionId);
        EnsureEditable(competition);
        var award = await FindAwardAsync(competitionId, awardId);
        var (name, rank, prize, quantity) = Validate(form);
        await EnsureUniqueNameAsync(competitionId, name, exceptAwardId: awardId);

        // BR-31: the awards already given must still fit in the quantity
        var issued = await IssuedCountAsync(awardId);
        if (quantity < issued)
        {
            throw new BusinessException(
                $"This award has already been given {issued} time(s), so the quantity cannot be less than {issued}.",
                nameof(CompetitionAwardFormVm.Quantity));
        }

        // Valid → save
        award.AwardName = name;
        award.Rank = rank;
        award.PrizeValue = prize;
        award.Quantity = quantity;
        award.PrizeDescription = Clean(form.PrizeDescription);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Award {AwardId} of competition {Code} updated", awardId, competition.Code);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(long competitionId, long awardId)
    {
        var competition = await FindCompetitionAsync(competitionId);
        EnsureEditable(competition);
        var award = await FindAwardAsync(competitionId, awardId);

        // An issued award is part of the results: it must never disappear
        if (await IssuedCountAsync(awardId) > 0)
        {
            throw new BusinessException(IssuedDeleteMessage);
        }

        _db.CompetitionAwards.Remove(award);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Award {Name} deleted from competition {Code}", award.AwardName, competition.Code);
    }

    /// <summary>Checks the form values again (no caller can skip BR-09) and returns them trimmed / not null.</summary>
    private static (string Name, int Rank, decimal Prize, int Quantity) Validate(CompetitionAwardFormVm form)
    {
        // Name: required, at most 100 characters
        var name = form.AwardName?.Trim() ?? string.Empty;
        if (name.Length == 0)
        {
            throw new BusinessException("Please enter the award name", nameof(CompetitionAwardFormVm.AwardName));
        }
        if (name.Length > 100)
        {
            throw new BusinessException("Award name must be at most 100 characters", nameof(CompetitionAwardFormVm.AwardName));
        }

        // Rank ≥ 1
        if (form.Rank is not { } rank || rank < 1)
        {
            throw new BusinessException(CompetitionAwardFormVm.RankMessage, nameof(CompetitionAwardFormVm.Rank));
        }

        // Prize ≥ 0 USD, fits decimal(18,2)
        if (form.PrizeValue is not { } prize || prize < 0 || prize > MaxPrize)
        {
            throw new BusinessException(CompetitionAwardFormVm.PrizeMessage, nameof(CompetitionAwardFormVm.PrizeValue));
        }
        if (decimal.Round(prize, 2) != prize)
        {
            throw new BusinessException(PrizeDecimalsMessage, nameof(CompetitionAwardFormVm.PrizeValue));
        }

        // Quantity ≥ 1
        if (form.Quantity is not { } quantity || quantity < 1)
        {
            throw new BusinessException(CompetitionAwardFormVm.QuantityMessage, nameof(CompetitionAwardFormVm.Quantity));
        }

        // Description: optional, at most 500 characters
        if (form.PrizeDescription?.Trim().Length > 500)
        {
            throw new BusinessException("Prize description must be at most 500 characters", nameof(CompetitionAwardFormVm.PrizeDescription));
        }
        return (name, rank, prize, quantity);
    }

    /// <summary>The award name must be unique in the competition, ignoring case ("first prize" = "First Prize").</summary>
    private async Task EnsureUniqueNameAsync(long competitionId, string name, long? exceptAwardId)
    {
        // ToLower on both sides so the check is case-insensitive in SQL Server and in the in-memory test database
        var lower = name.ToLower();
        var exists = await _db.CompetitionAwards.AnyAsync(a => a.CompetitionId == competitionId
            && a.Id != exceptAwardId && a.AwardName.ToLower() == lower);
        if (exists)
        {
            throw new BusinessException(DuplicateNameMessage, nameof(CompetitionAwardFormVm.AwardName));
        }
    }

    /// <summary>How many times the award has been given (rows in award_issued).</summary>
    private Task<int> IssuedCountAsync(long awardId)
        => _db.AwardsIssued.CountAsync(i => i.CompetitionAwardId == awardId);

    /// <summary>Loads the competition or throws NotFoundException.</summary>
    private async Task<Competition> FindCompetitionAsync(long competitionId)
        => await _db.Competitions.AsNoTracking().FirstOrDefaultAsync(c => c.Id == competitionId)
           ?? throw new NotFoundException($"Competition {competitionId} was not found.");

    /// <summary>Loads a tracked award of this competition; an award of another competition counts as not found.</summary>
    private async Task<CompetitionAward> FindAwardAsync(long competitionId, long awardId)
        => await _db.CompetitionAwards.FirstOrDefaultAsync(a => a.Id == awardId && a.CompetitionId == competitionId)
           ?? throw new NotFoundException($"Award {awardId} of competition {competitionId} was not found.");

    /// <summary>GRADED competitions are read only (BR-10), including their awards.</summary>
    private static void EnsureEditable(Competition competition)
    {
        if (competition.Status == CompetitionStatus.GRADED)
        {
            throw new BusinessException(GradedReadOnlyMessage);
        }
    }

    /// <summary>Trims text; blank becomes null.</summary>
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
