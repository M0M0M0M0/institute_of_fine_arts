using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using InstituteOfFineArts.Web.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace InstituteOfFineArts.Web.Infrastructure.Api;

/// <summary>
/// JSON Web API for the Flutter mobile app (folder mobile/): JWT bearer login next to the web's cookie login,
/// CORS for the app running in a browser, and ProblemDetails JSON instead of the HTML error pages under /api.
/// The API reuses the same services as the web screens, so every business rule stays in one place.
/// </summary>
public static class ApiSetup
{
    /// <summary>Path prefix of every API endpoint.</summary>
    public const string PathPrefix = "/api";

    /// <summary>CORS policy applied to the API controllers.</summary>
    public const string CorsPolicy = "MobileApp";

    /// <summary>True for a request to the JSON API.</summary>
    public static bool IsApiRequest(this HttpContext context) => context.Request.Path.StartsWithSegments(PathPrefix);

    /// <summary>Registers JWT bearer authentication, the token issuer and CORS.</summary>
    public static void AddMobileApi(this WebApplicationBuilder builder)
    {
        var options = new JwtOptions
        {
            Issuer = "institute-of-fine-arts",
            Audience = "ifa-mobile",
            Lifetime = TimeSpan.FromDays(builder.Configuration.GetValue("Jwt:LifetimeDays", 7)),
            SigningKey = LoadSigningKey(builder),
        };
        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton<JwtTokenIssuer>();

        builder.Services.AddAuthentication()
            .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, jwt =>
            {
                // Keep the claim types exactly as issued, so User.GetAccountId(), IsInRole… work like with the cookie
                jwt.MapInboundClaims = false;
                jwt.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true, ValidIssuer = options.Issuer,
                    ValidateAudience = true, ValidAudience = options.Audience,
                    ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(options.SigningKey),
                    ValidateLifetime = true, ClockSkew = TimeSpan.FromMinutes(1),
                    NameClaimType = ClaimTypes.Name, RoleClaimType = ClaimTypes.Role,
                };
                jwt.Events = new JwtBearerEvents
                {
                    // A token of a locked or deleted account stops working at once (BR-05), not when it expires
                    OnTokenValidated = async context =>
                    {
                        var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
                        var id = long.TryParse(context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var x) ? x : 0;
                        if (!await db.Accounts.AnyAsync(a => a.Id == id && a.Active))
                        {
                            context.Fail("The account is locked or no longer exists.");
                        }
                    },
                    // JSON instead of the HTML login redirect / error page
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();
                        await WriteProblemAsync(context.HttpContext, StatusCodes.Status401Unauthorized, "Please sign in again.");
                    },
                    OnForbidden = context =>
                        WriteProblemAsync(context.HttpContext, StatusCodes.Status403Forbidden, "You do not have permission to do this."),
                };
            });

        // The app sends its token in the Authorization header (no cookies), so any origin may call the API
        builder.Services.AddCors(cors => cors.AddPolicy(CorsPolicy, policy =>
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod().WithExposedHeaders("Content-Disposition")));
    }

    /// <summary>
    /// Error handling: /api requests get ProblemDetails JSON, every other request keeps the friendly HTML error pages.
    /// </summary>
    public static void UseErrorHandling(this WebApplication app)
    {
        app.UseWhen(c => c.IsApiRequest(), api => api.UseExceptionHandler(handler => handler.Run(context =>
            WriteProblemAsync(context, StatusCodes.Status500InternalServerError, "An unexpected error occurred. Please try again."))));
        app.UseWhen(c => !c.IsApiRequest(), web =>
        {
            web.UseExceptionHandler("/error/500");
            web.UseStatusCodePagesWithReExecute("/error/{0}");
        });
    }

    /// <summary>Writes a ProblemDetails JSON response (title = message shown by the app).</summary>
    public static Task WriteProblemAsync(HttpContext context, int status, string title)
    {
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";
        var problem = new ProblemDetails { Status = status, Title = title };
        return context.Response.WriteAsync(JsonSerializer.Serialize(problem, JsonSerializerOptions.Web));
    }

    /// <summary>
    /// Signing key: Jwt:SigningKey (base64, at least 32 bytes) from User Secrets / environment variables, otherwise a
    /// random key created once and kept in App_Data/jwt-signing.key (git-ignored) so tokens survive a restart.
    /// </summary>
    private static byte[] LoadSigningKey(WebApplicationBuilder builder)
    {
        var configured = builder.Configuration["Jwt:SigningKey"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            var bytes = Convert.FromBase64String(configured);
            return bytes.Length >= 32 ? bytes : throw new InvalidOperationException("Jwt:SigningKey must be at least 32 bytes (base64).");
        }
        var path = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "jwt-signing.key");
        if (File.Exists(path))
        {
            var stored = Convert.FromBase64String(File.ReadAllText(path).Trim());
            if (stored.Length >= 32) return stored;
        }
        var key = RandomNumberGenerator.GetBytes(64);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, Convert.ToBase64String(key), Encoding.ASCII);
        return key;
    }
}

/// <summary>Settings of the tokens given to the mobile app.</summary>
public sealed class JwtOptions
{
    public string Issuer { get; init; } = string.Empty;
    public string Audience { get; init; } = string.Empty;
    public TimeSpan Lifetime { get; init; }
    public byte[] SigningKey { get; init; } = [];
}
