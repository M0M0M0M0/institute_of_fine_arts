using InstituteOfFineArts.Web.Data.Seed;
using SkiaSharp;

namespace InstituteOfFineArts.Tests.Data;

/// <summary>
/// Generated sample paintings: valid JPEG of the requested size, deterministic per seed.
/// </summary>
public class SampleArtworkTests
{
    /// <summary>Every theme produces a decodable JPEG of the requested size.</summary>
    [Theory]
    [MemberData(nameof(Themes))]
    public void Create_ValidJpeg(SampleArtwork.Theme theme)
    {
        using var codec = SKCodec.Create(new SKMemoryStream(SampleArtwork.Create(theme, 7, 800, 600)));
        Assert.Equal(SKEncodedImageFormat.Jpeg, codec.EncodedFormat);
        Assert.Equal(800, codec.Info.Width);
        Assert.Equal(600, codec.Info.Height);
    }

    /// <summary>Same seed → same image; different seed → different image.</summary>
    [Fact]
    public void Create_Deterministic()
    {
        var a = SampleArtwork.Create(SampleArtwork.Theme.Landscape, 1, 400, 300);
        Assert.Equal(a, SampleArtwork.Create(SampleArtwork.Theme.Landscape, 1, 400, 300));
        Assert.NotEqual(a, SampleArtwork.Create(SampleArtwork.Theme.Landscape, 2, 400, 300));
    }

    /// <summary>All themes.</summary>
    public static IEnumerable<object[]> Themes() => Enum.GetValues<SampleArtwork.Theme>().Select(t => new object[] { t });
}
