namespace InstituteOfFineArts.Web.Data.Seed;

/// <summary>
/// Sample data part: competitions, submissions, evaluations, awards, exhibitions, customers (task BE2-01).
/// Runs after SeedAccountsAsync, so staff and student accounts already exist.
/// </summary>
public static partial class DbSeeder
{
    /// <summary>
    /// Inserts sample business data so modules can be built in parallel without waiting for each other.
    /// </summary>
    /// <param name="db">DbContext used to insert the data</param>
    private static async Task SeedSampleDataAsync(AppDbContext db)
    {
        // TODO (BE2-01): follow docs/plan/04 sections 4.2 – 4.4 (all data in English, prices in USD)
        //   (1) 7 competitions with dates relative to DateTimeHelper.Today, plus their awards
        //   (2) 27 submissions; copy images from database/seed-images into the uploads folder
        //   (3) evaluations covering all 6 ratings; issued awards; publish the GRADED competitions
        //   (4) 3 customers, 2 exhibitions / 8 entries
        //   Call await db.SaveChangesAsync() after each group of data
        await db.SaveChangesAsync();
    }
}
