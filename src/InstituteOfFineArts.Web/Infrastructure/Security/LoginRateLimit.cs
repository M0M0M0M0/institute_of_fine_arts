namespace InstituteOfFineArts.Web.Infrastructure.Security;

/// <summary>
/// Rate limit of the sign-in endpoints (web /login and POST /api/auth/login), per IP address (review 26/11/2026, B7).
/// Registered in Program.cs; the per-user-name block after wrong passwords is in AuthService.
/// </summary>
public static class LoginRateLimit
{
    /// <summary>Name of the policy for [EnableRateLimiting].</summary>
    public const string PolicyName = "login";

    /// <summary>Sign-in attempts allowed per IP address and minute (generous: a whole class may share one address).</summary>
    public const int PermitsPerMinute = 20;

    /// <summary>Message when the limit is reached.</summary>
    public const string Message = "Too many sign-in attempts from this network. Please wait a minute and try again.";
}
