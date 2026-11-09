using InstituteOfFineArts.Web.Data;
using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Helpers;
using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.Services.Interfaces;
using InstituteOfFineArts.Web.ViewModels.Staff.Competitions;
using Microsoft.EntityFrameworkCore;

namespace InstituteOfFineArts.Web.Services.Implementations;

/// <summary>
/// Publishing competition results (F-M05-08, UC-06 step 5, task BE1-11). Rules: only a CLOSED competition whose
/// entries all have an evaluation can be published (BR-08, US-10 AC1); publishing stores GRADED, which makes the
/// competition, its defined awards and the evaluations read only (BR-10, BR-21) and shows the results to students
/// and on the home page (BR-24, BR-32).
/// Decision: an issued award is not required. BR-08 only asks for 100% evaluated entries, a competition may have no
/// entry worth an award, and BR-32 still allows issuing awards after GRADED; the page warns when none was issued.
/// </summary>
public class CompetitionResultService : ICompetitionResultService
{
    /// <summary>Message when the results are already published.</summary>
    public const string AlreadyPublishedMessage = "The results of this competition have already been published.";

    /// <summary>Message when the competition is UPCOMING / ONGOING (BR-08).</summary>
    public const string NotClosedMessage = "Results can only be published after the competition has closed.";

    /// <summary>Message when the result remarks are too long.</summary>
    public const string RemarksTooLongMessage = "Result remarks must be at most 2000 characters.";

    /// <summary>Database access.</summary>
    private readonly AppDbContext _db;

    /// <summary>Logs published results.</summary>
    private readonly ILogger<CompetitionResultService> _logger;

    /// <summary>Tells the participants that the results are out (optional feature M12).</summary>
    private readonly INotificationService _notifications;

    /// <summary>Receives the required services through Dependency Injection.</summary>
    public CompetitionResultService(AppDbContext db, ILogger<CompetitionResultService> logger, INotificationService notifications)
    {
        _db = db;
        _logger = logger;
        _notifications = notifications;
    }

    /// <summary>
    /// Message when entries are still waiting for an evaluation (US-10 AC1: "2 submissions have not been graded yet.").
    /// </summary>
    /// <param name="count">Entries without an evaluation</param>
    public static string NotGradedMessage(int count)
        => count == 1 ? "1 submission has not been graded yet." : $"{count} submissions have not been graded yet.";

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

        // BR-08: 100% of the entries evaluated; otherwise say how many are left (UC-06 5a)
        var notGraded = await _db.Submissions.CountAsync(s => s.CompetitionId == competitionId && s.Evaluation == null);
        if (notGraded > 0)
        {
            throw new BusinessException(NotGradedMessage(notGraded));
        }

        // Remarks: optional, limited length
        var remarks = string.IsNullOrWhiteSpace(resultRemarks) ? null : resultRemarks.Trim();
        if (remarks?.Length > PublishResultsVm.MaxRemarksLength)
        {
            throw new BusinessException(RemarksTooLongMessage, nameof(PublishResultsVm.ResultRemarks));
        }

        // Valid → GRADED with who and when
        competition.Status = CompetitionStatus.GRADED;
        competition.PublishedAt = DateTimeHelper.Now;
        competition.PublishedBy = staffProfileId;
        competition.ResultRemarks = remarks;
        await _db.SaveChangesAsync();

        var awardCount = await _db.AwardsIssued.CountAsync(a => a.CompetitionAward.CompetitionId == competitionId);
        _logger.LogInformation("Results of competition {Code} published by staff {StaffId} ({AwardCount} award(s) issued)",
            competition.Code, staffProfileId, awardCount);

        // M12: every student who submitted is told (in-app + e-mail; never fails the publish)
        await _notifications.ResultsPublishedAsync(competitionId);
        return awardCount;
    }
}
