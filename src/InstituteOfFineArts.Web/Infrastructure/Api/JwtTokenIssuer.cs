using System.Security.Claims;
using InstituteOfFineArts.Web.Helpers;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace InstituteOfFineArts.Web.Infrastructure.Api;

/// <summary>Creates the JWT the mobile app sends as "Authorization: Bearer …" (same claims as the login cookie).</summary>
public sealed class JwtTokenIssuer
{
    private readonly JwtOptions _options;

    public JwtTokenIssuer(JwtOptions options) => _options = options;

    /// <summary>Signed token for the principal built by AuthService.LoginAsync.</summary>
    /// <param name="principal">Signed-in user (NameIdentifier, Name, GivenName, Role, ProfileId claims)</param>
    public (string Token, DateTime ExpiresAt) Issue(ClaimsPrincipal principal)
    {
        var expires = DateTime.UtcNow.Add(_options.Lifetime);
        var token = new JsonWebTokenHandler { SetDefaultTimesOnTokenCreation = true }.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            Expires = expires,
            Subject = new ClaimsIdentity(principal.Claims.Select(c => new Claim(c.Type, c.Value))),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(_options.SigningKey), SecurityAlgorithms.HmacSha256),
        });
        return (token, DateTimeHelper.Now.Add(_options.Lifetime));
    }
}
