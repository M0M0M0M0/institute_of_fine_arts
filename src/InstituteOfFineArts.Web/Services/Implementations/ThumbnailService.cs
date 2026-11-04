using InstituteOfFineArts.Web.Services.Interfaces;
using SkiaSharp;

namespace InstituteOfFineArts.Web.Services.Implementations;

/// <summary>
/// Creates thumbnails with SkiaSharp and writes them with File I/O.
/// </summary>
public class ThumbnailService : IThumbnailService
{
    /// <summary>JPEG quality of thumbnails (0–100).</summary>
    private const int Quality = 85;

    /// <inheritdoc />
    public async Task CreateAsync(string sourcePath, string targetPath, int width)
    {
        // 1. Decode the original and turn it upright according to its EXIF orientation (photos from phones)
        using var codec = SKCodec.Create(sourcePath)
            ?? throw new InvalidOperationException("The image could not be decoded.");
        using var decoded = SKBitmap.Decode(codec)
            ?? throw new InvalidOperationException("The image could not be decoded.");
        using var upright = ApplyOrientation(decoded, codec.EncodedOrigin);

        // 2. Scale down to the target width, keeping the aspect ratio (never enlarge)
        var targetWidth = Math.Min(width, upright.Width);
        var targetHeight = Math.Max(1, (int)Math.Round(upright.Height * (double)targetWidth / upright.Width));
        using var resized = upright.Resize(new SKImageInfo(targetWidth, targetHeight),
                                new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear))
            ?? throw new InvalidOperationException("The image could not be resized.");

        // 3. Encode as JPEG and write it to disk with a FileStream
        using var image = SKImage.FromBitmap(resized);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, Quality);
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        await using var output = new FileStream(targetPath, FileMode.Create, FileAccess.Write);
        data.SaveTo(output);
    }

    /// <summary>
    /// Returns a copy rotated according to the EXIF origin (0°, 90°, 180°, 270°).
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
