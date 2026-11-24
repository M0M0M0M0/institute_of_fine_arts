using InstituteOfFineArts.Web.ViewModels.Staff.Competitions;

namespace InstituteOfFineArts.Web.Services.Interfaces;

/// <summary>
/// Results of a competition (F-M05-08, task BE1-11; TODO step 5): the ranking from the jury's scores, the awards the
/// system gives from it, and publishing (CLOSED → GRADED, BR-08). Business rule violations throw BusinessException;
/// an unknown competition throws NotFoundException.
/// </summary>
public interface ICompetitionResultService
{
    /// <summary>
    /// Ranking of the fully graded entries, highest average first, with the place (equal averages share it) and the
    /// award: the awards that will be given (preview while CLOSED) or that were given (GRADED). Disqualified entries
    /// are listed last without a place.
    /// </summary>
    /// <param name="competitionId">Competition id</param>
    Task<IReadOnlyList<RankingRowVm>> GetRankingAsync(long competitionId);

    /// <summary>
    /// Publishes the results: only when the competition is CLOSED, has a jury and every entry is graded by every judge
    /// (BR-08). Recalculates every result with the current thresholds, gives the awards from the ranking (ties share
    /// the award, each winner gets the full prize, no BEST needed), then sets GRADED with who, when and the remarks.
    /// </summary>
    /// <param name="competitionId">Competition id</param>
    /// <param name="resultRemarks">General remarks about the results (optional)</param>
    /// <param name="staffProfileId">staff_profile.id of the staff member publishing</param>
    /// <returns>Number of awards given (0 → the page shows a warning)</returns>
    Task<int> PublishAsync(long competitionId, string? resultRemarks, long staffProfileId);
}
