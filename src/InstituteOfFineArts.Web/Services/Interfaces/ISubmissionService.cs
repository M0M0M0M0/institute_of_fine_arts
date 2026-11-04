using InstituteOfFineArts.Web.ViewModels.Common;
using InstituteOfFineArts.Web.ViewModels.Student.Submissions;

namespace InstituteOfFineArts.Web.Services.Interfaces;

/// <summary>
/// A student's own competition entries (M06, tasks BE1-07..BE1-09): submit, list, details, edit / replace image,
/// delete. Every method takes the signed-in student's student_profile.id; entries of other students and unknown
/// ids throw NotFoundException (they are never revealed). Rule violations throw BusinessException.
/// </summary>
public interface ISubmissionService
{
    /// <summary>
    /// Empty submit form for a competition. Checks it exists (NotFoundException), is ONGOING (BR-13, BR-14)
    /// and that the student has not submitted yet (BR-15).
    /// </summary>
    /// <param name="competitionId">Competition id</param>
    /// <param name="studentProfileId">student_profile.id of the signed-in student</param>
    Task<SubmissionFormVm> GetNewFormAsync(long competitionId, long studentProfileId);

    /// <summary>
    /// Submits an entry: checks the competition and the fields again, stores the JPEG (BR-16..BR-18) and inserts
    /// the submission. Returns the new id and an optional image warning.
    /// </summary>
    /// <param name="form">Validated form data (CompetitionId, texts, image)</param>
    /// <param name="studentProfileId">student_profile.id of the signed-in student</param>
    Task<SubmissionSaveResult> CreateAsync(SubmissionFormVm form, long studentProfileId);

    /// <summary>The student's own entries with filters and paging, newest first; ratings only of GRADED competitions (BR-24).</summary>
    /// <param name="studentProfileId">student_profile.id of the signed-in student</param>
    /// <param name="search">Filters</param>
    /// <param name="pageSize">Rows per page</param>
    Task<PagedResult<MySubmissionListItemVm>> SearchMineAsync(long studentProfileId, MySubmissionSearchVm search, int pageSize = 10);

    /// <summary>Competitions the student has entered, for the competition filter.</summary>
    /// <param name="studentProfileId">student_profile.id of the signed-in student</param>
    Task<List<MyCompetitionOptionVm>> GetMyCompetitionsAsync(long studentProfileId);

    /// <summary>Details of an own entry; the evaluation only when the competition is GRADED (BR-24).</summary>
    /// <param name="id">Submission id</param>
    /// <param name="studentProfileId">student_profile.id of the signed-in student</param>
    Task<MySubmissionDetailsVm> GetDetailsAsync(long id, long studentProfileId);

    /// <summary>Edit form with the current values; throws BusinessException after the deadline (BR-13, BR-14).</summary>
    /// <param name="id">Submission id</param>
    /// <param name="studentProfileId">student_profile.id of the signed-in student</param>
    Task<SubmissionEditVm> GetForEditAsync(long id, long studentProfileId);

    /// <summary>
    /// Saves the new texts and, when a new image is chosen, replaces the image: new file first, then the row,
    /// then the old files are deleted (BR-18). Returns the id and an optional image warning.
    /// </summary>
    /// <param name="id">Submission id</param>
    /// <param name="form">Validated form data</param>
    /// <param name="studentProfileId">student_profile.id of the signed-in student</param>
    Task<SubmissionSaveResult> UpdateAsync(long id, SubmissionEditVm form, long studentProfileId);

    /// <summary>Deletes an own entry and its files while the competition is ONGOING (BR-13, BR-18).</summary>
    /// <param name="id">Submission id</param>
    /// <param name="studentProfileId">student_profile.id of the signed-in student</param>
    Task DeleteAsync(long id, long studentProfileId);
}
