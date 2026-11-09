namespace InstituteOfFineArts.Web.Services.Interfaces;

/// <summary>
/// Forgot password with a one-time e-mail link (optional feature F-M16-01, task BE1-OPT).
/// </summary>
public interface IPasswordResetService
{
    /// <summary>
    /// Handles a "forgot password" request. When an active account matches the user name or e-mail (and the
    /// account is under the hourly request limit), a new 30-minute token is stored (hash only), older unused tokens
    /// are invalidated and the reset link is e-mailed. Nothing tells the caller whether the account exists.
    /// </summary>
    /// <param name="userNameOrEmail">User name or e-mail typed on the form</param>
    /// <param name="requestBaseUrl">Site address of the current request, used only when Email:AppBaseUrl is not configured</param>
    /// <param name="cancellationToken">Cancellation</param>
    Task RequestResetAsync(string userNameOrEmail, string requestBaseUrl, CancellationToken cancellationToken = default);

    /// <summary>True when the token exists, is unused, has not expired and belongs to an active account.</summary>
    /// <param name="token">Token from the e-mail link</param>
    Task<bool> IsTokenValidAsync(string? token);

    /// <summary>
    /// Sets a new password with a valid token and marks the token as used.
    /// Throws BusinessException when the link is invalid/expired/used, the account is locked or the password is weak.
    /// </summary>
    /// <param name="token">Token from the e-mail link</param>
    /// <param name="newPassword">New password (BR-02)</param>
    Task ResetPasswordAsync(string? token, string newPassword);
}
