using System.Security.Claims;
using InstituteOfFineArts.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace InstituteOfFineArts.Web.Infrastructure.Security;

/// <summary>
/// Checks on every request that a session (web cookie or mobile JWT) still belongs to an active account whose security
/// stamp has not changed since sign-in. Locking an account, changing its role or its password renews the stamp
/// (AppDbContext), so the session of a locked or demoted user ends at once instead of living on until it expires
/// (BR-05; review 26/11/2026, A1). One small primary-key query per request.
/// </summary>
public static class SessionValidator
{
    /// <summary>True when <paramref name="user"/> is an active account with the security stamp it signed in with.</summary>
    public static async Task<bool> IsCurrentAsync(AppDbContext db, ClaimsPrincipal? user, CancellationToken cancellationToken = default)
    {
        if (user == null || !long.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id))
        {
            return false;
        }
        // A session from before the stamp existed has no claim: it ends once and the user signs in again
        var stamp = user.FindFirstValue(ClaimsPrincipalExtensions.SecurityStampClaim);
        if (string.IsNullOrEmpty(stamp))
        {
            return false;
        }
        return await db.Accounts.AsNoTracking()
            .AnyAsync(a => a.Id == id && a.Active && a.SecurityStamp == stamp, cancellationToken);
    }
}
