using System.Security.Claims;

namespace InstituteOfFineArts.Web.Services.Interfaces;

/// <summary>
/// Result of a login attempt.
/// </summary>
public enum LoginStatus
{
    /// <summary>Correct username and password, account is active.</summary>
    Success,

    /// <summary>Wrong username or wrong password (deliberately not saying which one).</summary>
    InvalidCredentials,

    /// <summary>Correct password but the account is locked (BR-05).</summary>
    Locked,

    /// <summary>
    /// Too many failed attempts for this user name in a short time: refused without checking the password, so a
    /// password cannot be guessed by trying many (review 26/11/2026, B7).
    /// </summary>
    TooManyAttempts,
}

/// <summary>
/// Result returned to AccountController after checking the login.
/// </summary>
/// <param name="Status">Login status</param>
/// <param name="Principal">User information (claims) used to create the cookie – only when Status = Success</param>
public record LoginResult(LoginStatus Status, ClaimsPrincipal? Principal = null);

/// <summary>
/// Authenticates users against the account table (task BE1-02, module M01).
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Checks username + password; on success updates the last login time
    /// and returns a ClaimsPrincipal so the controller can create the cookie.
    /// </summary>
    /// <param name="username">Username typed by the user</param>
    /// <param name="password">Password typed by the user (plain text)</param>
    /// <param name="authenticationScheme">Authentication scheme name used for the ClaimsIdentity</param>
    Task<LoginResult> LoginAsync(string username, string password, string authenticationScheme);

    /// <summary>
    /// Home page path of each role after login
    /// (ADMIN → /admin, STAFF → /staff, TEACHER → /teacher, STUDENT → /student).
    /// </summary>
    /// <param name="user">Signed-in user</param>
    string GetHomePath(ClaimsPrincipal user);
}
