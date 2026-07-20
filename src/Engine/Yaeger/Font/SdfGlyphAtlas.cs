using System.Numerics;
using Silk.NET.OpenGL;
using SkiaSharp;
using Yaeger.Rendering;

namespace Yaeger.Font;

/// <summary>
/// Glyph atlas that stores signed distance fields instead of raw alpha coverage.
/// Each glyph is rendered internally at <see cref="SdfScale"/>× the atlas font size
/// so the distance transform has sub-pixel accuracy; the result is then box-filtered
/// down to the atlas cell.  The companion SDF fragment shader reconstructs a crisp
/// edge at any display scale using <c>smoothstep</c> on the stored distance value,
/// where 0.5 == the glyph boundary, values &gt;0.5 are inside, and values &lt;0.5
/// are outside.
/// </summary>
public class SdfGlyphAtlas : IGlyphAtlas
{
    /// <summary>Upscale factor for the internal high-resolution render.</summary>
    internal const int SdfScale = 2;

    /// <summary>Distance-search radius in high-resolution pixels (= SdfSpread/SdfScale in atlas pixels).</summary>
    internal const int SdfSpread = 16;

    private readonly Font _font;
    private readonly int _fontSize;
    private readonly Dictionary<uint, AtlasGlyph> _glyphs = new();
    private readonly FontTexture _texture;
    private readonly int _atlasWidth;
    private readonly int _atlasHeight;
    private readonly SKTypeface _typeface;

    // High-res font used for both measuring and rasterising.
    // Metrics are divided by SdfScale to get atlas-space values.
    private readonly SKFont _hrFont;
    private bool _disposed;
    private int _nextAtlasIndex;

    public SdfGlyphAtlas(GL gl, Font font, int fontSize = 48, int atlasSize = 512)
    {
        _font = font ?? throw new ArgumentNullException(nameof(font));
        _fontSize = fontSize;
        _atlasWidth = atlasSize;
        _atlasHeight = atlasSize;

        _texture = new FontTexture(gl, _atlasWidth, _atlasHeight);

        using var fontData = SKData.CreateCopy(_font.FontBytes);
        _typeface =
            SKTypeface.FromData(fontData)
            ?? throw new InvalidOperationException("Failed to create typeface from font data");

        // No hinting for SDF — we want smooth, hint-free outlines so the distance
        // field represents the true vector shape rather than a hinted approximation.
        _hrFont = new SKFont(_typeface, fontSize * SdfScale)
        {
            Edging = SKFontEdging.Antialias,
            Hinting = SKFontHinting.None,
            Subpixel = true,
        };
    }

    private AtlasGlyph AddGlyph(uint codepoint)
    {
        if (_glyphs.TryGetValue(codepoint, out var existing))
            return existing;

        var glyph = RenderGlyph(codepoint);
        _glyphs[codepoint] = glyph;
        return glyph;
    }

    public AtlasGlyph[] AddGlyphsForText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return [];

        return _font.Shape(text, _fontSize).Select(g => AddGlyph(g.Codepoint)).ToArray();
    }

    public AtlasGlyph? GetGlyph(uint codepoint) =>
        _glyphs.TryGetValue(codepoint, out var g) ? g : null;

    private AtlasGlyph RenderGlyph(uint codepoint)
    {
        int glyphIndex = _nextAtlasIndex++;
        int glyphsPerRow = _atlasWidth / _fontSize;
        int cellX = (glyphIndex % glyphsPerRow) * _fontSize;
        int cellY = (glyphIndex / glyphsPerRow) * _fontSize;

        var text = char.ConvertFromUtf32((int)codepoint);

        // Measure using the high-res font; divide results by SdfScale for atlas-space metrics.
        var hrAdvance = _hrFont.MeasureText(text, out SKRect hrBounds);
        var advance = hrAdvance / SdfScale;
        var glyphWidth = (int)Math.Ceiling(hrBounds.Width / SdfScale);
        var glyphHeight = (int)Math.Ceiling(hrBounds.Height / SdfScale);

        if (glyphWidth <= 0 || glyphHeight <= 0)
            return CreateEmptyGlyph(codepoint, cellX, cellY, advance);

        int renderWidth = Math.Min(Math.Max(glyphWidth, 1), _fontSize);
        int renderHeight = Math.Min(Math.Max(glyphHeight, 1), _fontSize);

        // --- High-resolution render ---
        int hrSize = _fontSize * SdfScale;
        var imageInfo = new SKImageInfo(hrSize, hrSize, SKColorType.Alpha8);
        using var surface = SKSurface.Create(imageInfo);
        if (surface == null)
            return CreateEmptyGlyph(codepoint, cellX, cellY, advance);

        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        using var paint = new SKPaint();
        paint.Color = SKColors.White;
        paint.IsAntialias = true;

        // Position the glyph so its bounding-box top-left lands at (0,0) in the HR surface,
        // matching the layout used by GlyphAtlas for consistent metric behaviour.
        canvas.DrawText(text, -hrBounds.Left, -hrBounds.Top, SKTextAlign.Left, _hrFont, paint);

        using var image = surface.Snapshot();
        using var pixmap = image.PeekPixels();
        if (pixmap == null)
            return CreateEmptyGlyph(codepoint, cellX, cellY, advance);

        // --- SDF pipeline ---
        var alphaBytes = pixmap.GetPixelSpan();
        var binaryMask = ThresholdAlpha(alphaBytes, hrSize, hrSize, pixmap.RowBytes);
        var sdfFloat = GenerateSdf(binaryMask, hrSize, hrSize, SdfSpread);
        var sdfBytes = DownsampleSdf(sdfFloat, hrSize, hrSize, _fontSize, _fontSize, SdfScale);

        _texture.SetData(new ReadOnlySpan<byte>(sdfBytes), cellX, cellY, _fontSize, _fontSize);

        var bearing = new Vector2(hrBounds.Left / SdfScale, -hrBounds.Top / SdfScale);

        return new AtlasGlyph
        {
            Codepoint = codepoint,
            TexCoordMin = new Vector2(
                (float)cellX / _atlasWidth,
                (float)(cellY + renderHeight) / _atlasHeight
            ),
            TexCoordMax = new Vector2(
                (float)(cellX + renderWidth) / _atlasWidth,
                (float)cellY / _atlasHeight
            ),
            Size = new Vector2(renderWidth, renderHeight),
            Bearing = bearing,
            Advance = advance,
        };
    }

    // --- Pure CPU helpers (internal so unit tests can exercise them without GL) ---

    /// <summary>
    /// Converts an Alpha8 pixel span into a boolean "inside" mask.
    /// A pixel is considered inside when its alpha value exceeds 127.
    /// <paramref name="rowStride"/> is the number of bytes between rows
    /// (SkiaSharp may pad rows internally).
    /// </summary>
    internal static bool[] ThresholdAlpha(
        ReadOnlySpan<byte> alpha,
        int width,
        int height,
        int rowStride = 0
    )
    {
        if (rowStride <= 0)
            rowStride = width;

        var mask = new bool[width * height];
        for (int y = 0; y < height; y++)
        {
            int srcBase = y * rowStride;
            int dstBase = y * width;
            for (int x = 0; x < width; x++)
            {
                int srcIdx = srcBase + x;
                mask[dstBase + x] = srcIdx < alpha.Length && alpha[srcIdx] > 127;
            }
        }
        return mask;
    }

    /// <summary>
    /// Brute-force signed Euclidean distance transform with radius <paramref name="spread"/>.
    /// Returns a float array in [0, 1] where 0.5 == the glyph boundary,
    /// values &gt; 0.5 are inside, and values &lt; 0.5 are outside.
    /// </summary>
    /// <remarks>
    /// Complexity is O(W × H × spread²).  For the default SdfSpread=16 and a 96×96
    /// high-resolution surface this is ≈2.7 M iterations per glyph, which is fast
    /// enough given glyphs are cached after the first render.
    /// </remarks>
    internal static float[] GenerateSdf(bool[] insideMask, int w, int h, int spread)
    {
        var sdf = new float[w * h];
        float spreadF = spread;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                bool isInside = insideMask[y * w + x];
                float minDistSq = (spreadF + 1) * (spreadF + 1);

                int xLo = Math.Max(0, x - spread);
                int xHi = Math.Min(w - 1, x + spread);
                int yLo = Math.Max(0, y - spread);
                int yHi = Math.Min(h - 1, y + spread);

                for (int ny = yLo; ny <= yHi; ny++)
                {
                    float dy = ny - y;
                    float dy2 = dy * dy;
                    if (dy2 >= minDistSq)
                        continue;

                    for (int nx = xLo; nx <= xHi; nx++)
                    {
                        if (insideMask[ny * w + nx] != isInside)
                        {
                            float dx = nx - x;
                            float dSq = dx * dx + dy2;
                            if (dSq < minDistSq)
                                minDistSq = dSq;
                        }
                    }
                }

                float dist = Math.Min(MathF.Sqrt(minDistSq), spreadF);
                float signedDist = isInside ? dist : -dist;
                sdf[y * w + x] = Math.Clamp(0.5f + 0.5f * signedDist / spreadF, 0f, 1f);
            }
        }

        return sdf;
    }

    /// <summary>
    /// Box-filters a high-resolution SDF float array down to <paramref name="outW"/> × <paramref name="outH"/>
    /// and encodes the result as bytes (0–255).
    /// </summary>
    internal static byte[] DownsampleSdf(
        float[] highRes,
        int hrW,
        int hrH,
        int outW,
        int outH,
        int scale
    )
    {
        var result = new byte[outW * outH];

        for (int y = 0; y < outH; y++)
        {
            for (int x = 0; x < outW; x++)
            {
                float sum = 0f;
                int count = 0;
                for (int dy = 0; dy < scale; dy++)
                {
                    int hy = y * scale + dy;
                    if (hy >= hrH)
                        continue;
                    for (int dx = 0; dx < scale; dx++)
                    {
                        int hx = x * scale + dx;
                        if (hx < hrW)
                        {
                            sum += highRes[hy * hrW + hx];
                            count++;
                        }
                    }
                }
                result[y * outW + x] = count > 0 ? (byte)(sum / count * 255f + 0.5f) : (byte)0;
            }
        }

        return result;
    }

    private AtlasGlyph CreateEmptyGlyph(uint codepoint, int cellX, int cellY, float advance)
    {
        var empty = new byte[_fontSize * _fontSize];
        _texture.SetData(new ReadOnlySpan<byte>(empty), cellX, cellY, _fontSize, _fontSize);

        return new AtlasGlyph
        {
            Codepoint = codepoint,
            TexCoordMin = new Vector2(
                (float)cellX / _atlasWidth,
                (float)(cellY + _fontSize) / _atlasHeight
            ),
            TexCoordMax = new Vector2(
                (float)(cellX + _fontSize) / _atlasWidth,
                (float)cellY / _atlasHeight
            ),
            Size = Vector2.Zero,
            Bearing = Vector2.Zero,
            Advance = advance,
        };
    }

    public void BindTexture() => _texture.Bind();

    public void UnbindTexture() => _texture.Unbind();

    public void Dispose()
    {
        if (_disposed)
            return;

        _hrFont.Dispose();
        _typeface.Dispose();
        _texture.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
