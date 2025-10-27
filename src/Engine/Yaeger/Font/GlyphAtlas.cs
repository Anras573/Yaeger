using System.Numerics;

using Silk.NET.OpenGL;

using Yaeger.Rendering;

namespace Yaeger.Font;

/// <summary>
/// Represents a glyph in the atlas with its texture coordinates and metrics.
/// </summary>
public struct AtlasGlyph
{
    public uint Codepoint { get; set; }
    public Vector2 TexCoordMin { get; set; }
    public Vector2 TexCoordMax { get; set; }
    public Vector2 Size { get; set; }
    public Vector2 Bearing { get; set; }
    public float Advance { get; set; }
}

/// <summary>
/// Manages a texture atlas of rendered glyphs for efficient text rendering.
/// Uses SkiaSharp for glyph rasterization and OpenGL for _texture storage.
/// </summary>
public class GlyphAtlas : IDisposable
{
    private readonly Font _font;
    private readonly int _fontSize;
    private readonly Dictionary<uint, AtlasGlyph> _glyphs = new();
    private readonly FontTexture _texture;
    private readonly int _atlasWidth;
    private readonly int _atlasHeight;
    private bool _disposed;

    public GlyphAtlas(GL gl, Font font, int fontSize = 48, int atlasSize = 512)
    {
        _font = font ?? throw new ArgumentNullException(nameof(font));
        _fontSize = fontSize;
        _atlasWidth = atlasSize;
        _atlasHeight = atlasSize;

        _texture = new FontTexture(gl, _atlasWidth, _atlasHeight);
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
    public void AddGlyphsForText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return;

        var shaper = new TextShaper(_font);
        var glyphs = shaper.Shape(text);

        foreach (var glyph in glyphs)
        {
            AddGlyph(glyph.Codepoint);
        }
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
        // Create a SkiaSharp typeface from the font data
        // For now, we'll create a simple placeholder implementation
        // A full implementation would require extracting font data from HarfBuzz

        // Calculate position in atlas (simple grid layout)
        int glyphIndex = _glyphs.Count;
        int glyphsPerRow = _atlasWidth / _fontSize;
        int x = (glyphIndex % glyphsPerRow) * _fontSize;
        int y = (glyphIndex / glyphsPerRow) * _fontSize;

        // For now, create a simple rectangular glyph representation
        // A full implementation would render actual glyph shapes using SkiaSharp
        var atlasGlyph = new AtlasGlyph
        {
            Codepoint = codepoint,
            TexCoordMin = new Vector2((float)x / _atlasWidth, (float)y / _atlasHeight),
            TexCoordMax = new Vector2((float)(x + _fontSize) / _atlasWidth, (float)(y + _fontSize) / _atlasHeight),
            Size = new Vector2(_fontSize * 0.6f, _fontSize * 0.8f),
            Bearing = new Vector2(0, _fontSize * 0.8f),
            Advance = _fontSize * 0.6f
        };

        // Upload a simple white square for now (placeholder)
        // A full implementation would render the actual glyph
        var glyphData = new byte[_fontSize * _fontSize];
        Array.Fill<byte>(glyphData, 255);
        
        _texture.SetData(new ReadOnlySpan<byte>(glyphData), x, y, _fontSize, _fontSize);

        return atlasGlyph;
    }
    
    public void BindTexture() => _texture.Bind();

    public void UnbindTexture() => _texture.Unbind();

    public void Dispose()
    {
        if (_disposed)
            return;

        _texture.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}