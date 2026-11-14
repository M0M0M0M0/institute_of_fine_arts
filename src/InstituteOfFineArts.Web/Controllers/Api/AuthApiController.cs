using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using InstituteOfFineArts.Web.Data;
using InstituteOfFineArts.Web.Infrastructure.Api;
using InstituteOfFineArts.Web.Infrastructure.Security;
using InstituteOfFineArts.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InstituteOfFineArts.Web.Controllers.Api;

/// <summary>Mobile app login (JWT): POST /api/auth/login, GET /api/auth/me. Same accounts and rules as the web login.</summary>
[Route("api/auth")]
public class AuthApiController : ApiControllerBase
{
    private readonly IAuthService _auth;
    private readonly JwtTokenIssuer _tokens;
    private readonly AppDbContext _db;

    public AuthApiController(IAuthService auth, JwtTokenIssuer tokens, AppDbContext db)
    {
        _auth = auth;
        _tokens = tokens;
        _db = db;
    }

    /// <summary>Body of POST /api/auth/login.</summary>
    public sealed class LoginRequest
    {
        [Required(ErrorMessage = "Please enter your username.")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please enter your password.")]
        public string Password { get; set; } = string.Empty;
    }

    /// <summary>Signed-in user as the app reads it (AppUser.fromJson).</summary>
    public sealed record UserDto(long AccountId, string Username, string FullName, string Role);

    /// <summary>POST /api/auth/login → { token, expiresAt, user }; 401 for a wrong username/password, 403 for a locked account.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var result = await _auth.LoginAsync(request.Username, request.Password, JwtBearerDefaults.AuthenticationScheme);
        switch (result.Status)
        {
            case LoginStatus.Locked:
                return Problem(StatusCodes.Status403Forbidden, "Your account is locked. Please contact the administrator.");
            case LoginStatus.Success when result.Principal != null:
                var (token, expiresAt) = _tokens.Issue(result.Principal);
                return Ok(new { token, expiresAt, user = ToDto(result.Principal) });
            default:
                return Problem(StatusCodes.Status401Unauthorized, "Incorrect username or password.");
        }
    }

    /// <summary>GET /api/auth/me → the user of the token (401 when expired or the account was locked).</summary>
    [HttpGet("me")]
    public async Task<ActionResult<UserDto>> Me()
    {
        var id = User.GetAccountId();
        var account = await _db.Accounts.AsNoTracking().FirstAsync(a => a.Id == id);
        return new UserDto(account.Id, account.Username, account.FullName, account.Role.ToString());
    }

    /// <summary>User from the login principal's claims.</summary>
    private static UserDto ToDto(ClaimsPrincipal p) => new(
        p.GetAccountId(), p.FindFirstValue(ClaimTypes.Name) ?? string.Empty, p.GetFullName(), p.FindFirstValue(ClaimTypes.Role) ?? string.Empty);
}
