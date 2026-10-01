using System.Numerics;
using System.Text;

namespace Yaeger.Platform;

public enum TextAlignment
{
    Left,
    Center,
    Right,
}

/// <param name="MaxWidth">
/// Wrap width in layout pixels. Lines break at spaces; a single word wider than this is kept on
/// its own line rather than split. Zero, negative or <see cref="float.PositiveInfinity"/> disables wrapping.
/// </param>
/// <param name="Alignment">Horizontal alignment of each line within the block's width.</param>
public readonly record struct TextLayoutOptions(
    float MaxWidth = float.PositiveInfinity,
    TextAlignment Alignment = TextAlignment.Left
);

/// <summary>One glyph quad produced by <see cref="TextLayout"/> (bottom-left corner, Y up).</summary>
public readonly record struct PositionedGlyph(
    Vector2 Position,
    Vector2 Size,
    Vector2 UvMin,
    Vector2 UvMax,
    string TexturePath
);

/// <summary>
/// Platform-independent text layout: advance, newlines, simple word wrap and alignment. The
/// origin is the left end of the first line's baseline; later lines move down (negative Y).
/// </summary>
public static class TextLayout
{
    /// <summary>
    /// Appends the glyph quads for <paramref name="text"/> to <paramref name="output"/> (which is
    /// not cleared, so callers can reuse one list per frame) and returns the block's size.
    /// </summary>
    public static Vector2 Layout(
        string text,
        IGlyphMetricsProvider metrics,
        string fontKey,
        int fontSize,
        TextLayoutOptions options,
        List<PositionedGlyph> output
    )
    {
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(output);
        if (string.IsNullOrEmpty(text))
            return Vector2.Zero;

        var line = metrics.GetLineMetrics(fontKey, fontSize);
        var maxWidth = options.MaxWidth > 0f ? options.MaxWidth : float.PositiveInfinity;
        var blockWidth = 0f;
        var lineCount = 0;

        // Glyphs of the line being built, positioned from x = 0; shifted for alignment on flush.
        var lineStart = output.Count;
        var penX = 0f;
        // Where the last breakable space left the line: output index and pen position before it.
        var breakIndex = -1;
        var breakPenX = 0f;
        var breakSkipX = 0f;

        void FlushLine(float width)
        {
            var shift = options.Alignment switch
            {
                TextAlignment.Center when float.IsFinite(maxWidth) => (maxWidth - width) / 2f,
                TextAlignment.Right when float.IsFinite(maxWidth) => maxWidth - width,
                _ => 0f,
            };
            var y = -lineCount * line.LineHeight;
            for (var i = lineStart; i < output.Count; i++)
            {
                var g = output[i];
                output[i] = g with
                {
                    Position = new Vector2(g.Position.X + shift, g.Position.Y + y),
                };
            }
            blockWidth = MathF.Max(blockWidth, width);
            lineCount++;
            lineStart = output.Count;
            penX = 0f;
            breakIndex = -1;
        }

        foreach (var rune in text.EnumerateRunes())
        {
            if (rune.Value == '\r')
                continue;
            if (rune.Value == '\n')
            {
                FlushLine(penX);
                continue;
            }
            if (!metrics.TryGetGlyph(fontKey, fontSize, rune.Value, out var glyph))
                continue;

            var isSpace = rune.Value == ' ';
            // Wrap before a non-space glyph that would overflow, at the last space if the line has one.
            if (!isSpace && penX + glyph.Advance > maxWidth && breakIndex >= 0)
            {
                var carried = output.GetRange(breakIndex, output.Count - breakIndex);
                output.RemoveRange(breakIndex, output.Count - breakIndex);
                var carriedX0 = breakSkipX;
                var carriedWidth = penX - breakSkipX;
                FlushLine(breakPenX);
                // Re-seat the carried word at the start of the new line.
                foreach (var g in carried)
                    output.Add(
                        g with
                        {
                            Position = new Vector2(g.Position.X - carriedX0, g.Position.Y),
                        }
                    );
                penX = carriedWidth;
            }

            if (glyph.Width > 0f && glyph.Height > 0f)
                output.Add(
                    new PositionedGlyph(
                        new Vector2(penX + glyph.OffsetX, glyph.OffsetY),
                        new Vector2(glyph.Width, glyph.Height),
                        glyph.UvMin,
                        glyph.UvMax,
                        glyph.TexturePath
                    )
                );
            penX += glyph.Advance;

            if (isSpace)
            {
                breakIndex = output.Count;
                breakPenX = penX - glyph.Advance;
                breakSkipX = penX;
            }
        }

        FlushLine(penX);
        return new Vector2(blockWidth, lineCount * line.LineHeight);
    }

    /// <summary>Size of the laid-out block, without producing glyph quads.</summary>
    public static Vector2 Measure(
        string text,
        IGlyphMetricsProvider metrics,
        string fontKey,
        int fontSize,
        TextLayoutOptions options = default
    )
    {
        var scratch = new List<PositionedGlyph>();
        return Layout(text, metrics, fontKey, fontSize, options, scratch);
    }
}
