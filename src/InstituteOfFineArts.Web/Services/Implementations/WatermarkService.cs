using InstituteOfFineArts.Web.Services.Interfaces;
using SkiaSharp;

namespace InstituteOfFineArts.Web.Services.Implementations;

/// <summary>
/// Watermarks images with SkiaSharp (F-M13-02): the text runs along the diagonal, white and semi-transparent
/// with a faint dark outline so it stays readable on light and dark paintings.
/// </summary>
public class WatermarkService : IWatermarkService
{
    /// <summary>Text drawn on the image.</summary>
    public const string WatermarkText = "Institute of Fine Arts";

    /// <summary>JPEG quality of the watermarked copy (0–100).</summary>
    private const int Quality = 90;

    /// <summary>Share of the image diagonal covered by the text.</summary>
    private const float DiagonalShare = 0.7f;

    /// <summary>Logs images that cannot be decoded.</summary>
    private readonly ILogger<WatermarkService> _logger;

    /// <summary>Receives the logger through Dependency Injection.</summary>
    public WatermarkService(ILogger<WatermarkService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<byte[]?> ApplyAsync(Stream source)
    {
        // SKCodec needs a seekable stream: copy the file into memory (images are at most 5 MB, BR-17)
        using var buffer = new MemoryStream();
        await source.CopyToAsync(buffer);
        buffer.Position = 0;

        // 1. Decode and turn the image upright: re-encoding drops the EXIF orientation the browser would apply
        using var codec = SKCodec.Create(buffer);
        using var decoded = codec == null ? null : SKBitmap.Decode(codec);
        if (codec == null || decoded == null)
        {
            _logger.LogWarning("Watermark skipped: the image could not be decoded");
            return null;
        }
        using var bitmap = ApplyOrientation(decoded, codec.EncodedOrigin);

        // 2. Draw the text on the bitmap
        DrawText(bitmap);

        // 3. Encode as JPEG in memory
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, Quality);
        return data.ToArray();
    }

    /// <summary>Draws the watermark text centred along the diagonal from bottom-left to top-right.</summary>
    private static void DrawText(SKBitmap bitmap)
    {
        using var canvas = new SKCanvas(bitmap);
        // Falls back to the system default font when Arial is not installed
        using var typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold);

        // Size the font so the text covers a fixed share of the diagonal, whatever the image size
        var diagonal = (float)Math.Sqrt((double)bitmap.Width * bitmap.Width + (double)bitmap.Height * bitmap.Height);
        using var font = new SKFont(typeface, 100);
        var widthAt100 = font.MeasureText(WatermarkText);
        font.Size = widthAt100 > 0 ? Math.Max(8f, 100 * diagonal * DiagonalShare / widthAt100) : Math.Max(8f, diagonal / 20);

        // Rotate around the centre so the text follows the diagonal; the baseline is lowered by a third of
        // the font size so the letters are vertically centred
        canvas.Translate(bitmap.Width / 2f, bitmap.Height / 2f);
        canvas.RotateDegrees(-(float)(Math.Atan2(bitmap.Height, bitmap.Width) * 180 / Math.PI));
        var baseline = font.Size / 3;

        // Faint dark outline first, then the white fill on top
        using var outline = new SKPaint
        {
            Color = new SKColor(0, 0, 0, 70), IsAntialias = true, Style = SKPaintStyle.Stroke,
            StrokeWidth = Math.Max(1f, font.Size / 25),
        };
        using var fill = new SKPaint { Color = new SKColor(255, 255, 255, 110), IsAntialias = true, Style = SKPaintStyle.Fill };
        canvas.DrawText(WatermarkText, 0, baseline, SKTextAlign.Center, font, outline);
        canvas.DrawText(WatermarkText, 0, baseline, SKTextAlign.Center, font, fill);
    }

    /// <summary>
    /// Returns a copy rotated according to the EXIF origin (0°, 90°, 180°, 270°), as ThumbnailService does.
    /// Mirrored origins are rare for photos and are kept as they are.
    /// </summary>
    private static SKBitmap ApplyOrientation(SKBitmap source, SKEncodedOrigin origin)
    {
        var degrees = origin switch
        {
            SKEncodedOrigin.RightTop => 90,
            SKEncodedOrigin.BottomRight => 180,
            SKEncodedOrigin.LeftBottom => 270,
            _ => 0,
        };
        if (degrees == 0)
        {
            return source.Copy();
        }

        // Width and height swap for a quarter turn
        var swap = degrees != 180;
        var rotated = new SKBitmap(swap ? source.Height : source.Width, swap ? source.Width : source.Height);
        using var canvas = new SKCanvas(rotated);
        canvas.Translate(rotated.Width / 2f, rotated.Height / 2f);
        canvas.RotateDegrees(degrees);
        canvas.Translate(-source.Width / 2f, -source.Height / 2f);
        using var image = SKImage.FromBitmap(source);
        canvas.DrawImage(image, 0, 0, SKSamplingOptions.Default);
        return rotated;
    }
}
