using System.Numerics;

namespace Yaeger.Platform;

/// <summary>
/// Metrics and atlas location of one rasterized glyph, in the same pixel units the text is laid
/// out in. The glyph quad's bottom-left corner sits at
/// <c>(penX + OffsetX, baselineY + OffsetY)</c> with Y pointing up.
/// </summary>
/// <param name="Advance">Horizontal distance the pen moves after this glyph.</param>
/// <param name="OffsetX">Left edge of the glyph quad relative to the pen.</param>
/// <param name="OffsetY">Bottom edge of the glyph quad relative to the baseline (Y up).</param>
/// <param name="Width">Quad width; zero for glyphs with no ink (e.g. a space).</param>
/// <param name="Height">Quad height; zero for glyphs with no ink.</param>
/// <param name="UvMin">Bottom-left UV in the atlas texture.</param>
/// <param name="UvMax">Top-right UV in the atlas texture.</param>
/// <param name="TexturePath">Atlas texture the glyph lives on.</param>
public readonly record struct GlyphMetrics(
    float Advance,
    float OffsetX,
    float OffsetY,
    float Width,
    float Height,
    Vector2 UvMin,
    Vector2 UvMax,
    string TexturePath
);

/// <summary>Vertical metrics of a font at a given size, in layout pixels.</summary>
/// <param name="LineHeight">Baseline-to-baseline distance.</param>
/// <param name="Ascent">Distance from the baseline to the top of a line.</param>
public readonly record struct FontLineMetrics(float LineHeight, float Ascent);

/// <summary>
/// Supplies glyph metrics to <see cref="TextLayout"/>. The browser backs this with a Canvas 2D
/// glyph atlas; tests back it with fixed fake metrics.
/// </summary>
public interface IGlyphMetricsProvider
{
    FontLineMetrics GetLineMetrics(string fontKey, int fontSize);

    /// <summary>Returns false when the glyph can't be provided (it is then skipped).</summary>
    bool TryGetGlyph(string fontKey, int fontSize, int codepoint, out GlyphMetrics glyph);
}
