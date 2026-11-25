using InstituteOfFineArts.Web.Data;
using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Helpers;
using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.Services.Interfaces;
using InstituteOfFineArts.Web.ViewModels.Staff.Competitions;
using Microsoft.EntityFrameworkCore;

namespace InstituteOfFineArts.Web.Services.Implementations;

/// <summary>
/// Results of a competition (F-M05-08, task BE1-11; TODO step 5, decided 18/11/2026).
/// <list type="bullet">
/// <item>Ranking: entries by the jury's average, compared at full precision (rounding is for display only); equal
/// averages share a place ("1, 1, 2"); disqualified entries are out (<see cref="GradingMath"/>).</item>
/// <item>Awards are given by the system from the ranking when the results are published: by rank, each award takes the
/// next places until its quantity is reached – a shared place is never split, so a tie can exceed the quantity, and
/// every winner gets the full prize. No BEST rating is required. Staff do not pick winners; they may still revoke an
/// award for a violation (BR-33), which leaves the place empty.</item>
/// <item>Publishing (BR-08): CLOSED, a jury, criteria, and every entry graded by every judge.</item>
/// </list>
/// </summary>
public class CompetitionResultService : ICompetitionResultService
{
    /// <summary>Message when the results were already published.</summary>
    public const string AlreadyPublishedMessage = "The results of this competition have already been published.";

    /// <summary>Message when the competition is not closed yet.</summary>
    public const string NotClosedMessage = "Results can only be published after the competition has closed.";

    /// <summary>Message when the remarks are too long.</summary>
    public const string RemarksTooLongMessage = "Result remarks must be at most 2000 characters.";

    /// <summary>Message when the competition has no jury.</summary>
    public const string NoJuryMessage = "This competition has no jury yet. Add the judges on the edit page, let them grade, then publish.";

    /// <summary>Database access.</summary>
    private readonly AppDbContext _db;

    /// <summary>Logs published results.</summary>
    private readonly ILogger<CompetitionResultService> _logger;

    /// <summary>Tells the students.</summary>
    private readonly INotificationService _notifications;

    /// <summary>Receives the required services through Dependency Injection.</summary>
    public CompetitionResultService(AppDbContext db, ILogger<CompetitionResultService> logger, INotificationService notifications)
    {
        _db = db;
        _logger = logger;
        _notifications = notifications;
    }

    /// <summary>"N submissions have not been graded yet." (by every judge).</summary>
    public static string NotGradedMessage(int count)
        => count == 1 ? "1 submission has not been graded by the whole jury yet." : $"{count} submissions have not been graded by the whole jury yet.";

    /// <inheritdoc />
    public async Task<IReadOnlyList<RankingRowVm>> GetRankingAsync(long competitionId)
    {
        var competition = await _db.Competitions.AsNoTracking().FirstOrDefaultAsync(c => c.Id == competitionId)
            ?? throw new NotFoundException($"Competition {competitionId} was not found.");
        var entries = await _db.Submissions.AsNoTracking().Where(s => s.CompetitionId == competitionId)
            .Select(s => new
            {
                s.Id, s.Title, Student = s.Student.Account.FullName,
                Rating = s.Evaluation == null ? (Rating?)null : s.Evaluation.Rating,
                Award = s.AwardIssued == null ? null : s.AwardIssued.CompetitionAward.AwardName,
            })
            .ToDictionaryAsync(s => s.Id);
        var results = await JuryResults.CalculateAsync(_db, competitionId);
        var complete = results.Values.Where(r => r.Complete && !r.Disqualified && r.Average != null)
            .Select(r => new GradingMath.Ranked(r.SubmissionId, r.Average!.Value)).ToList();
        var places = GradingMath.Places(complete).SelectMany(p => p.Entries.Select(e => (e.SubmissionId, p.Place))).ToDictionary(x => x.SubmissionId, x => x.Place);

        // Awards: given ones once GRADED, otherwise what publishing would give
        Dictionary<long, string> awards;
        if (CompetitionStatusResolver.Resolve(competition) == CompetitionStatus.GRADED)
        {
            awards = entries.Values.Where(e => e.Award != null).ToDictionary(e => e.Id, e => e.Award!);
        }
        else
        {
            var slots = await AwardSlotsAsync(competitionId);
            var names = await _db.CompetitionAwards.AsNoTracking().Where(a => a.CompetitionId == competitionId).ToDictionaryAsync(a => a.Id, a => a.AwardName);
            awards = GradingMath.Awards(complete, slots).ToDictionary(w => w.SubmissionId, w => names[w.AwardId]);
        }

        var ranked = complete.Select(r => r.SubmissionId)
            .OrderBy(id => places[id]).ThenBy(id => id)
            .Select(id => new RankingRowVm(id, entries[id].Title, entries[id].Student, Math.Round(results[id].Average!.Value, 2, MidpointRounding.AwayFromZero),
                entries[id].Rating, places[id], awards.GetValueOrDefault(id)));
        var disqualified = results.Values.Where(r => r.Complete && r.Disqualified)
            .Select(r => new RankingRowVm(r.SubmissionId, entries[r.SubmissionId].Title, entries[r.SubmissionId].Student, null,
                Rating.DISQUALIFIED, null, null));
        return ranked.Concat(disqualified).ToList();
    }

    /// <inheritdoc />
    public async Task<int> PublishAsync(long competitionId, string? resultRemarks, long staffProfileId)
    {
        var competition = await _db.Competitions.FirstOrDefaultAsync(c => c.Id == competitionId)
            ?? throw new NotFoundException($"Competition {competitionId} was not found.");

        // BR-08: only CLOSED (after the deadline, not published yet)
        var status = CompetitionStatusResolver.Resolve(competition);
        if (status == CompetitionStatus.GRADED)
        {
            throw new BusinessException(AlreadyPublishedMessage);
        }
        if (status != CompetitionStatus.CLOSED)
        {
            throw new BusinessException(NotClosedMessage);
        }
        if (!await _db.CompetitionJudges.AnyAsync(j => j.CompetitionId == competitionId))
        {
            throw new BusinessException(NoJuryMessage);
        }

        // Remarks: optional, limited length
        var remarks = RichText.CleanChecked(resultRemarks, PublishResultsVm.MaxRemarksLength, nameof(PublishResultsVm.ResultRemarks), RemarksTooLongMessage);

        // Every result again with today's thresholds, then BR-08: every entry graded by every judge
        await JuryResults.RecomputeAsync(_db, competitionId);
        var results = await JuryResults.CalculateAsync(_db, competitionId);
        var notGraded = results.Values.Count(r => !r.Complete);
        if (notGraded > 0)
        {
            throw new BusinessException(NotGradedMessage(notGraded));
        }

        // Awards from the ranking (anything given by hand before publishing is replaced)
        var ranked = results.Values.Where(r => !r.Disqualified && r.Average != null)
            .Select(r => new GradingMath.Ranked(r.SubmissionId, r.Average!.Value)).ToList();
        var winners = GradingMath.Awards(ranked, await AwardSlotsAsync(competitionId));
        var handGiven = await _db.AwardsIssued.Where(a => a.CompetitionAward.CompetitionId == competitionId).ToListAsync();
        if (handGiven.Count > 0)
        {
            // Saved first: the same entry may win again, and the unique index on submission_id must not see both rows
            _db.AwardsIssued.RemoveRange(handGiven);
            await _db.SaveChangesAsync();
        }
        var today = DateTimeHelper.Today;
        foreach (var w in winners)
        {
            _db.AwardsIssued.Add(new AwardIssued
            {
                CompetitionAwardId = w.AwardId, SubmissionId = w.SubmissionId, IssuedBy = staffProfileId, IssuedDate = today,
                Remarks = w.Place == 1 ? null : $"Place {w.Place} in the ranking",
            });
        }

        // GRADED with who and when
        competition.Status = CompetitionStatus.GRADED;
        competition.PublishedAt = DateTimeHelper.Now;
        competition.PublishedBy = staffProfileId;
        competition.ResultRemarks = remarks;
        await _db.SaveChangesAsync();

        _logger.LogInformation("Results of competition {Code} published by staff {StaffId} ({AwardCount} award(s) given from the scores)",
            competition.Code, staffProfileId, winners.Count);

        // M12: every student who submitted is told (in-app + e-mail; never fails the publish)
        await _notifications.ResultsPublishedAsync(competitionId);
        return winners.Count;
    }

    /// <summary>The awards of a competition as slots for <see cref="GradingMath.Awards"/>.</summary>
    private async Task<List<GradingMath.AwardSlot>> AwardSlotsAsync(long competitionId)
        => await _db.CompetitionAwards.AsNoTracking().Where(a => a.CompetitionId == competitionId)
            .Select(a => new GradingMath.AwardSlot(a.Id, a.Rank, a.Quantity)).ToListAsync();
}
