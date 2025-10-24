using System.Numerics;

using Silk.NET.OpenGL;

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
/// Uses SkiaSharp for glyph rasterization and OpenGL for texture storage.
/// </summary>
public class GlyphAtlas : IDisposable
{
    private readonly GL _gl;
    private readonly Font _font;
    private readonly int _fontSize;
    private readonly Dictionary<uint, AtlasGlyph> _glyphs = new();
    private uint _textureId;
    private readonly int _atlasWidth;
    private readonly int _atlasHeight;
    private bool _disposed;

    public uint TextureId => _textureId;
    public int AtlasWidth => _atlasWidth;
    public int AtlasHeight => _atlasHeight;

    public GlyphAtlas(GL gl, Font font, int fontSize = 48, int atlasSize = 512)
    {
        _gl = gl ?? throw new ArgumentNullException(nameof(gl));
        _font = font ?? throw new ArgumentNullException(nameof(font));
        _fontSize = fontSize;
        _atlasWidth = atlasSize;
        _atlasHeight = atlasSize;

        InitializeAtlas();
    }

    private unsafe void InitializeAtlas()
    {
        // Create OpenGL texture
        _textureId = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, _textureId);

        // Create empty texture with alpha channel
        var emptyData = new byte[_atlasWidth * _atlasHeight];
        fixed (byte* data = emptyData)
        {
            _gl.TexImage2D(TextureTarget.Texture2D, 0, (int)InternalFormat.R8,
                (uint)_atlasWidth, (uint)_atlasHeight, 0,
                PixelFormat.Red, PixelType.UnsignedByte, data);
        }

        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);

        _gl.BindTexture(TextureTarget.Texture2D, 0);
    }

    /// <summary>
    /// Adds a glyph to the atlas. If the glyph is already in the atlas, returns the existing entry.
    /// </summary>
    public AtlasGlyph AddGlyph(uint codepoint)
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

    private unsafe AtlasGlyph RenderGlyph(uint codepoint)
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

        _gl.BindTexture(TextureTarget.Texture2D, _textureId);
        fixed (byte* data = glyphData)
        {
            _gl.TexSubImage2D(TextureTarget.Texture2D, 0, x, y,
                (uint)_fontSize, (uint)_fontSize,
                PixelFormat.Red, PixelType.UnsignedByte, data);
        }
        _gl.BindTexture(TextureTarget.Texture2D, 0);

        return atlasGlyph;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _gl.DeleteTexture(_textureId);
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}