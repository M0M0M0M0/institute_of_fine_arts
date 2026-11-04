using InstituteOfFineArts.Web.Data;
using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Services.Interfaces;
using InstituteOfFineArts.Web.Validation;
using InstituteOfFineArts.Web.ViewModels.Profile;
using Microsoft.EntityFrameworkCore;

namespace InstituteOfFineArts.Web.Services.Implementations;

/// <summary>
/// The signed-in user's own profile. Role, username, codes and names are never changed here (BR-03).
/// </summary>
public class ProfileService : IProfileService
{
    /// <summary>Message when the current password does not match.</summary>
    public const string WrongCurrentPasswordMessage = "Your current password is incorrect.";

    /// <summary>Message when the new password is the same as the current one.</summary>
    public const string SamePasswordMessage = "The new password must be different from the current password.";

    /// <summary>Database access.</summary>
    private readonly AppDbContext _db;

    /// <summary>Logs profile and password changes (never the passwords themselves).</summary>
    private readonly ILogger<ProfileService> _logger;

    /// <summary>Receives the required services through Dependency Injection.</summary>
    public ProfileService(AppDbContext db, ILogger<ProfileService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ProfileVm> GetProfileAsync(long accountId)
    {
        // Read-only query with both possible profiles
        var account = await _db.Accounts
            .AsNoTracking()
            .Include(a => a.StaffProfile)
            .Include(a => a.StudentProfile)
            .FirstOrDefaultAsync(a => a.Id == accountId)
            ?? throw new NotFoundException($"Account {accountId} was not found.");

        var vm = new ProfileVm
        {
            Username = account.Username,
            FullName = account.FullName,
            Email = account.Email,
            Role = account.Role,
            DateOfBirth = account.DateOfBirth,
            Gender = account.Gender,
            Phone = account.Phone,
            Address = account.Address,
            LastLoginAt = account.LastLoginAt,
        };

        // Students and staff have different profile tables; fill the shared display fields
        if (account.StudentProfile is { } student)
        {
            vm.Code = student.StudentCode;
            vm.CourseOrPosition = student.CourseName;
            vm.Classes = student.ClassName;
            vm.SinceDate = student.EnrollmentDate;
        }
        else if (account.StaffProfile is { } staff)
        {
            vm.Code = staff.StaffCode;
            vm.CourseOrPosition = staff.Position;
            vm.Classes = staff.ClassesInCharge;
            vm.Subjects = staff.SubjectsInCharge;
            vm.SinceDate = staff.JoinedDate;
        }

        return vm;
    }

    /// <inheritdoc />
    public async Task UpdateProfileAsync(long accountId, EditProfileVm form)
    {
        var account = await FindAccountAsync(accountId);

        // Only these two fields are taken from the form
        account.Phone = Clean(form.Phone);
        account.Address = Clean(form.Address);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Profile updated: {Username}", account.Username);
    }

    /// <inheritdoc />
    public async Task ChangePasswordAsync(long accountId, string currentPassword, string newPassword)
    {
        var account = await FindAccountAsync(accountId);

        // The current password must be correct
        if (!BCrypt.Net.BCrypt.Verify(currentPassword ?? string.Empty, account.PasswordHash))
        {
            _logger.LogWarning("Password change rejected: wrong current password for {Username}", account.Username);
            throw new BusinessException(WrongCurrentPasswordMessage, nameof(ChangePasswordVm.CurrentPassword));
        }

        // BR-02 is also checked on the form; checked again here so no caller can skip it
        if (!StrongPasswordAttribute.IsStrong(newPassword))
        {
            throw new BusinessException(new StrongPasswordAttribute().FormatErrorMessage("New password"),
                nameof(ChangePasswordVm.NewPassword));
        }

        // Reusing the same password is pointless
        if (newPassword == currentPassword)
        {
            throw new BusinessException(SamePasswordMessage, nameof(ChangePasswordVm.NewPassword));
        }

        account.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Password changed: {Username}", account.Username);
    }

    /// <summary>Loads a tracked account for updating, or throws NotFoundException.</summary>
    private async Task<Account> FindAccountAsync(long accountId)
        => await _db.Accounts.FirstOrDefaultAsync(a => a.Id == accountId)
           ?? throw new NotFoundException($"Account {accountId} was not found.");

    /// <summary>Trims a text value; blank becomes null.</summary>
    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
