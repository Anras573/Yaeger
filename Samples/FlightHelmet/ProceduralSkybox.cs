using System.Numerics;
using SkiaSharp;

namespace FlightHelmet;

/// <summary>
/// Generates the six faces of a simple procedural sky cubemap — a vertical gradient from a warm
/// horizon up to a blue zenith, a dark ground plane below, and a sun disc with a soft glow — and
/// writes them as PNG files so they can be loaded through <c>CubemapRegistry.Register</c>.
/// </summary>
internal static class ProceduralSkybox
{
    private static readonly Vector3 ZenithColor = new(0.16f, 0.30f, 0.55f);
    private static readonly Vector3 HorizonColor = new(0.74f, 0.82f, 0.90f);
    private static readonly Vector3 GroundHorizonColor = new(0.42f, 0.38f, 0.33f);
    private static readonly Vector3 GroundColor = new(0.20f, 0.18f, 0.16f);
    private static readonly Vector3 SunColor = new(1.00f, 0.96f, 0.86f);
    private static readonly Vector3 SunGlowColor = new(0.95f, 0.80f, 0.55f);

    /// <summary>
    /// Renders the six cubemap faces into <paramref name="directory"/> and returns their paths in
    /// <c>CubemapRegistry.Register</c> order: right (+X), left (−X), top (+Y), bottom (−Y),
    /// front (+Z), back (−Z). <paramref name="sunDirection"/> points from the scene toward the sun.
    /// </summary>
    public static string[] GenerateFaces(string directory, Vector3 sunDirection, int faceSize = 512)
    {
        Directory.CreateDirectory(directory);
        var sun = Vector3.Normalize(sunDirection);

        // Maps face-local (u, v) in [-1, 1] (v grows downward in the image) to a sample
        // direction, following the OpenGL cubemap face conventions.
        (string Name, Func<float, float, Vector3> Direction)[] faces =
        [
            ("right", (u, v) => new Vector3(1f, -v, -u)),
            ("left", (u, v) => new Vector3(-1f, -v, u)),
            ("top", (u, v) => new Vector3(u, 1f, v)),
            ("bottom", (u, v) => new Vector3(u, -1f, -v)),
            ("front", (u, v) => new Vector3(u, -v, 1f)),
            ("back", (u, v) => new Vector3(-u, -v, -1f)),
        ];

        var paths = new string[faces.Length];
        var pixels = new SKColor[faceSize * faceSize];

        for (var faceIndex = 0; faceIndex < faces.Length; faceIndex++)
        {
            var (name, direction) = faces[faceIndex];

            for (var y = 0; y < faceSize; y++)
            {
                var v = (2f * (y + 0.5f)) / faceSize - 1f;
                for (var x = 0; x < faceSize; x++)
                {
                    var u = (2f * (x + 0.5f)) / faceSize - 1f;
                    var dir = Vector3.Normalize(direction(u, v));
                    pixels[y * faceSize + x] = ToColor(Shade(dir, sun));
                }
            }

            var path = Path.Combine(directory, $"{name}.png");
            using var bitmap = new SKBitmap(
                faceSize,
                faceSize,
                SKColorType.Rgba8888,
                SKAlphaType.Opaque
            );
            bitmap.Pixels = pixels;
            using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
            using var stream = File.Create(path);
            data.SaveTo(stream);
            paths[faceIndex] = path;
        }

        return paths;
    }

    private static Vector3 Shade(Vector3 dir, Vector3 sun)
    {
        Vector3 color;
        if (dir.Y >= 0f)
        {
            // Bias the blend toward the horizon colour so the gradient reads as atmosphere.
            var t = MathF.Pow(dir.Y, 0.6f);
            color = Vector3.Lerp(HorizonColor, ZenithColor, t);
        }
        else
        {
            var t = MathF.Min(1f, -dir.Y * 4f);
            color = Vector3.Lerp(GroundHorizonColor, GroundColor, t);
        }

        var alignment = Vector3.Dot(dir, sun);
        if (alignment > 0f)
        {
            // Crisp disc (~2° across) with a wide soft glow around it.
            var disc = SmoothStep(MathF.Cos(0.045f), MathF.Cos(0.030f), alignment);
            var glow = MathF.Pow(alignment, 64f) * 0.35f;
            color += SunColor * disc + SunGlowColor * glow;
        }

        return color;
    }

    private static float SmoothStep(float edge0, float edge1, float x)
    {
        var t = Math.Clamp((x - edge0) / (edge1 - edge0), 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    private static SKColor ToColor(Vector3 color) =>
        new(
            (byte)(Math.Clamp(color.X, 0f, 1f) * 255f),
            (byte)(Math.Clamp(color.Y, 0f, 1f) * 255f),
            (byte)(Math.Clamp(color.Z, 0f, 1f) * 255f)
        );
}
