using Microsoft.AspNetCore.Http;
using SkiaSharp;

namespace InstituteOfFineArts.Tests;

/// <summary>
/// Builds in-memory images and uploaded files for the file storage tests.
/// </summary>
public static class TestImages
{
    /// <summary>Encodes a solid-colour image of the given size in the given format.</summary>
    public static byte[] Create(int width, int height, SKEncodedImageFormat format = SKEncodedImageFormat.Jpeg)
    {
        using var bitmap = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(new SKColor(200, 120, 60));
        }
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, 90);
        return data.ToArray();
    }

    /// <summary>Wraps bytes as an uploaded form file, as model binding would.</summary>
    public static IFormFile AsFormFile(byte[] bytes, string fileName = "photo.jpg", string contentType = "image/jpeg")
        => new FormFile(new MemoryStream(bytes), 0, bytes.Length, "Image", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType,
        };
}
