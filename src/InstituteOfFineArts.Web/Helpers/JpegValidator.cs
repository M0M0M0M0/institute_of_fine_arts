using SkiaSharp;

namespace InstituteOfFineArts.Web.Helpers;

/// <summary>Result of checking an uploaded image.</summary>
/// <param name="IsValid">True when the file can be stored</param>
/// <param name="Error">Reason the file was rejected (English, shown to the user)</param>
/// <param name="Width">Image width in pixels (0 when invalid)</param>
/// <param name="Height">Image height in pixels (0 when invalid)</param>
/// <param name="Warning">Non-blocking advice, e.g. the image is small (BR-17)</param>
public record JpegCheckResult(bool IsValid, string? Error, int Width = 0, int Height = 0, string? Warning = null);

/// <summary>
/// Checks that an uploaded file really is a JPEG image within the size limit (BR-16, BR-17):
/// extension + content type + magic bytes FF D8 FF + decodable by SkiaSharp (blocks renamed files).
/// </summary>
public static class JpegValidator
{
    /// <summary>Recommended minimum length of the shortest side; smaller images only get a warning.</summary>
    public const int RecommendedMinSide = 600;

    /// <summary>Message when no file was chosen.</summary>
    public const string MissingMessage = "Please choose an image file.";

    /// <summary>Message for a wrong extension or content type.</summary>
    public const string NotJpegMessage = "Only JPEG images (.jpg, .jpeg) are allowed.";

    /// <summary>Message when the content is not a readable JPEG.</summary>
    public const string CorruptMessage = "The file is not a valid JPEG image.";

    /// <summary>Allowed file extensions (lower case).</summary>
    private static readonly string[] Extensions = { ".jpg", ".jpeg" };

    /// <summary>Allowed content types sent by browsers for JPEG files.</summary>
    private static readonly string[] ContentTypes = { "image/jpeg", "image/pjpeg" };

    /// <summary>Checks one uploaded file.</summary>
    /// <param name="file">File from the form (may be null when nothing was chosen)</param>
    /// <param name="maxBytes">Maximum allowed size in bytes</param>
    public static async Task<JpegCheckResult> ValidateAsync(IFormFile? file, long maxBytes)
    {
        // 1. A file must be present and not empty
        if (file == null || file.Length == 0)
        {
            return new JpegCheckResult(false, MissingMessage);
        }

        // 2. Extension and content type
        var extension = Path.GetExtension(file.FileName ?? string.Empty).ToLowerInvariant();
        if (!Extensions.Contains(extension) || !ContentTypes.Contains((file.ContentType ?? string.Empty).ToLowerInvariant()))
        {
            return new JpegCheckResult(false, NotJpegMessage);
        }

        // 3. Size limit (checked before reading the content)
        if (file.Length > maxBytes)
        {
            return new JpegCheckResult(false, TooLargeMessage(maxBytes));
        }

        // 4. Read the content once (at most 5 MB) to check the magic bytes and decode the header
        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer);
        var bytes = buffer.ToArray();
        if (bytes.Length < 3 || bytes[0] != 0xFF || bytes[1] != 0xD8 || bytes[2] != 0xFF)
        {
            return new JpegCheckResult(false, CorruptMessage);
        }

        // 5. SkiaSharp must recognise it as JPEG and read its dimensions
        using var data = SKData.CreateCopy(bytes);
        using var codec = SKCodec.Create(data);
        if (codec == null || codec.EncodedFormat != SKEncodedImageFormat.Jpeg || codec.Info.Width <= 0 || codec.Info.Height <= 0)
        {
            return new JpegCheckResult(false, CorruptMessage);
        }

        var width = codec.Info.Width;
        var height = codec.Info.Height;

        // 6. Small images are accepted but the user is told they may look blurry
        string? warning = Math.Min(width, height) < RecommendedMinSide
            ? $"The image is {width}×{height} px. At least {RecommendedMinSide} px on the shortest side is recommended."
            : null;
        return new JpegCheckResult(true, null, width, height, warning);
    }

    /// <summary>Message for a file over the limit, e.g. "Images must be 5 MB or smaller."</summary>
    /// <param name="maxBytes">Maximum allowed size in bytes</param>
    public static string TooLargeMessage(long maxBytes)
        => $"Images must be {maxBytes / (1024.0 * 1024.0):0.#} MB or smaller.";
}
