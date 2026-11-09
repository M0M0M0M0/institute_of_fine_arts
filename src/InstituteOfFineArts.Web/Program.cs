using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using InstituteOfFineArts.Web.Data;
using InstituteOfFineArts.Web.Data.Seed;
using InstituteOfFineArts.Web.Infrastructure.DependencyInjection;
using InstituteOfFineArts.Web.Infrastructure.FileStorage;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.WebEncoders;

var builder = WebApplication.CreateBuilder(args);

// ===== Service registration (Dependency Injection) =====

// MVC: controllers + Razor views; validate the anti-forgery token on every POST form (CSRF protection)
builder.Services.AddControllersWithViews(options =>
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));

// Keep non-ASCII characters (e.g. Vietnamese text entered by users) as-is in the generated HTML
// instead of &#x...; entities. Dangerous characters < > & " ' are still encoded, so XSS protection is unchanged.
builder.Services.Configure<WebEncoderOptions>(options =>
    options.TextEncoderSettings = new TextEncoderSettings(UnicodeRanges.All));

// Cookie authentication (M01) – design: docs/plan/02 section 7.1
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        // Not signed in → login page; wrong role → 403 page
        options.LoginPath = "/login";
        options.LogoutPath = "/logout";
        options.AccessDeniedPath = "/error/403";

        // Expires after 30 minutes of inactivity; every request extends it
        options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
        options.SlidingExpiration = true;

        // Cookie readable by the server only (cannot be stolen by JavaScript)
        options.Cookie.Name = "IFA.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
    });

// Upload size limit (BR-17, task BE1-03): images ≤ Upload:MaxFileSizeBytes (5 MB) plus 1 MB for the other form fields.
// Applied to every request body (Kestrel and IIS) and to multipart forms; a larger request shows the 413 error page.
var maxRequestBytes = builder.Configuration.GetValue<long>("Upload:MaxFileSizeBytes", 5 * 1024 * 1024) + 1024 * 1024;
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = maxRequestBytes);
builder.Services.Configure<IISServerOptions>(options => options.MaxRequestBodySize = maxRequestBytes);
builder.Services.Configure<FormOptions>(options => options.MultipartBodyLengthLimit = maxRequestBytes);

// Database: EF Core + SQL Server; table/column names converted to snake_case (account, password_hash…)
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"))
           .UseSnakeCaseNamingConvention());

// Business services: every XxxService class in Services/Implementations is registered automatically
// → adding a new service does NOT require editing this file
builder.Services.AddApplicationServices();

// Upload settings (root folder, 5 MB limit, thumbnail width) used by FileStorageService – task BE1-04
builder.Services.Configure<UploadOptions>(builder.Configuration.GetSection(UploadOptions.SectionName));

// E-mail settings (SMTP or .eml pickup folder) used by EmailService – task BE1-OPT
builder.Services.Configure<InstituteOfFineArts.Web.Infrastructure.Email.EmailOptions>(builder.Configuration.GetSection("Email"));

// Background jobs: every BackgroundService in Infrastructure/BackgroundJobs is registered automatically
builder.Services.AddBackgroundJobs();

var app = builder.Build();

// Create the upload root (outside wwwroot) at start-up so the first upload does not fail
var uploadOptions = builder.Configuration.GetSection(UploadOptions.SectionName).Get<UploadOptions>() ?? new UploadOptions();
Directory.CreateDirectory(uploadOptions.ResolveRoot(app.Environment.ContentRootPath));

// ===== Request pipeline =====

// Development: create the database + sample data (Data/Seed/DbSeeder.cs)
// Production: HSTS (browsers keep using HTTPS)
if (app.Environment.IsDevelopment())
{
    await DbSeeder.SeedAsync(app.Services);
}
else
{
    app.UseHsts();
}

// Friendly error pages in every environment (F-M01-07): unhandled exception → /error/500 (logged; details shown
// on the page in Development only); empty 4xx/5xx responses such as 404 → /error/{code}. See ErrorController.
app.UseExceptionHandler("/error/500");
app.UseStatusCodePagesWithReExecute("/error/{0}");

// Reject oversized requests up front with 413 (→ "File too large" page). Without this, the anti-forgery check
// reads the form first, hits the size limit and answers 400 "Bad request". Requests without a Content-Length
// (chunked) are still stopped by the Kestrel/IIS limit above.
// Skipped while the error page itself is being rendered (re-execute keeps the original Content-Length).
app.Use(async (context, next) =>
{
    if (context.Request.ContentLength > maxRequestBytes
        && context.Features.Get<IStatusCodeReExecuteFeature>() == null)
    {
        context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
        return;
    }
    await next();
});

// The UI is English: fix the culture to en-US so numbers, dates and USD amounts are parsed and
// formatted the same way on every machine, even when Windows uses another language (e.g. vi-VN uses "," for decimals).
// Vietnamese UI is an optional feature (M19); vi-VN will be added to the supported cultures then.
var english = new CultureInfo("en-US");
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(english),
    SupportedCultures = new[] { english },
    SupportedUICultures = new[] { english },
});

app.UseHttpsRedirection();
app.UseRouting();

// Read the login cookie → User; must come before UseAuthorization
app.UseAuthentication();
// Enforce [Authorize(Roles = ...)] on controllers
app.UseAuthorization();

// Static files in wwwroot (css, js, lib, img)
app.MapStaticAssets();

// Route for the areas (Admin, Staff, Student, Manager) – must come before the default route
app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}")
    .WithStaticAssets();

// Default route for shared controllers (Home, Account, Files…)
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
