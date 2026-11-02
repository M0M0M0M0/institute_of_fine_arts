using InstituteOfFineArts.Web.Models.Enums;

namespace InstituteOfFineArts.Web.Models.Entities;

/// <summary>
/// Login account plus common personal details (table account).
/// </summary>
public class Account : BaseEntity
{
    /// <summary>Login name, unique (BR-01).</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>BCrypt password hash – the plain password is never stored (BR-02).</summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>Full name.</summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>Email, unique (BR-01).</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Phone number.</summary>
    public string? Phone { get; set; }

    /// <summary>Date of birth.</summary>
    public DateOnly? DateOfBirth { get; set; }

    /// <summary>Gender.</summary>
    public Gender? Gender { get; set; }

    /// <summary>Address.</summary>
    public string? Address { get; set; }

    /// <summary>Role – exactly one role per account (BR-03).</summary>
    public Role Role { get; set; }

    /// <summary>false = account locked/deactivated instead of being deleted (BR-04, BR-05).</summary>
    public bool Active { get; set; } = true;

    /// <summary>Time of the latest successful login.</summary>
    public DateTime? LastLoginAt { get; set; }

    /// <summary>Staff profile (when the role is ADMIN, MANAGER or STAFF).</summary>
    public StaffProfile? StaffProfile { get; set; }

    /// <summary>Student profile (when the role is STUDENT).</summary>
    public StudentProfile? StudentProfile { get; set; }
}
