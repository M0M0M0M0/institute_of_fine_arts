using InstituteOfFineArts.Web.ViewModels.Staff.Competitions;

namespace InstituteOfFineArts.Web.Services.Interfaces;

/// <summary>
/// Defined awards of a competition managed by staff (F-M05-06, task BE1-06): add, edit, delete.
/// Business rule violations throw BusinessException (with the form field when there is one);
/// an unknown competition, or an award that does not belong to the competition, throws NotFoundException.
/// </summary>
public interface ICompetitionAwardService
{
    /// <summary>Empty add form for the competition. Throws BusinessException when the competition is GRADED.</summary>
    /// <param name="competitionId">Competition id</param>
    Task<CompetitionAwardFormVm> GetForCreateAsync(long competitionId);

    /// <summary>Current values of an award for the edit form. Throws BusinessException when the competition is GRADED.</summary>
    /// <param name="competitionId">Competition id</param>
    /// <param name="awardId">Award id (must belong to the competition)</param>
    Task<CompetitionAwardFormVm> GetForEditAsync(long competitionId, long awardId);

    /// <summary>Adds an award (rank ≥ 1, prize ≥ 0, quantity ≥ 1, name unique in the competition); returns its id.</summary>
    /// <param name="competitionId">Competition id</param>
    /// <param name="form">Form data</param>
    Task<long> CreateAsync(long competitionId, CompetitionAwardFormVm form);

    /// <summary>Saves an award; the quantity cannot go below the number already issued.</summary>
    /// <param name="competitionId">Competition id</param>
    /// <param name="awardId">Award id (must belong to the competition)</param>
    /// <param name="form">Form data</param>
    Task UpdateAsync(long competitionId, long awardId, CompetitionAwardFormVm form);

    /// <summary>Deletes an award that has never been issued.</summary>
    /// <param name="competitionId">Competition id</param>
    /// <param name="awardId">Award id (must belong to the competition)</param>
    Task DeleteAsync(long competitionId, long awardId);
}
