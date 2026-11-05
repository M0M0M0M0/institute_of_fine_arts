namespace InstituteOfFineArts.Web.Services.Interfaces;

/// <summary>
/// Publishing the results of a competition (F-M05-08, task BE1-11): CLOSED → GRADED (BR-08).
/// Business rule violations throw BusinessException; an unknown competition throws NotFoundException.
/// </summary>
public interface ICompetitionResultService
{
    /// <summary>
    /// Publishes the results: only when the competition is CLOSED and every entry has an evaluation (BR-08).
    /// Sets status GRADED, the publish time, the publisher and the result remarks.
    /// </summary>
    /// <param name="competitionId">Competition id</param>
    /// <param name="resultRemarks">General remarks about the results (optional)</param>
    /// <param name="staffProfileId">staff_profile.id of the staff member publishing</param>
    /// <returns>Number of awards issued in the competition (0 → the page shows a warning)</returns>
    Task<int> PublishAsync(long competitionId, string? resultRemarks, long staffProfileId);
}
