using InstituteOfFineArts.Web.ViewModels.Staff.Evaluations;

namespace InstituteOfFineArts.Web.Services.Interfaces;

/// <summary>
/// Grading by the jury (F-M07-03..05, F-M07-08): each judge of the competition grades every entry on their own with a
/// score 0–100 per criterion (or disqualifies it); the entry's result is the jury's average (JuryResults).
/// Business rule violations throw BusinessException (with the form field when there is one); an unknown entry, or one
/// the caller may not see, throws NotFoundException.
/// </summary>
public interface IEvaluationService
{
    /// <summary>
    /// The entry, the criteria and the caller's own grade, with what may be done now (never throws BusinessException:
    /// a page that cannot be saved is shown read only with the reason).
    /// </summary>
    /// <param name="submissionId">Entry id</param>
    /// <param name="judgeId">staff_profile.id of the teacher; null = a read-only viewer (staff on the mobile app)</param>
    /// <exception cref="Exceptions.NotFoundException">Unknown id; the competition still takes entries (nobody grading
    /// sees a painting before the deadline); or the teacher is not on its jury</exception>
    Task<EvaluationPageVm> GetPageAsync(long submissionId, long? judgeId);

    /// <summary>
    /// Saves the judge's grade (created the first time, replaced afterwards), then updates the entry's result.
    /// Only a member of the jury, only while the competition is CLOSED (BR-21); a late entry can only be disqualified
    /// (BR-14). Returns true when the grade was created.
    /// </summary>
    /// <param name="submissionId">Entry id</param>
    /// <param name="judgeId">staff_profile.id of the signed-in teacher</param>
    /// <param name="form">Form data</param>
    Task<bool> SaveAsync(long submissionId, long judgeId, EvaluationFormVm form);

    /// <summary>Deletes the judge's own grade while the competition is CLOSED, then updates the entry's result.</summary>
    /// <param name="submissionId">Entry id</param>
    /// <param name="judgeId">staff_profile.id of the signed-in teacher</param>
    Task DeleteAsync(long submissionId, long judgeId);

    /// <summary>
    /// The jury's full score sheet of an entry, or null when the viewer may not see it (yet):
    /// the student who owns it, once GRADED; a judge of the competition, once GRADED; staff, once the entry is fully
    /// graded. Nobody else – other students and guests never see scores, even of public paintings.
    /// </summary>
    /// <param name="submissionId">Entry id</param>
    /// <param name="viewer">Who is asking</param>
    Task<ScoreSheetVm?> GetScoreSheetAsync(long submissionId, ScoreSheetViewer viewer);
}

/// <summary>Who asks for a score sheet.</summary>
/// <param name="Role">STAFF, TEACHER, STUDENT, ADMIN (RoleNames)</param>
/// <param name="ProfileId">staff_profile.id or student_profile.id</param>
public sealed record ScoreSheetViewer(string Role, long ProfileId);
