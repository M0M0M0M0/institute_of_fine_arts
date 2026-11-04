using InstituteOfFineArts.Web.Data;
using InstituteOfFineArts.Web.Exceptions;
using InstituteOfFineArts.Web.Models.Entities;
using InstituteOfFineArts.Web.Models.Enums;
using InstituteOfFineArts.Web.Services.Interfaces;
using InstituteOfFineArts.Web.Validation;
using InstituteOfFineArts.Web.ViewModels.Admin.Staff;
using InstituteOfFineArts.Web.ViewModels.Common;
using Microsoft.EntityFrameworkCore;

namespace InstituteOfFineArts.Web.Services.Implementations;

/// <summary>
/// Staff account management for administrators.
/// </summary>
public class StaffService : IStaffService
{
    /// <summary>Duplicate username (TC-M03-02).</summary>
    public const string UsernameTakenMessage = "Username already exists";

    /// <summary>Duplicate email.</summary>
    public const string EmailTakenMessage = "Email already exists";

    /// <summary>Duplicate staff code.</summary>
    public const string CodeTakenMessage = "Staff code already exists";

    /// <summary>BR-03: own role.</summary>
    public const string OwnRoleMessage = "You cannot change your own role.";

    /// <summary>BR-03: own account.</summary>
    public const string SelfMessage = "You cannot lock or delete your own account.";

    /// <summary>BR-03: last administrator.</summary>
    public const string LastAdminMessage = "The last active administrator cannot be locked, deleted or given another role.";

    /// <summary>Only staff roles are managed here.</summary>
    public const string RoleMessage = "Choose STAFF, MANAGER or ADMIN.";

    /// <summary>Database access.</summary>
    private readonly AppDbContext _db;

    /// <summary>Logs account changes (never passwords).</summary>
    private readonly ILogger<StaffService> _logger;

    /// <summary>Receives the required services through Dependency Injection.</summary>
    public StaffService(AppDbContext db, ILogger<StaffService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<PagedResult<StaffListItemVm>> SearchAsync(StaffSearchVm search, int pageSize = 10)
    {
        var query = _db.StaffProfiles.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search.Keyword))
        {
            var k = search.Keyword.Trim();
            query = query.Where(s => s.Account.FullName.Contains(k) || s.Account.Username.Contains(k)
                                     || s.StaffCode.Contains(k) || s.Account.Email.Contains(k));
        }
        if (search.Role is { } role)
        {
            query = query.Where(s => s.Account.Role == role);
        }
        if (!string.IsNullOrWhiteSpace(search.Subject))
        {
            var subject = search.Subject.Trim();
            query = query.Where(s => s.SubjectsInCharge != null && s.SubjectsInCharge.Contains(subject));
        }
        if (!string.IsNullOrWhiteSpace(search.ClassName))
        {
            var className = search.ClassName.Trim();
            query = query.Where(s => s.ClassesInCharge != null && s.ClassesInCharge.Contains(className));
        }
        if (search.Active is { } active)
        {
            query = query.Where(s => s.Account.Active == active);
        }

        var rows = query.OrderBy(s => s.StaffCode).Select(s => new StaffListItemVm
        {
            Id = s.Id,
            StaffCode = s.StaffCode,
            FullName = s.Account.FullName,
            Username = s.Account.Username,
            Email = s.Account.Email,
            Role = s.Account.Role,
            Position = s.Position,
            SubjectsInCharge = s.SubjectsInCharge,
            ClassesInCharge = s.ClassesInCharge,
            Active = s.Account.Active,
        });
        return await PagedResult<StaffListItemVm>.CreateAsync(rows, search.Page, pageSize);
    }

    /// <inheritdoc />
    public async Task<StaffDetailsVm> GetDetailsAsync(long id, long currentAccountId)
    {
        var s = await LoadAsync(id, tracked: false);
        var vm = new StaffDetailsVm
        {
            Active = s.Account.Active,
            LastLoginAt = s.Account.LastLoginAt,
            CreatedAt = s.Account.CreatedAt,
            IsCurrentUser = s.AccountId == currentAccountId,
            CompetitionCount = await _db.Competitions.CountAsync(c => c.CreatedBy == id || c.PublishedBy == id),
            EvaluationCount = await _db.Evaluations.CountAsync(e => e.EvaluatedBy == id),
            AwardCount = await _db.AwardsIssued.CountAsync(a => a.IssuedBy == id),
            ExhibitionCount = await _db.Exhibitions.CountAsync(x => x.CreatedBy == id)
                              + await _db.ExhibitionEntries.CountAsync(e => e.ReviewedBy == id || e.ProposedBy == s.AccountId),
        };
        CopyToForm(s, vm);
        return vm;
    }

    /// <inheritdoc />
    public async Task<StaffFormVm> GetForEditAsync(long id)
    {
        var vm = new StaffFormVm();
        CopyToForm(await LoadAsync(id, tracked: false), vm);
        return vm;
    }

    /// <inheritdoc />
    public async Task<long> CreateAsync(CreateStaffVm form)
    {
        var role = CheckRole(form.Role);
        var username = form.NewUsername.Trim().ToLowerInvariant();
        var email = form.Email.Trim();

        // BR-01: username, email and staff code are unique
        if (await _db.Accounts.AnyAsync(a => a.Username == username))
        {
            throw new BusinessException(UsernameTakenMessage, nameof(CreateStaffVm.NewUsername));
        }
        await EnsureEmailFreeAsync(email, exceptAccountId: null);
        var code = string.IsNullOrWhiteSpace(form.StaffCode) ? await NextCodeAsync() : form.StaffCode.Trim().ToUpperInvariant();
        await EnsureCodeFreeAsync(code, exceptProfileId: null);

        // BR-02 again on the server side
        if (!StrongPasswordAttribute.IsStrong(form.Password))
        {
            throw new BusinessException(new StrongPasswordAttribute().FormatErrorMessage("Password"), nameof(CreateStaffVm.Password));
        }

        // Account and profile are inserted by one SaveChanges, which EF Core runs in a single transaction
        var profile = new StaffProfile
        {
            StaffCode = code,
            Account = new Account
            {
                Username = username,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(form.Password),
                Role = role,
                Active = true,
            },
        };
        Apply(form, profile, email);
        _db.StaffProfiles.Add(profile);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Staff account {Username} ({Role}) created", username, role);
        return profile.Id;
    }

    /// <inheritdoc />
    public async Task UpdateAsync(long id, StaffFormVm form, long currentAccountId)
    {
        var s = await LoadAsync(id, tracked: true);
        var role = CheckRole(form.Role);
        var email = form.Email.Trim();

        // BR-03: nobody changes their own role; the last active admin keeps the ADMIN role
        if (role != s.Account.Role)
        {
            if (s.AccountId == currentAccountId)
            {
                throw new BusinessException(OwnRoleMessage, nameof(StaffFormVm.Role));
            }
            if (s.Account.Role == Role.ADMIN && s.Account.Active && await ActiveAdminCountAsync() <= 1)
            {
                throw new BusinessException(LastAdminMessage, nameof(StaffFormVm.Role));
            }
        }

        await EnsureEmailFreeAsync(email, s.AccountId);
        var code = string.IsNullOrWhiteSpace(form.StaffCode) ? s.StaffCode : form.StaffCode.Trim().ToUpperInvariant();
        await EnsureCodeFreeAsync(code, s.Id);

        s.StaffCode = code;
        s.Account.Role = role;
        Apply(form, s, email);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Staff account {Username} updated", s.Account.Username);
    }

    /// <inheritdoc />
    public async Task ResetPasswordAsync(long id, string newPassword)
    {
        if (!StrongPasswordAttribute.IsStrong(newPassword))
        {
            throw new BusinessException(new StrongPasswordAttribute().FormatErrorMessage("New password"), nameof(ResetPasswordVm.NewPassword));
        }
        var s = await LoadAsync(id, tracked: true);
        s.Account.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Password reset for {Username}", s.Account.Username);
    }

    /// <inheritdoc />
    public async Task SetActiveAsync(long id, bool active, long currentAccountId)
    {
        var s = await LoadAsync(id, tracked: true);
        if (!active)
        {
            await EnsureCanRemoveAsync(s, currentAccountId);
        }
        s.Account.Active = active;
        await _db.SaveChangesAsync();
        _logger.LogInformation("Staff account {Username} {State}", s.Account.Username, active ? "unlocked" : "locked");
    }

    /// <inheritdoc />
    public async Task<StaffDeleteResult> DeleteAsync(long id, long currentAccountId)
    {
        var s = await LoadAsync(id, tracked: true);
        await EnsureCanRemoveAsync(s, currentAccountId);

        // BR-04: with business data the account is only locked, so history keeps the person's name
        if (await HasRelatedDataAsync(s))
        {
            s.Account.Active = false;
            await _db.SaveChangesAsync();
            _logger.LogInformation("Staff account {Username} locked instead of deleted (related data)", s.Account.Username);
            return StaffDeleteResult.Deactivated;
        }

        _db.StaffProfiles.Remove(s);
        _db.Accounts.Remove(s.Account);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Staff account {Username} deleted", s.Account.Username);
        return StaffDeleteResult.Deleted;
    }

    /// <summary>BR-03 for lock and delete: not yourself, not the last active administrator.</summary>
    private async Task EnsureCanRemoveAsync(StaffProfile s, long currentAccountId)
    {
        if (s.AccountId == currentAccountId)
        {
            throw new BusinessException(SelfMessage);
        }
        if (s.Account.Role == Role.ADMIN && s.Account.Active && await ActiveAdminCountAsync() <= 1)
        {
            throw new BusinessException(LastAdminMessage);
        }
    }

    /// <summary>True when the person created, evaluated, awarded or handled exhibition data.</summary>
    private async Task<bool> HasRelatedDataAsync(StaffProfile s)
        => await _db.Competitions.AnyAsync(c => c.CreatedBy == s.Id || c.PublishedBy == s.Id)
           || await _db.Evaluations.AnyAsync(e => e.EvaluatedBy == s.Id)
           || await _db.AwardsIssued.AnyAsync(a => a.IssuedBy == s.Id)
           || await _db.Exhibitions.AnyAsync(x => x.CreatedBy == s.Id)
           || await _db.ExhibitionEntries.AnyAsync(e => e.ReviewedBy == s.Id || e.ProposedBy == s.AccountId);

    /// <summary>Number of active ADMIN accounts.</summary>
    private Task<int> ActiveAdminCountAsync() => _db.Accounts.CountAsync(a => a.Role == Role.ADMIN && a.Active);

    /// <summary>Email unique across all accounts, ignoring case (BR-01).</summary>
    private async Task EnsureEmailFreeAsync(string email, long? exceptAccountId)
    {
        var lower = email.ToLower();
        if (await _db.Accounts.AnyAsync(a => a.Email.ToLower() == lower && a.Id != exceptAccountId))
        {
            throw new BusinessException(EmailTakenMessage, nameof(StaffFormVm.Email));
        }
    }

    /// <summary>Staff code unique (BR-01).</summary>
    private async Task EnsureCodeFreeAsync(string code, long? exceptProfileId)
    {
        if (await _db.StaffProfiles.AnyAsync(s => s.StaffCode == code && s.Id != exceptProfileId))
        {
            throw new BusinessException(CodeTakenMessage, nameof(StaffFormVm.StaffCode));
        }
    }

    /// <summary>Next free code NVnnnn after the highest existing NV number.</summary>
    private async Task<string> NextCodeAsync()
    {
        var codes = await _db.StaffProfiles.Where(s => s.StaffCode.StartsWith("NV")).Select(s => s.StaffCode).ToListAsync();
        var max = codes.Select(c => int.TryParse(c[2..], out var n) ? n : 0).DefaultIfEmpty(0).Max();
        return $"NV{max + 1:D4}";
    }

    /// <summary>Only STAFF, MANAGER and ADMIN are allowed here.</summary>
    private static Role CheckRole(Role? role)
        => role is Role.STAFF or Role.MANAGER or Role.ADMIN
            ? role.Value
            : throw new BusinessException(RoleMessage, nameof(StaffFormVm.Role));

    /// <summary>Loads a profile with its account or throws NotFoundException.</summary>
    private async Task<StaffProfile> LoadAsync(long id, bool tracked)
    {
        var query = _db.StaffProfiles.Include(s => s.Account).AsQueryable();
        if (!tracked)
        {
            query = query.AsNoTracking();
        }
        return await query.FirstOrDefaultAsync(s => s.Id == id)
               ?? throw new NotFoundException($"Staff member {id} was not found.");
    }

    /// <summary>Copies the editable form fields onto the profile and its account.</summary>
    private static void Apply(StaffFormVm form, StaffProfile s, string email)
    {
        s.Account.FullName = form.FullName.Trim();
        s.Account.Email = email;
        s.Account.Gender = form.Gender;
        s.Account.DateOfBirth = form.DateOfBirth;
        s.Account.Phone = Clean(form.Phone);
        s.Account.Address = Clean(form.Address);
        s.JoinedDate = form.JoinedDate ?? throw new BusinessException("Please choose the joined date", nameof(StaffFormVm.JoinedDate));
        s.Position = Clean(form.Position);
        s.ClassesInCharge = Clean(form.ClassesInCharge);
        s.SubjectsInCharge = Clean(form.SubjectsInCharge);
        s.Qualification = Clean(form.Qualification);
        s.Notes = Clean(form.Notes);
    }

    /// <summary>Copies a profile into a form / details view model.</summary>
    private static void CopyToForm(StaffProfile s, StaffFormVm vm)
    {
        vm.Id = s.Id;
        vm.Username = s.Account.Username;
        vm.FullName = s.Account.FullName;
        vm.Email = s.Account.Email;
        vm.Role = s.Account.Role;
        vm.Gender = s.Account.Gender;
        vm.DateOfBirth = s.Account.DateOfBirth;
        vm.Phone = s.Account.Phone;
        vm.Address = s.Account.Address;
        vm.StaffCode = s.StaffCode;
        vm.JoinedDate = s.JoinedDate;
        vm.Position = s.Position;
        vm.ClassesInCharge = s.ClassesInCharge;
        vm.SubjectsInCharge = s.SubjectsInCharge;
        vm.Qualification = s.Qualification;
        vm.Notes = s.Notes;
    }

    /// <summary>Trims text; blank becomes null.</summary>
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
