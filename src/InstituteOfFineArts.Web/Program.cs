using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using InstituteOfFineArts.Web.Data;
using InstituteOfFineArts.Web.Data.Seed;
using InstituteOfFineArts.Web.Infrastructure.DependencyInjection;
using Microsoft.AspNetCore.Authentication.Cookies;
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

// Database: EF Core + SQL Server; table/column names converted to snake_case (account, password_hash…)
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"))
           .UseSnakeCaseNamingConvention());

// Business services: every XxxService class in Services/Implementations is registered automatically
// → adding a new service does NOT require editing this file
builder.Services.AddApplicationServices();

// Background jobs: every BackgroundService in Infrastructure/BackgroundJobs is registered automatically
builder.Services.AddBackgroundJobs();

var app = builder.Build();

// ===== Request pipeline =====

// Development: create the database + sample data (Data/Seed/DbSeeder.cs)
// Production: friendly error page + HSTS
if (app.Environment.IsDevelopment())
{
    await DbSeeder.SeedAsync(app.Services);
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

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
