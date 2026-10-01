using System.Numerics;
using Yaeger.Browser.Interop;
using Yaeger.Graphics;
using Yaeger.Platform;

namespace Yaeger.Browser;

/// <summary>
/// <see cref="ITextRenderSurface"/> for the browser host. Glyphs are rasterized by the browser's
/// own font stack into Canvas 2D atlases (see <see cref="BrowserGlyphAtlas"/>), laid out by the
/// platform-independent <see cref="TextLayout"/>, and submitted as textured quads through the
/// render surface's batched path, so text interleaves with sprites in submission order.
/// </summary>
/// <remarks>
/// A font handle's <c>Id</c> is a CSS font family: a system font (<c>"sans-serif"</c>) or one
/// registered with <see cref="LoadFontAsync"/>. Fonts must be loaded before first use.
/// <see cref="TextRenderMode"/> doesn't apply here — Canvas 2D output is already anti-aliased.
/// </remarks>
public sealed class BrowserTextRenderSurface : ITextRenderSurface
{
    private readonly IRenderSurface _surface;
    private readonly IGlyphMetricsProvider _metrics;
    private readonly BrowserGlyphAtlas? _atlas;
    private readonly List<PositionedGlyph> _glyphs = [];

    public BrowserTextRenderSurface(BrowserRenderSurface surface)
        : this(surface, new BrowserGlyphAtlas(() => surface.PixelRatio)) { }

    public BrowserTextRenderSurface(IRenderSurface surface, IGlyphMetricsProvider metrics)
    {
        _surface = surface;
        _metrics = metrics;
        _atlas = metrics as BrowserGlyphAtlas;
    }

    /// <summary>Layout options applied to every draw (wrap width, alignment).</summary>
    public TextLayoutOptions Options { get; set; }

    /// <summary>
    /// Registers a font file (<c>.woff2</c>, <c>.ttf</c>, …) under <paramref name="family"/>;
    /// use <c>new FontHandle(family)</c> afterwards.
    /// </summary>
    public static Task LoadFontAsync(string family, string url) => JsInterop.FontLoad(family, url);

    public void DrawText(
        string text,
        Matrix4x4 transform,
        FontHandle font,
        int fontSize,
        Color color
    ) => Draw(text, transform, font.Id, fontSize, color);

    public void DrawText(
        string text,
        Matrix4x4 transform,
        IFontHandle font,
        int fontSize,
        Color color
    )
    {
        ArgumentNullException.ThrowIfNull(font);
        Draw(text, transform, font.Id, fontSize, color);
    }

    private void Draw(string text, Matrix4x4 transform, string fontKey, int fontSize, Color color)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fontSize);
        if (string.IsNullOrEmpty(text))
            return;

        _atlas?.Prepare(fontKey, fontSize, text);
        _glyphs.Clear();
        TextLayout.Layout(text, _metrics, fontKey, fontSize, Options, _glyphs);

        var tint = color.ToVector4();
        foreach (var g in _glyphs)
        {
            // Unit quad (centred, -0.5..0.5) -> glyph rect in layout pixels -> caller's transform.
            var quad =
                Matrix4x4.CreateScale(g.Size.X, g.Size.Y, 1f)
                * Matrix4x4.CreateTranslation(
                    g.Position.X + g.Size.X * 0.5f,
                    g.Position.Y + g.Size.Y * 0.5f,
                    0f
                )
                * transform;
            _surface.SubmitQuad(quad, g.TexturePath, g.UvMin, g.UvMax, tint);
        }
    }
}
