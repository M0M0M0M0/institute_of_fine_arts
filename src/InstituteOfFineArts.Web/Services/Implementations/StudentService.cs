using InstituteOfFineArts.Web.Data;
using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.Services.Interfaces;
using InstituteOfFineArts.Web.Validation;
using InstituteOfFineArts.Web.ViewModels.Admin.Students;
using InstituteOfFineArts.Web.ViewModels.Common;
using Microsoft.EntityFrameworkCore;

namespace InstituteOfFineArts.Web.Services.Implementations;

/// <summary>
/// Student account management (M04, task BE2-03): administrators create / edit / lock / delete,
/// staff members only read (list + details).
/// </summary>
public class StudentService : IStudentService
{
    /// <summary>Duplicate username (BR-01).</summary>
    public const string UsernameTakenMessage = "Username already exists";

    /// <summary>Duplicate email (BR-01).</summary>
    public const string EmailTakenMessage = "Email already exists";

    /// <summary>Duplicate student code (BR-01).</summary>
    public const string CodeTakenMessage = "Student code already exists";

    /// <summary>Missing enrollment date.</summary>
    public const string EnrollmentDateMessage = "Please choose the enrollment date";

    /// <summary>Missing course.</summary>
    public const string CourseMessage = "Please enter the course";

    /// <summary>Prefix of generated student codes.</summary>
    private const string CodePrefix = "HV";

    /// <summary>Database access.</summary>
    private readonly AppDbContext _db;

    /// <summary>Logs account changes (never passwords).</summary>
    private readonly ILogger<StudentService> _logger;

    /// <summary>Receives the required services through Dependency Injection.</summary>
    public StudentService(AppDbContext db, ILogger<StudentService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<PagedResult<StudentListItemVm>> SearchAsync(StudentSearchVm search, int pageSize = 10)
    {
        var query = _db.StudentProfiles.AsNoTracking().AsQueryable();

        // Each filter is applied only when it has a value
        if (!string.IsNullOrWhiteSpace(search.Keyword))
        {
            var k = search.Keyword.Trim();
            query = query.Where(s => s.Account.FullName.Contains(k) || s.Account.Username.Contains(k)
                                     || s.StudentCode.Contains(k) || s.Account.Email.Contains(k));
        }
        if (!string.IsNullOrWhiteSpace(search.Course))
        {
            var course = search.Course.Trim();
            query = query.Where(s => s.CourseName.Contains(course));
        }
        if (!string.IsNullOrWhiteSpace(search.ClassName))
        {
            var className = search.ClassName.Trim();
            query = query.Where(s => s.ClassName != null && s.ClassName.Contains(className));
        }
        if (search.Status is { } status)
        {
            query = query.Where(s => s.EnrollmentStatus == status);
        }
        if (search.EnrolledFrom is { } from)
        {
            query = query.Where(s => s.EnrollmentDate >= from);
        }
        if (search.EnrolledTo is { } to)
        {
            query = query.Where(s => s.EnrollmentDate <= to);
        }
        if (search.Active is { } active)
        {
            query = query.Where(s => s.Account.Active == active);
        }

        // Ordered by student code, projected to list rows (no password hash leaves the database)
        var rows = query.OrderBy(s => s.StudentCode).Select(s => new StudentListItemVm
        {
            Id = s.Id,
            StudentCode = s.StudentCode,
            FullName = s.Account.FullName,
            Username = s.Account.Username,
            Email = s.Account.Email,
            CourseName = s.CourseName,
            ClassName = s.ClassName,
            EnrollmentDate = s.EnrollmentDate,
            EnrollmentStatus = s.EnrollmentStatus,
            Active = s.Account.Active,
        });
        return await PagedResult<StudentListItemVm>.CreateAsync(rows, search.Page, pageSize);
    }

    /// <inheritdoc />
    public async Task<StudentDetailsVm> GetDetailsAsync(long id)
    {
        var s = await LoadAsync(id, tracked: false);
        var vm = new StudentDetailsVm
        {
            Active = s.Account.Active,
            LastLoginAt = s.Account.LastLoginAt,
            CreatedAt = s.Account.CreatedAt,
            SubmissionCount = await _db.Submissions.CountAsync(x => x.StudentId == id),
            ExhibitionEntryCount = await _db.ExhibitionEntries.CountAsync(e => e.ProposedBy == s.AccountId || e.Submission.StudentId == id),
            AwardCount = await _db.AwardsIssued.CountAsync(a => a.Submission.StudentId == id),
        };
        CopyToForm(s, vm);
        return vm;
    }

    /// <inheritdoc />
    public async Task<StudentFormVm> GetForEditAsync(long id)
    {
        var vm = new StudentFormVm();
        CopyToForm(await LoadAsync(id, tracked: false), vm);
        return vm;
    }

    /// <inheritdoc />
    public async Task<long> CreateAsync(CreateStudentVm form)
    {
        var profile = await BuildNewAsync(form, new NewStudentBatch());

        // Account and profile are inserted by one SaveChanges, which EF Core runs in a single transaction
        _db.StudentProfiles.Add(profile);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Student account {Username} ({Code}) created", profile.Account.Username, profile.StudentCode);
        return profile.Id;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BusinessException>> CheckNewAsync(CreateStudentVm form)
        => (await CollectNewErrorsAsync(form, new NewStudentBatch())).Errors;

    /// <inheritdoc />
    public async Task<int> CreateManyAsync(IReadOnlyList<CreateStudentVm> forms)
    {
        // Every student is checked with the same rules as CreateAsync before anything is saved;
        // the batch also keeps usernames / emails / codes unique between the new students themselves
        var batch = new NewStudentBatch();
        var profiles = new List<StudentProfile>();
        foreach (var form in forms)
        {
            try
            {
                profiles.Add(await BuildNewAsync(form, batch));
            }
            catch (BusinessException ex)
            {
                // Name the student so the administrator knows which row failed; nothing has been saved yet
                throw new BusinessException($"{form.NewUsername.Trim()}: {ex.Message}", ex.Field);
            }
        }

        // All accounts and profiles are inserted by one SaveChanges = one transaction (all or nothing)
        _db.StudentProfiles.AddRange(profiles);
        await _db.SaveChangesAsync();

        _logger.LogInformation("{Count} student accounts created in one batch: {Usernames}",
            profiles.Count, string.Join(", ", profiles.Select(p => p.Account.Username)));
        return profiles.Count;
    }

    /// <summary>
    /// Checks the create rules (BR-01 unique username / email / code, BR-02 strong password, required study data)
    /// and builds the new STUDENT account + profile without saving it. Throws the first violation as BusinessException.
    /// </summary>
    /// <param name="form">Create form</param>
    /// <param name="batch">Values already used by other new students of the same save</param>
    private async Task<StudentProfile> BuildNewAsync(CreateStudentVm form, NewStudentBatch batch)
    {
        var (errors, username, email, code) = await CollectNewErrorsAsync(form, batch);
        if (errors.Count > 0)
        {
            throw errors[0];
        }

        var profile = new StudentProfile
        {
            StudentCode = code,
            Account = new Account
            {
                Username = username,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(form.Password),
                Role = Role.STUDENT,
                Active = true,
            },
        };
        Apply(form, profile, email);

        // Reserve the values for the next students of the same batch
        batch.Usernames.Add(username);
        batch.Emails.Add(email.ToLowerInvariant());
        batch.Codes.Add(code);
        return profile;
    }

    /// <summary>
    /// All create-only rule violations of one form (in the order of the form) plus the normalised values.
    /// A blank student code is replaced by the next free HVnnnn.
    /// </summary>
    /// <param name="form">Create form</param>
    /// <param name="batch">Values already used by other new students of the same save</param>
    private async Task<(List<BusinessException> Errors, string Username, string Email, string Code)> CollectNewErrorsAsync(
        CreateStudentVm form, NewStudentBatch batch)
    {
        var errors = new List<BusinessException>();
        var username = form.NewUsername.Trim().ToLowerInvariant();
        var email = form.Email.Trim();

        // BR-01: username, email and student code are unique (in the database and within the same batch)
        if (batch.Usernames.Contains(username) || await _db.Accounts.AnyAsync(a => a.Username == username))
        {
            errors.Add(new BusinessException(UsernameTakenMessage, nameof(CreateStudentVm.NewUsername)));
        }
        if (batch.Emails.Contains(email.ToLowerInvariant()) || await IsEmailTakenAsync(email, exceptAccountId: null))
        {
            errors.Add(new BusinessException(EmailTakenMessage, nameof(StudentFormVm.Email)));
        }
        var code = string.IsNullOrWhiteSpace(form.StudentCode) ? await NextCodeAsync(batch.Codes) : form.StudentCode.Trim().ToUpperInvariant();
        if (batch.Codes.Contains(code) || await IsCodeTakenAsync(code, exceptProfileId: null))
        {
            errors.Add(new BusinessException(CodeTakenMessage, nameof(StudentFormVm.StudentCode)));
        }

        // BR-02 again on the server side
        if (!StrongPasswordAttribute.IsStrong(form.Password))
        {
            errors.Add(new BusinessException(new StrongPasswordAttribute().FormatErrorMessage("Password"), nameof(CreateStudentVm.Password)));
        }
        return (errors, username, email, code);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(long id, StudentFormVm form)
    {
        var s = await LoadAsync(id, tracked: true);
        var email = form.Email.Trim();

        // BR-01 on edit: ignore the student's own email / code
        await EnsureEmailFreeAsync(email, s.AccountId);
        var code = string.IsNullOrWhiteSpace(form.StudentCode) ? s.StudentCode : form.StudentCode.Trim().ToUpperInvariant();
        await EnsureCodeFreeAsync(code, s.Id);

        // Username and role never change here
        s.StudentCode = code;
        Apply(form, s, email);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Student account {Username} updated", s.Account.Username);
    }

    /// <inheritdoc />
    public async Task ResetPasswordAsync(long id, string newPassword)
    {
        // BR-02: the temporary password must be strong too
        if (!StrongPasswordAttribute.IsStrong(newPassword))
        {
            throw new BusinessException(new StrongPasswordAttribute().FormatErrorMessage("New password"), nameof(ResetStudentPasswordVm.NewPassword));
        }
        var s = await LoadAsync(id, tracked: true);
        s.Account.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Password reset for student {Username}", s.Account.Username);
    }

    /// <inheritdoc />
    public async Task SetActiveAsync(long id, bool active)
    {
        var s = await LoadAsync(id, tracked: true);
        s.Account.Active = active;
        await _db.SaveChangesAsync();
        _logger.LogInformation("Student account {Username} {State}", s.Account.Username, active ? "unlocked" : "locked");
    }

    /// <inheritdoc />
    public async Task<StudentDeleteResult> DeleteAsync(long id)
    {
        var s = await LoadAsync(id, tracked: true);

        // BR-04: with submissions or exhibition entries the account is only locked, so no data is orphaned (TC-M04-04)
        if (await HasRelatedDataAsync(s))
        {
            s.Account.Active = false;
            await _db.SaveChangesAsync();
            _logger.LogInformation("Student account {Username} locked instead of deleted (related data)", s.Account.Username);
            return StudentDeleteResult.Deactivated;
        }

        // No related data: remove the profile and its account in one save
        _db.StudentProfiles.Remove(s);
        _db.Accounts.Remove(s.Account);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Student account {Username} deleted", s.Account.Username);
        return StudentDeleteResult.Deleted;
    }

    /// <summary>True when the student has submissions or exhibition entries (proposed by them or of their work).</summary>
    private async Task<bool> HasRelatedDataAsync(StudentProfile s)
        => await _db.Submissions.AnyAsync(x => x.StudentId == s.Id)
           || await _db.ExhibitionEntries.AnyAsync(e => e.ProposedBy == s.AccountId || e.Submission.StudentId == s.Id);

    /// <summary>Email unique across all accounts, ignoring case (BR-01).</summary>
    private async Task EnsureEmailFreeAsync(string email, long? exceptAccountId)
    {
        if (await IsEmailTakenAsync(email, exceptAccountId))
        {
            throw new BusinessException(EmailTakenMessage, nameof(StudentFormVm.Email));
        }
    }

    /// <summary>True when another account already uses the email, ignoring case (BR-01).</summary>
    private Task<bool> IsEmailTakenAsync(string email, long? exceptAccountId)
    {
        var lower = email.ToLower();
        return _db.Accounts.AnyAsync(a => a.Email.ToLower() == lower && a.Id != exceptAccountId);
    }

    /// <summary>Student code unique (BR-01).</summary>
    private async Task EnsureCodeFreeAsync(string code, long? exceptProfileId)
    {
        if (await IsCodeTakenAsync(code, exceptProfileId))
        {
            throw new BusinessException(CodeTakenMessage, nameof(StudentFormVm.StudentCode));
        }
    }

    /// <summary>True when another student already has the code (BR-01).</summary>
    private Task<bool> IsCodeTakenAsync(string code, long? exceptProfileId)
        => _db.StudentProfiles.AnyAsync(s => s.StudentCode == code && s.Id != exceptProfileId);

    /// <summary>Next free code HVnnnn after the highest existing HV number (also counting codes reserved by the batch).</summary>
    /// <param name="reserved">Codes given to other new students of the same save</param>
    private async Task<string> NextCodeAsync(IEnumerable<string> reserved)
    {
        var codes = await _db.StudentProfiles.Where(s => s.StudentCode.StartsWith(CodePrefix)).Select(s => s.StudentCode).ToListAsync();
        codes.AddRange(reserved.Where(c => c.StartsWith(CodePrefix)));
        var max = codes.Select(c => int.TryParse(c[CodePrefix.Length..], out var n) ? n : 0).DefaultIfEmpty(0).Max();
        return $"{CodePrefix}{max + 1:D4}";
    }

    /// <summary>Loads a profile with its account or throws NotFoundException.</summary>
    private async Task<StudentProfile> LoadAsync(long id, bool tracked)
    {
        var query = _db.StudentProfiles.Include(s => s.Account).AsQueryable();
        if (!tracked)
        {
            query = query.AsNoTracking();
        }
        return await query.FirstOrDefaultAsync(s => s.Id == id)
               ?? throw new NotFoundException($"Student {id} was not found.");
    }

    /// <summary>Copies the editable form fields onto the profile and its account (re-checking the required ones).</summary>
    private static void Apply(StudentFormVm form, StudentProfile s, string email)
    {
        // Rules repeated on the server side, in case the form validation was skipped
        if (form.DateOfBirth is { } dob && dob >= Helpers.DateTimeHelper.Today)
        {
            throw new BusinessException(StudentDateMessages.DateOfBirthInPast, nameof(StudentFormVm.DateOfBirth));
        }
        if (string.IsNullOrWhiteSpace(form.CourseName))
        {
            throw new BusinessException(CourseMessage, nameof(StudentFormVm.CourseName));
        }

        // Personal information lives on the account
        s.Account.FullName = form.FullName.Trim();
        s.Account.Email = email;
        s.Account.Gender = form.Gender;
        s.Account.DateOfBirth = form.DateOfBirth;
        s.Account.Phone = Clean(form.Phone);
        s.Account.Address = Clean(form.Address);

        // Study information lives on the profile
        s.EnrollmentDate = form.EnrollmentDate ?? throw new BusinessException(EnrollmentDateMessage, nameof(StudentFormVm.EnrollmentDate));
        s.CourseName = form.CourseName.Trim();
        s.ClassName = Clean(form.ClassName);
        s.EnrollmentStatus = form.EnrollmentStatus ?? EnrollmentStatus.STUDYING;
        s.Notes = Clean(form.Notes);
    }

    /// <summary>Copies a profile into a form / details view model.</summary>
    private static void CopyToForm(StudentProfile s, StudentFormVm vm)
    {
        vm.Id = s.Id;
        vm.Username = s.Account.Username;
        vm.FullName = s.Account.FullName;
        vm.Email = s.Account.Email;
        vm.Gender = s.Account.Gender;
        vm.DateOfBirth = s.Account.DateOfBirth;
        vm.Phone = s.Account.Phone;
        vm.Address = s.Account.Address;
        vm.StudentCode = s.StudentCode;
        vm.EnrollmentDate = s.EnrollmentDate;
        vm.CourseName = s.CourseName;
        vm.ClassName = s.ClassName;
        vm.EnrollmentStatus = s.EnrollmentStatus;
        vm.Notes = s.Notes;
    }

    /// <summary>Usernames, emails (both lower case) and codes already taken by new students of the same save.</summary>
    private sealed class NewStudentBatch
    {
        /// <summary>Reserved usernames.</summary>
        public HashSet<string> Usernames { get; } = new();

        /// <summary>Reserved emails (lower case).</summary>
        public HashSet<string> Emails { get; } = new();

        /// <summary>Reserved student codes.</summary>
        public HashSet<string> Codes { get; } = new();
    }

    /// <summary>Trims text; blank becomes null.</summary>
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
