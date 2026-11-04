using InstituteOfFineArts.Web.ViewModels.Staff.Evaluations;

namespace InstituteOfFineArts.Web.Services.Interfaces;

/// <summary>
/// Evaluation of submissions by staff (F-M07-03..05, F-M07-08, task BE1-10): evaluate, edit, delete.
/// Business rule violations throw BusinessException (with the form field when there is one);
/// an unknown submission throws NotFoundException.
/// </summary>
public interface IEvaluationService
{
    /// <summary>
    /// Summary of the entry, its current evaluation and the form, with what may be done now
    /// (never throws BusinessException: a page that cannot be saved is shown read only with the reason).
    /// </summary>
    /// <param name="submissionId">Submission id</param>
    Task<EvaluationPageVm> GetPageAsync(long submissionId);

    /// <summary>
    /// Creates the evaluation, or overwrites the existing one (BR-22), as the given staff member and now.
    /// Only while the competition is CLOSED (BR-21); a late entry only accepts DISQUALIFIED (BR-14).
    /// Returns true when a new evaluation was created, false when the existing one was updated.
    /// </summary>
    /// <param name="submissionId">Submission id</param>
    /// <param name="staffProfileId">staff_profile.id of the signed-in staff member (saved as EvaluatedBy)</param>
    /// <param name="form">Form data</param>
    Task<bool> SaveAsync(long submissionId, long staffProfileId, EvaluationFormVm form);

    /// <summary>
    /// Deletes the evaluation of the submission: not when the competition is GRADED, the entry has an award
    /// or it has been sent to an exhibition.
    /// </summary>
    /// <param name="submissionId">Submission id</param>
    Task DeleteAsync(long submissionId);
}
