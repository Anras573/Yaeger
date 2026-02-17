using System.Numerics;

using Silk.NET.OpenGL;

using SkiaSharp;

using Yaeger.Rendering;

namespace Yaeger.Font;

/// <summary>
/// Manages a texture atlas of rendered glyphs for efficient text rendering.
/// Uses SkiaSharp for glyph rasterization and OpenGL for texture storage.
/// </summary>
public class GlyphAtlas : IDisposable
{
    private readonly Font _font;
    private readonly int _fontSize;
    private readonly Dictionary<uint, AtlasGlyph> _glyphs = new();
    private readonly FontTexture _texture;
    private readonly int _atlasWidth;
    private readonly int _atlasHeight;
    private readonly SKTypeface _typeface;
    private readonly SKFont _skFont;
    private bool _disposed;
    private int _nextAtlasIndex = 0;

    public GlyphAtlas(GL gl, Font font, int fontSize = 48, int atlasSize = 512)
    {
        _font = font ?? throw new ArgumentNullException(nameof(font));
        _fontSize = fontSize;
        _atlasWidth = atlasSize;
        _atlasHeight = atlasSize;

        _texture = new FontTexture(gl, _atlasWidth, _atlasHeight);

        // Create SkiaSharp typeface from font bytes
        using var fontData = SKData.CreateCopy(_font.FontBytes);
        _typeface = SKTypeface.FromData(fontData) ?? throw new InvalidOperationException("Failed to create typeface from font data");

        // Create SKFont with the desired size
        _skFont = new SKFont(_typeface, _fontSize);
        _skFont.Edging = SKFontEdging.Antialias;
    }

    /// <summary>
    /// Adds a glyph to the atlas. If the glyph is already in the atlas, returns the existing entry.
    /// </summary>
    private AtlasGlyph AddGlyph(uint codepoint)
    {
        if (_glyphs.TryGetValue(codepoint, out var existingGlyph))
        {
            return existingGlyph;
        }

        // Render glyph using SkiaSharp
        var glyphInfo = RenderGlyph(codepoint);
        _glyphs[codepoint] = glyphInfo;
        return glyphInfo;
    }

    /// <summary>
    /// Batch adds multiple glyphs to the atlas for a given text string.
    /// </summary>
    public AtlasGlyph[] AddGlyphsForText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return [];

        var glyphs = _font.Shape(text);

        return glyphs
            .Select(g => AddGlyph(g.Codepoint))
            .ToArray();
    }

    /// <summary>
    /// Gets a glyph from the atlas. Returns null if the glyph hasn't been added yet.
    /// </summary>
    public AtlasGlyph? GetGlyph(uint codepoint)
    {
        return _glyphs.TryGetValue(codepoint, out var glyph) ? glyph : null;
    }

    private AtlasGlyph RenderGlyph(uint codepoint)
    {
        // Calculate position in atlas (simple grid layout)
        int glyphIndex = _nextAtlasIndex++;
        int glyphsPerRow = _atlasWidth / _fontSize;
        int x = (glyphIndex % glyphsPerRow) * _fontSize;
        int y = (glyphIndex / glyphsPerRow) * _fontSize;

        // Convert codepoint to string for rendering
        var text = char.ConvertFromUtf32((int)codepoint);

        // Measure the glyph using SKFont
        var advance = _skFont.MeasureText(text, out SKRect bounds);

        // Add 1px padding to account for anti-aliasing pixels at edges
        const int padding = 0; // Set to 0 for now, can be adjusted if needed

        // Calculate metrics with padding
        var glyphWidth = (int)Math.Ceiling(bounds.Width) + padding * 2;
        var glyphHeight = (int)Math.Ceiling(bounds.Height) + padding * 2;
        var bearing = new Vector2(bounds.Left - padding, -bounds.Top + padding);

        // Ensure glyph fits in the allocated space
        var renderWidth = Math.Min(Math.Max(glyphWidth, 1), _fontSize);
        var renderHeight = Math.Min(Math.Max(glyphHeight, 1), _fontSize);

        // Create a bitmap to render the glyph
        var imageInfo = new SKImageInfo(_fontSize, _fontSize, SKColorType.Alpha8);
        using var surface = SKSurface.Create(imageInfo);

        if (surface == null)
        {
            // Fallback to empty glyph if surface creation fails
            return CreateEmptyGlyph(codepoint, x, y, advance);
        }

        var canvas = surface.Canvas;

        // Clear the canvas
        canvas.Clear(SKColors.Transparent);

        // Create paint for rendering the glyph
        using var paint = new SKPaint();
        paint.Color = SKColors.White;
        paint.IsAntialias = true;

        // Draw the glyph with padding offset to preserve anti-aliased edges
        canvas.DrawText(text, -bounds.Left + padding, -bounds.Top + padding, SKTextAlign.Left, _skFont, paint);

        // Get the pixel data
        using var image = surface.Snapshot();
        using var pixmap = image.PeekPixels();

        if (pixmap != null)
        {
            var pixelSpan = pixmap.GetPixelSpan();
            // Upload to texture atlas
            _texture.SetData(pixelSpan, x, y, _fontSize, _fontSize);
        }

        // Create atlas glyph with actual metrics
        // Texture coordinates must match the actual glyph size, not the full cell
        var atlasGlyph = new AtlasGlyph
        {
            Codepoint = codepoint,
            TexCoordMin = new Vector2((float)x / _atlasWidth, (float)(y + renderHeight) / _atlasHeight),
            TexCoordMax = new Vector2((float)(x + renderWidth) / _atlasWidth, (float)y / _atlasHeight),
            Size = new Vector2(renderWidth, renderHeight),
            Bearing = bearing,
            Advance = advance
        };

        return atlasGlyph;
    }

    private AtlasGlyph CreateEmptyGlyph(uint codepoint, int x, int y, float advance)
    {
        // Create an empty glyph as fallback
        var glyphData = new byte[_fontSize * _fontSize];
        _texture.SetData(new ReadOnlySpan<byte>(glyphData), x, y, _fontSize, _fontSize);

        return new AtlasGlyph
        {
            Codepoint = codepoint,
            TexCoordMin = new Vector2((float)x / _atlasWidth, (float)(y + _fontSize) / _atlasHeight),
            TexCoordMax = new Vector2((float)(x + _fontSize) / _atlasWidth, (float)y / _atlasHeight),
            Size = new Vector2(0, 0),
            Bearing = Vector2.Zero,
            Advance = advance
        };
    }

    public void BindTexture() => _texture.Bind();

    public void UnbindTexture() => _texture.Unbind();

    public void Dispose()
    {
        if (_disposed)
            return;

        _skFont?.Dispose();
        _typeface?.Dispose();
        _texture.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}