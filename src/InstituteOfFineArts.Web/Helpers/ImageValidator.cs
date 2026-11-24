using SkiaSharp;

namespace InstituteOfFineArts.Web.Helpers;

/// <summary>Result of checking an uploaded image.</summary>
/// <param name="IsValid">True when the file can be stored</param>
/// <param name="Error">Message for the user when it cannot</param>
/// <param name="Width">Width in pixels</param>
/// <param name="Height">Height in pixels</param>
/// <param name="Warning">Non-blocking advice (small image), or null</param>
/// <param name="ContentType">image/jpeg, image/png or image/webp</param>
/// <param name="Extension">.jpg, .png or .webp (the extension the stored file gets)</param>
public record ImageCheckResult(bool IsValid, string? Error, int Width = 0, int Height = 0, string? Warning = null,
    string ContentType = "image/jpeg", string Extension = ".jpg");

/// <summary>
/// Checks an uploaded image before it is stored (BR-16, BR-17 – widened 16/11/2026): JPEG, PNG or WebP, at most the
/// configured size (10 MB). Every layer must agree, so a renamed file is refused: the file extension, the content type
/// sent by the browser, the first bytes of the file (magic bytes: FF D8 FF / 89 50 4E 47 / RIFF….WEBP), and SkiaSharp
/// decoding the header to the same format with real dimensions.
/// </summary>
public static class ImageValidator
{
    /// <summary>Shortest side below which the user is warned that the picture may look blurry.</summary>
    public const int RecommendedMinSide = 600;

    /// <summary>No file chosen.</summary>
    public const string MissingMessage = "Please choose an image file.";

    /// <summary>Wrong type.</summary>
    public const string NotImageMessage = "Only JPG, PNG or WebP images are allowed.";

    /// <summary>The content does not match the type.</summary>
    public const string CorruptMessage = "The file is not a valid JPG, PNG or WebP image.";

    /// <summary>Accepted formats: extensions, content types sent by browsers, the stored content type and extension.</summary>
    private static readonly (string[] Extensions, string[] ContentTypes, SKEncodedImageFormat Format, string ContentType, string Extension)[] Formats =
    [
        ([".jpg", ".jpeg"], ["image/jpeg", "image/pjpeg", "image/jpg"], SKEncodedImageFormat.Jpeg, "image/jpeg", ".jpg"),
        ([".png"], ["image/png", "image/x-png"], SKEncodedImageFormat.Png, "image/png", ".png"),
        ([".webp"], ["image/webp"], SKEncodedImageFormat.Webp, "image/webp", ".webp"),
    ];

    /// <summary>Checks the file: present, accepted type, within the size limit, real content of that type.</summary>
    /// <param name="file">Uploaded file</param>
    /// <param name="maxBytes">Largest allowed size</param>
    public static async Task<ImageCheckResult> ValidateAsync(IFormFile? file, long maxBytes)
    {
        // 1. A file must be present and not empty
        if (file == null || file.Length == 0)
        {
            return new ImageCheckResult(false, MissingMessage);
        }

        // 2. Extension and content type must name the same accepted format
        var extension = Path.GetExtension(file.FileName ?? string.Empty).ToLowerInvariant();
        var contentType = (file.ContentType ?? string.Empty).ToLowerInvariant();
        var format = Formats.FirstOrDefault(f => f.Extensions.Contains(extension));
        if (format.Extensions == null || !format.ContentTypes.Contains(contentType))
        {
            return new ImageCheckResult(false, NotImageMessage);
        }

        // 3. Size limit (checked before reading the content)
        if (file.Length > maxBytes)
        {
            return new ImageCheckResult(false, TooLargeMessage(maxBytes));
        }

        // 4. Read the content once to check the magic bytes and decode the header
        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer);
        var bytes = buffer.ToArray();
        if (!HasMagicBytes(bytes, format.Format))
        {
            return new ImageCheckResult(false, CorruptMessage);
        }

        // 5. SkiaSharp must recognise the same format and read the dimensions
        using var data = SKData.CreateCopy(bytes);
        using var codec = SKCodec.Create(data);
        if (codec == null || codec.EncodedFormat != format.Format || codec.Info.Width <= 0 || codec.Info.Height <= 0)
        {
            return new ImageCheckResult(false, CorruptMessage);
        }
        var width = codec.Info.Width;
        var height = codec.Info.Height;

        // 6. Small images are accepted but the user is told they may look blurry
        string? warning = Math.Min(width, height) < RecommendedMinSide
            ? $"The image is {width}×{height} px. At least {RecommendedMinSide} px on the shortest side is recommended."
            : null;
        return new ImageCheckResult(true, null, width, height, warning, format.ContentType, format.Extension);
    }

    /// <summary>"Images must be 10 MB or smaller."</summary>
    public static string TooLargeMessage(long maxBytes)
        => $"Images must be {maxBytes / (1024.0 * 1024.0):0.#} MB or smaller.";

    /// <summary>Content type of a stored file from its extension (image/jpeg by default).</summary>
    public static string ContentTypeOf(string? path) => Path.GetExtension(path ?? string.Empty).ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".webp" => "image/webp",
        _ => "image/jpeg",
    };

    /// <summary>True when the first bytes are those of the format.</summary>
    private static bool HasMagicBytes(byte[] b, SKEncodedImageFormat format) => format switch
    {
        SKEncodedImageFormat.Jpeg => b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF,
        SKEncodedImageFormat.Png => b.Length >= 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47
                                    && b[4] == 0x0D && b[5] == 0x0A && b[6] == 0x1A && b[7] == 0x0A,
        SKEncodedImageFormat.Webp => b.Length >= 12 && b[0] == (byte)'R' && b[1] == (byte)'I' && b[2] == (byte)'F' && b[3] == (byte)'F'
                                     && b[8] == (byte)'W' && b[9] == (byte)'E' && b[10] == (byte)'B' && b[11] == (byte)'P',
        _ => false,
    };
}
