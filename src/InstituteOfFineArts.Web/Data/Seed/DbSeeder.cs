using Microsoft.EntityFrameworkCore;

namespace InstituteOfFineArts.Web.Data.Seed;

/// <summary>
/// Creates the database and sample data when running in the Development environment (called from Program.cs).
/// The class is split into several files (partial) so two people can work on it in parallel without conflicts:
/// <list type="bullet">
/// <item><c>DbSeeder.Accounts.cs</c> – sample accounts (task BE1-01)</item>
/// <item><c>DbSeeder.SampleData.cs</c> – competitions, submissions, grading, awards, exhibitions (task BE2-01)</item>
/// <item><c>DbSeeder.Archive.cs</c> – two years of past competitions with ~1,500 real paintings (downloaded separately)</item>
/// </list>
/// </summary>
public static partial class DbSeeder
{
    /// <summary>
    /// Applies pending migrations, then inserts sample data when the database is still empty.
    /// </summary>
    /// <param name="services">Application service provider used to resolve AppDbContext</param>
    public static async Task SeedAsync(IServiceProvider services)
    {
        // AppDbContext is registered as Scoped, so create a dedicated scope
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Create the database (if missing) and apply new migrations
        await db.Database.MigrateAsync();

        // Step 1: accounts (only when the account table is empty)
        if (!await db.Accounts.AnyAsync())
        {
            await SeedAccountsAsync(db);
        }

        // Step 2: business data (only when there is no competition yet)
        if (!await db.Competitions.AnyAsync())
        {
            await SeedSampleDataAsync(db, scope.ServiceProvider);
        }

        // Step 3: archive of past competitions with real paintings (once, only when its pictures were downloaded).
        // A failure is logged and rolled back; the app still starts with the main sample data.
        try
        {
            await SeedArchiveAsync(db, scope.ServiceProvider);
        }
        catch (Exception ex)
        {
            scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DbSeeder.Archive")
                .LogError(ex, "Archive sample data could not be seeded");
        }
    }
}
