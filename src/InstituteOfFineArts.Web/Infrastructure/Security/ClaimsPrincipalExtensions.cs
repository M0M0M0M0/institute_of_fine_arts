using System.Security.Claims;

namespace InstituteOfFineArts.Web.Infrastructure.Security;

/// <summary>
/// Helpers that read the signed-in user's information from the authentication cookie (claims).
/// Claims created by AccountController (M01) at login:
/// NameIdentifier = account.id, Name = username, GivenName = full name, Role = role,
/// ProfileId = id of staff_profile or student_profile.
/// </summary>
public static class ClaimsPrincipalExtensions
{
    /// <summary>Name of the claim holding the profile id (staff_profile.id or student_profile.id).</summary>
    public const string ProfileIdClaim = "ProfileId";

    /// <summary>Returns account.id of the signed-in user; throws if the user is not signed in.</summary>
    public static long GetAccountId(this ClaimsPrincipal user)
    {
        // Read the NameIdentifier claim and convert it to a number
        var value = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return long.TryParse(value, out var id)
            ? id
            : throw new InvalidOperationException("The user is not signed in.");
    }

    /// <summary>
    /// Returns the profile id: staff_profile.id for ADMIN/MANAGER/STAFF, student_profile.id for STUDENT.
    /// Used when writing created_by, evaluated_by, student_id…
    /// </summary>
    public static long GetProfileId(this ClaimsPrincipal user)
    {
        // Read the ProfileId claim and convert it to a number
        var value = user.FindFirstValue(ProfileIdClaim);
        return long.TryParse(value, out var id)
            ? id
            : throw new InvalidOperationException("The user has no profile.");
    }

    /// <summary>Returns the display name (full name) of the signed-in user.</summary>
    public static string GetFullName(this ClaimsPrincipal user)
        => user.FindFirstValue(ClaimTypes.GivenName) ?? user.Identity?.Name ?? string.Empty;
}
