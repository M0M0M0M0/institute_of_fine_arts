using System.Text.RegularExpressions;
using InstituteOfFineArts.Web.Data.Seed;

namespace InstituteOfFineArts.Tests.Data;

/// <summary>
/// The sample pictures in database/seed-images (QA-01) match what DbSeeder.SampleData.cs loads:
/// every entry file and cover exists, is a JPEG of at most 1 MB, and has a credit line.
/// </summary>
public class SeedImagesTests
{
    /// <summary>Repository root, found from this source file upwards (works whatever the build output folder is).</summary>
    private static string RepoRoot([System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "")
    {
        for (var dir = new DirectoryInfo(Path.GetDirectoryName(sourceFile)!); dir != null; dir = dir.Parent)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "database", "seed-images"))
                && Directory.Exists(Path.Combine(dir.FullName, "src", "InstituteOfFineArts.Web")))
            {
                return dir.FullName;
            }
        }
        throw new DirectoryNotFoundException("Repository root not found");
    }

    /// <summary>Seed files the seeder asks for: every "xxx.jpg" entry name plus a cover for the first six competitions.</summary>
    private static List<string> ExpectedFiles(string root)
    {
        var source = File.ReadAllText(Path.Combine(root, "src", "InstituteOfFineArts.Web", "Data", "Seed", "DbSeeder.SampleData.cs"));
        var entries = Regex.Matches(source, "\"([a-z0-9-]+\\.jpg)\"").Select(m => m.Groups[1].Value).Distinct().ToList();
        var covers = new[] { "Hometown Landscapes", "Wildlife", "City at Night", "Colors of Autumn", "Deadline Today", "Lunar New Year 2027" }
            .Select(t => $"cover-{DbSeeder.Slug(t)}.jpg");
        return entries.Concat(covers).ToList();
    }

    /// <summary>All 27 entries and 6 covers have a real picture: JPEG, at most 1 MB.</summary>
    [Fact]
    public void EveryExpectedFile_IsASmallJpeg()
    {
        var folder = Path.Combine(RepoRoot(), "database", "seed-images");
        var expected = ExpectedFiles(RepoRoot());

        Assert.Equal(33, expected.Count);
        foreach (var name in expected)
        {
            var path = Path.Combine(folder, name);
            Assert.True(File.Exists(path), $"missing seed image {name}");
            var bytes = File.ReadAllBytes(path);
            Assert.True(bytes.Length <= 1024 * 1024, $"{name} is larger than 1 MB");
            Assert.True(bytes[0] == 0xFF && bytes[1] == 0xD8, $"{name} is not a JPEG");
        }
    }

    /// <summary>Every seed picture is credited (source, author, link, licence).</summary>
    [Fact]
    public void EveryFile_HasACreditLine()
    {
        var folder = Path.Combine(RepoRoot(), "database", "seed-images");
        var credits = File.ReadAllLines(Path.Combine(folder, "CREDITS.txt"));
        foreach (var file in Directory.GetFiles(folder, "*.jpg").Select(Path.GetFileName))
        {
            Assert.Contains(credits, l => l.StartsWith(file + " |") && l.Contains("Pixabay") && l.Contains("http"));
        }
    }

    /// <summary>Cover file names come from the competition title.</summary>
    [Fact]
    public void Slug_FromTitle()
    {
        Assert.Equal("lunar-new-year-2027", DbSeeder.Slug("Lunar New Year 2027"));
        Assert.Equal("city-at-night", DbSeeder.Slug("City at Night"));
    }
}
