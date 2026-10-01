using System.Numerics;
using Yaeger.Platform;

namespace Yaeger.Tests.Platform;

public class TextLayoutTests
{
    // Every glyph is 10 wide (advance 10), 12 tall, sitting 2 below the baseline; a space has
    // advance 10 and no ink. Line height is 20.
    private sealed class FakeMetrics : IGlyphMetricsProvider
    {
        public FontLineMetrics GetLineMetrics(string fontKey, int fontSize) => new(20f, 16f);

        public bool TryGetGlyph(string fontKey, int fontSize, int codepoint, out GlyphMetrics glyph)
        {
            var ink = codepoint != ' ';
            glyph = new GlyphMetrics(
                10f,
                0f,
                -2f,
                ink ? 10f : 0f,
                ink ? 12f : 0f,
                Vector2.Zero,
                Vector2.One,
                "atlas"
            );
            return codepoint != '?';
        }
    }

    private static List<PositionedGlyph> Run(
        string text,
        TextLayoutOptions options,
        out Vector2 size
    )
    {
        var output = new List<PositionedGlyph>();
        size = TextLayout.Layout(text, new FakeMetrics(), "f", 16, options, output);
        return output;
    }

    [Fact]
    public void Layout_SingleLine_ShouldAdvanceEachGlyph()
    {
        var glyphs = Run("abc", default, out var size);

        Assert.Equal([0f, 10f, 20f], glyphs.Select(g => g.Position.X));
        Assert.All(glyphs, g => Assert.Equal(-2f, g.Position.Y));
        Assert.Equal(new Vector2(30f, 20f), size);
    }

    [Fact]
    public void Layout_Space_ShouldAdvanceWithoutEmittingQuad()
    {
        var glyphs = Run("a b", default, out var size);

        Assert.Equal(2, glyphs.Count);
        Assert.Equal(20f, glyphs[1].Position.X);
        Assert.Equal(30f, size.X);
    }

    [Fact]
    public void Layout_Newline_ShouldMoveNextLineDown()
    {
        var glyphs = Run("a\nb", default, out var size);

        Assert.Equal(0f, glyphs[1].Position.X);
        Assert.Equal(-22f, glyphs[1].Position.Y);
        Assert.Equal(new Vector2(10f, 40f), size);
    }

    [Fact]
    public void Layout_WrapWidth_ShouldBreakAtLastSpace()
    {
        var glyphs = Run("ab cd", new TextLayoutOptions(MaxWidth: 35f), out var size);

        // "ab" stays on line 1; "cd" is carried to the start of line 2.
        Assert.Equal([0f, 10f, 0f, 10f], glyphs.Select(g => g.Position.X));
        Assert.Equal([-2f, -2f, -22f, -22f], glyphs.Select(g => g.Position.Y));
        Assert.Equal(new Vector2(20f, 40f), size);
    }

    [Fact]
    public void Layout_WordWiderThanWrapWidth_ShouldStayWhole()
    {
        var glyphs = Run("abcdef", new TextLayoutOptions(MaxWidth: 25f), out var size);

        Assert.Equal(6, glyphs.Count);
        Assert.Equal(new Vector2(60f, 20f), size);
    }

    [Theory]
    [InlineData(TextAlignment.Left, 0f)]
    [InlineData(TextAlignment.Center, 45f)]
    [InlineData(TextAlignment.Right, 90f)]
    public void Layout_Alignment_ShouldShiftLineWithinMaxWidth(TextAlignment alignment, float x)
    {
        var glyphs = Run("a", new TextLayoutOptions(100f, alignment), out _);

        Assert.Equal(x, glyphs[0].Position.X);
    }

    [Fact]
    public void Layout_UnavailableGlyph_ShouldBeSkipped()
    {
        var glyphs = Run("a?b", default, out var size);

        Assert.Equal([0f, 10f], glyphs.Select(g => g.Position.X));
        Assert.Equal(20f, size.X);
    }

    [Fact]
    public void Layout_ExistingOutput_ShouldAppend()
    {
        var output = new List<PositionedGlyph>();
        TextLayout.Layout("a", new FakeMetrics(), "f", 16, default, output);
        TextLayout.Layout("b", new FakeMetrics(), "f", 16, default, output);

        Assert.Equal(2, output.Count);
    }

    [Fact]
    public void Layout_EmptyText_ShouldReturnZeroSize()
    {
        Run("", default, out var size);

        Assert.Equal(Vector2.Zero, size);
    }

    [Fact]
    public void Measure_ShouldMatchLayoutSize()
    {
        var size = TextLayout.Measure("hello world", new FakeMetrics(), "f", 16);

        Assert.Equal(new Vector2(110f, 20f), size);
    }
}
