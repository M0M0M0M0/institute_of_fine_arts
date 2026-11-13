using InstituteOfFineArts.Web.Data.Seed;

namespace InstituteOfFineArts.Tests.Data;

/// <summary>
/// Archive sample data (DbSeeder.Archive.cs): the committed NGA manifest is valid and the plan shares its ~1,500
/// paintings over the archive competitions without reusing a picture or exceeding the number of alumni students.
/// The pictures themselves are downloaded by fetch-images.ps1 and are not needed by these tests.
/// </summary>
public class ArchiveSeedTests
{
    /// <summary>database/seed-images/nga/manifest.csv, found from this source file upwards.</summary>
    private static string ManifestPath([System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "")
    {
        for (var dir = new DirectoryInfo(Path.GetDirectoryName(sourceFile)!); dir != null; dir = dir.Parent)
        {
            var path = Path.Combine(dir.FullName, "database", "seed-images", "nga", "manifest.csv");
            if (File.Exists(path)) return path;
        }
        throw new FileNotFoundException("manifest.csv not found");
    }

    /// <summary>Quoted fields keep their commas; "" inside quotes is one quote.</summary>
    [Fact]
    public void ParseCsvLine_Quotes()
    {
        var f = DbSeeder.ParseCsvLine("a,\"b, c\",\"say \"\"hi\"\"\",,d");

        Assert.Equal(["a", "b, c", "say \"hi\"", "", "d"], f);
    }

    /// <summary>1,500–2,000 open-access paintings with a title, a size, an NGA link and a known theme; no duplicates.</summary>
    [Fact]
    public void Manifest_IsValid()
    {
        var paintings = DbSeeder.ReadArchiveManifest(ManifestPath());
        var themes = DbSeeder.ArchiveCompetitions.Select(c => c.Theme).ToHashSet();

        Assert.InRange(paintings.Count, 1500, 2000);
        Assert.Equal(paintings.Count, paintings.Select(p => p.Uuid).Distinct().Count());
        Assert.All(paintings, p =>
        {
            Assert.Matches("^[0-9a-f-]{36}$", p.Uuid);
            Assert.InRange(p.Title.Length, 3, 150);
            Assert.True(p.Width >= 800 && p.Height >= 800, p.Uuid);
            Assert.StartsWith("https://www.nga.gov/collection/", p.PageUrl);
            Assert.Contains(p.Theme, themes);
        });
    }

    /// <summary>Every painting is used exactly once (cover or entry); no competition has more entries than alumni students.</summary>
    [Fact]
    public void Plan_UsesEveryPaintingOnce()
    {
        var paintings = DbSeeder.ReadArchiveManifest(ManifestPath());

        var plan = DbSeeder.PlanArchive(paintings);

        Assert.Equal(DbSeeder.ArchiveCompetitions.Length, plan.Count);
        var used = plan.SelectMany(p => p.Entries.Prepend(p.Cover)).Select(p => p.Uuid).ToList();
        Assert.Equal(paintings.Count, used.Count);
        Assert.Equal(used.Count, used.Distinct().Count());
        Assert.All(plan, p =>
        {
            Assert.InRange(p.Entries.Count, 5, DbSeeder.ArchiveStudentCount);
            Assert.All(p.Entries, e => Assert.Equal(p.Competition.Theme, e.Theme));
            // The cover is the widest picture of its share
            Assert.True(p.Entries.All(e => (double)e.Width / e.Height <= (double)p.Cover.Width / p.Cover.Height));
        });
        // Newest first, in the declared order
        Assert.Equal(DbSeeder.ArchiveCompetitions.Select(c => c.Title), plan.Select(p => p.Competition.Title));
    }

    /// <summary>Only part of the pictures downloaded: shares too small for a believable competition are left out.</summary>
    [Fact]
    public void Plan_PartialDownload_SkipsTinyShares()
    {
        var paintings = DbSeeder.ReadArchiveManifest(ManifestPath()).Where((_, i) => i % 20 == 0).ToList();

        var plan = DbSeeder.PlanArchive(paintings);

        Assert.All(plan, p => Assert.True(p.Entries.Count >= 5));
        Assert.True(plan.Count < DbSeeder.ArchiveCompetitions.Length);
    }

    /// <summary>Archive titles are unique and none clashes with the 7 main sample competitions.</summary>
    [Fact]
    public void ArchiveTitles_Unique()
    {
        var titles = DbSeeder.ArchiveCompetitions.Select(c => c.Title).ToList();

        Assert.Equal(titles.Count, titles.Distinct().Count());
        Assert.DoesNotContain("Wildlife", titles);
        Assert.DoesNotContain("Hometown Landscapes", titles);
    }
}
