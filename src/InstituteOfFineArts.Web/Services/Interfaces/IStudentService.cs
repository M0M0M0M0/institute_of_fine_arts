using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.ViewModels.Admin.Students;
using InstituteOfFineArts.Web.ViewModels.Common;

namespace InstituteOfFineArts.Web.Services.Interfaces;

/// <summary>
/// Student accounts (M04, task BE2-03): STUDENT accounts with their student_profile.
/// Admin manages them; Staff only reads the list and details.
/// Ids are student_profile ids. Rule violations throw BusinessException (with the form field when there is one);
/// unknown ids throw NotFoundException.
/// </summary>
public interface IStudentService
{
    /// <summary>Searches by keyword, course, class, study status, enrollment date and account status, ordered by student code.</summary>
    Task<PagedResult<StudentListItemVm>> SearchAsync(StudentSearchVm search, int pageSize = 10);

    /// <summary>Details with counts of related data (submissions, exhibition entries, awards).</summary>
    /// <param name="id">student_profile.id</param>
    Task<StudentDetailsVm> GetDetailsAsync(long id);

    /// <summary>Current values for the edit form.</summary>
    Task<StudentFormVm> GetForEditAsync(long id);

    /// <summary>Creates the STUDENT account and its student profile in one save (one transaction); returns the profile id.</summary>
    Task<long> CreateAsync(CreateStudentVm form);

    /// <summary>
    /// The create rules that need the database (BR-01 unique username / email / code) plus BR-02, without saving:
    /// every violation found, empty when the form can be created. Used by the CSV import preview (F-M04-08).
    /// </summary>
    Task<IReadOnlyList<BusinessException>> CheckNewAsync(CreateStudentVm form);

    /// <summary>
    /// Creates several students with exactly the same rules as <see cref="CreateAsync"/> in one save (one transaction):
    /// either all are created or, on the first violation, none (BusinessException naming the username). Returns the count.
    /// </summary>
    Task<int> CreateManyAsync(IReadOnlyList<CreateStudentVm> forms);

    /// <summary>Saves changes (not the username, not the role). Applies BR-01.</summary>
    Task UpdateAsync(long id, StudentFormVm form);

    /// <summary>Sets a temporary password (BR-02, stored as BCrypt).</summary>
    Task ResetPasswordAsync(long id, string newPassword);

    /// <summary>Locks or unlocks the account.</summary>
    Task SetActiveAsync(long id, bool active);

    /// <summary>Deletes the student, or locks the account when submissions or exhibition entries exist (BR-04).</summary>
    Task<StudentDeleteResult> DeleteAsync(long id);
}
