using SkiaSharp;

namespace InstituteOfFineArts.Web.Data.Seed;

/// <summary>
/// Draws simple original "paintings" for the sample data when no picture exists in database/seed-images
/// (no copyright issue: the images are generated). Each theme has its own palette and shapes; the seed number
/// makes every image different but always the same for the same seed.
/// </summary>
public static class SampleArtwork
{
    /// <summary>Themes matching the sample competitions.</summary>
    public enum Theme
    {
        Landscape,
        Wildlife,
        Night,
        Autumn,
        Abstract,
        Festival,
        Portrait,
    }

    /// <summary>JPEG quality of the generated images.</summary>
    private const int Quality = 85;

    /// <summary>Creates a JPEG image of the given theme and size.</summary>
    /// <param name="theme">What to draw</param>
    /// <param name="seed">Random seed (same seed → same image)</param>
    /// <param name="width">Width in pixels</param>
    /// <param name="height">Height in pixels</param>
    public static byte[] Create(Theme theme, int seed, int width = 1200, int height = 900)
    {
        var random = new Random(seed);
        using var bitmap = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(bitmap))
        {
            switch (theme)
            {
                case Theme.Landscape: DrawLandscape(canvas, random, width, height); break;
                case Theme.Wildlife: DrawWildlife(canvas, random, width, height); break;
                case Theme.Night: DrawNight(canvas, random, width, height); break;
                case Theme.Autumn: DrawAutumn(canvas, random, width, height); break;
                case Theme.Festival: DrawFestival(canvas, random, width, height); break;
                case Theme.Portrait: DrawPortrait(canvas, random, width, height); break;
                default: DrawAbstract(canvas, random, width, height); break;
            }
        }
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, Quality);
        return data.ToArray();
    }

    /// <summary>Sky gradient, a sun and three layers of hills.</summary>
    private static void DrawLandscape(SKCanvas c, Random r, int w, int h)
    {
        Sky(c, w, h, Hsl(r.Next(180, 220), 60, 75), Hsl(r.Next(20, 50), 80, 85));
        Circle(c, w * (0.2f + (float)r.NextDouble() * 0.6f), h * 0.3f, h * 0.08f, Hsl(r.Next(35, 50), 95, 65));
        for (var layer = 0; layer < 3; layer++)
        {
            Hills(c, r, w, h, h * (0.5f + layer * 0.13f), Hsl(r.Next(90, 150), 40, 45 - layer * 10));
        }
    }

    /// <summary>Green jungle background with leafy blobs and an animal-like shape made of ellipses.</summary>
    private static void DrawWildlife(SKCanvas c, Random r, int w, int h)
    {
        Sky(c, w, h, Hsl(r.Next(90, 130), 45, 70), Hsl(r.Next(60, 90), 50, 35));
        for (var i = 0; i < 40; i++)
        {
            Ellipse(c, r.Next(w), r.Next(h), r.Next(30, 120), r.Next(15, 60), Hsl(r.Next(80, 150), 50, r.Next(20, 50), 160));
        }
        var cx = w * 0.5f;
        var cy = h * 0.6f;
        var fur = Hsl(r.Next(15, 40), 60, 45);
        Ellipse(c, cx, cy, w * 0.18f, h * 0.12f, fur);                       // body
        Ellipse(c, cx + w * 0.17f, cy - h * 0.1f, w * 0.07f, h * 0.07f, fur); // head
        Circle(c, cx + w * 0.19f, cy - h * 0.12f, h * 0.012f, SKColors.Black);
    }

    /// <summary>Night sky with stars and a skyline of lit windows.</summary>
    private static void DrawNight(SKCanvas c, Random r, int w, int h)
    {
        Sky(c, w, h, Hsl(230, 60, 8), Hsl(r.Next(250, 280), 50, 25));
        for (var i = 0; i < 120; i++)
        {
            Circle(c, r.Next(w), r.Next(h / 2), (float)r.NextDouble() * 2 + 0.5f, Hsl(55, 30, 90, (byte)r.Next(120, 255)));
        }
        Circle(c, w * 0.8f, h * 0.18f, h * 0.06f, Hsl(50, 40, 88));
        var x = 0f;
        while (x < w)
        {
            var bw = r.Next(60, 160);
            var bh = r.Next(h / 5, h * 3 / 5);
            Rect(c, x, h - bh, bw, bh, Hsl(230, 30, r.Next(10, 22)));
            for (var wy = h - bh + 15; wy < h - 20; wy += 28)
            {
                for (var wx = x + 10; wx < x + bw - 15; wx += 22)
                {
                    if (r.NextDouble() < 0.45)
                    {
                        Rect(c, wx, wy, 10, 14, Hsl(r.Next(40, 55), 90, 65));
                    }
                }
            }
            x += bw + r.Next(0, 12);
        }
    }

    /// <summary>Warm gradient with falling leaves and tree trunks.</summary>
    private static void DrawAutumn(SKCanvas c, Random r, int w, int h)
    {
        Sky(c, w, h, Hsl(r.Next(30, 45), 70, 80), Hsl(r.Next(10, 25), 70, 50));
        for (var t = 0; t < 5; t++)
        {
            Rect(c, r.Next(w), h * 0.35f, r.Next(14, 28), h * 0.65f, Hsl(25, 40, 22));
        }
        for (var i = 0; i < 90; i++)
        {
            c.Save();
            c.RotateDegrees(r.Next(0, 360), r.Next(w), r.Next(h));
            Ellipse(c, r.Next(w), r.Next(h), r.Next(10, 26), r.Next(5, 12), Hsl(r.Next(0, 50), 85, r.Next(40, 60), 220));
            c.Restore();
        }
    }

    /// <summary>Red background with lanterns and fireworks (Lunar New Year).</summary>
    private static void DrawFestival(SKCanvas c, Random r, int w, int h)
    {
        Sky(c, w, h, Hsl(355, 70, 35), Hsl(10, 80, 20));
        for (var f = 0; f < 4; f++)
        {
            var fx = r.Next(w);
            var fy = r.Next(h / 2);
            var color = Hsl(r.Next(30, 60), 90, 65);
            using var paint = new SKPaint { Color = color, StrokeWidth = 3, IsAntialias = true };
            for (var a = 0; a < 360; a += 15)
            {
                var rad = a * Math.PI / 180;
                var len = r.Next(40, 110);
                c.DrawLine(fx, fy, fx + (float)(Math.Cos(rad) * len), fy + (float)(Math.Sin(rad) * len), paint);
            }
        }
        for (var l = 0; l < 6; l++)
        {
            var lx = w * (l + 0.5f) / 6;
            Ellipse(c, lx, h * 0.72f, 45, 60, Hsl(r.Next(0, 15), 85, 50));
            Rect(c, lx - 20, h * 0.72f - 66, 40, 10, Hsl(45, 80, 50));
        }
    }

    /// <summary>Soft background with a stylised figure (no real face).</summary>
    private static void DrawPortrait(SKCanvas c, Random r, int w, int h)
    {
        Sky(c, w, h, Hsl(r.Next(0, 360), 30, 85), Hsl(r.Next(0, 360), 30, 60));
        var skin = Hsl(r.Next(20, 35), 45, 70);
        Ellipse(c, w * 0.5f, h * 0.95f, w * 0.3f, h * 0.35f, Hsl(r.Next(0, 360), 45, 40)); // shoulders
        Ellipse(c, w * 0.5f, h * 0.42f, w * 0.13f, h * 0.2f, skin);                        // head
        Ellipse(c, w * 0.5f, h * 0.27f, w * 0.15f, h * 0.09f, Hsl(25, 40, 20));           // hair
    }

    /// <summary>Overlapping translucent shapes.</summary>
    private static void DrawAbstract(SKCanvas c, Random r, int w, int h)
    {
        Sky(c, w, h, Hsl(r.Next(0, 360), 50, 80), Hsl(r.Next(0, 360), 50, 40));
        for (var i = 0; i < 25; i++)
        {
            Circle(c, r.Next(w), r.Next(h), r.Next(30, 200), Hsl(r.Next(0, 360), 70, 55, (byte)r.Next(70, 160)));
        }
    }

    /// <summary>Vertical gradient filling the canvas.</summary>
    private static void Sky(SKCanvas c, int w, int h, SKColor top, SKColor bottom)
    {
        using var paint = new SKPaint
        {
            Shader = SKShader.CreateLinearGradient(new SKPoint(0, 0), new SKPoint(0, h), new[] { top, bottom }, SKShaderTileMode.Clamp),
        };
        c.DrawRect(0, 0, w, h, paint);
    }

    /// <summary>A wavy band of hills from <paramref name="baseY"/> to the bottom.</summary>
    private static void Hills(SKCanvas c, Random r, int w, int h, float baseY, SKColor color)
    {
        using var builder = new SKPathBuilder();
        builder.MoveTo(0, h);
        builder.LineTo(0, baseY);
        for (var x = 0; x <= w; x += w / 6)
        {
            builder.QuadTo(x + w / 12f, baseY - r.Next(20, (int)(h * 0.12)), x + w / 6f, baseY + r.Next(-10, 20));
        }
        builder.LineTo(w, h);
        builder.Close();
        using var path = builder.Detach();
        using var paint = new SKPaint { Color = color, IsAntialias = true };
        c.DrawPath(path, paint);
    }

    /// <summary>Filled circle.</summary>
    private static void Circle(SKCanvas c, float x, float y, float radius, SKColor color)
    {
        using var paint = new SKPaint { Color = color, IsAntialias = true };
        c.DrawCircle(x, y, radius, paint);
    }

    /// <summary>Filled ellipse centred on (x, y).</summary>
    private static void Ellipse(SKCanvas c, float x, float y, float rx, float ry, SKColor color)
    {
        using var paint = new SKPaint { Color = color, IsAntialias = true };
        c.DrawOval(x, y, rx, ry, paint);
    }

    /// <summary>Filled rectangle.</summary>
    private static void Rect(SKCanvas c, float x, float y, float w, float h, SKColor color)
    {
        using var paint = new SKPaint { Color = color };
        c.DrawRect(x, y, w, h, paint);
    }

    /// <summary>Colour from hue (0–360), saturation and lightness (0–100), with optional alpha.</summary>
    private static SKColor Hsl(float hue, float saturation, float lightness, byte alpha = 255)
        => SKColor.FromHsl(hue, saturation, lightness, alpha);
}
