using HarfBuzzSharp;

using Buffer = HarfBuzzSharp.Buffer;

namespace Yaeger.Font;

public class Font : IDisposable
{
    private readonly Blob _blob;
    private readonly Face _face;
    private readonly HarfBuzzSharp.Font _font;
    private bool _disposed;

    public Font(string fontPath)
    {
        if (!File.Exists(fontPath))
        {
            throw new FileNotFoundException($"Font file not found: {fontPath}");
        }

        FontBytes = File.ReadAllBytes(fontPath);
        _blob = Blob.FromStream(new MemoryStream(FontBytes));
        _face = new Face(_blob, 0);
        _font = new HarfBuzzSharp.Font(_face);
    }

    public GlyphInfo[] Shape(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        using var buffer = new Buffer();
        buffer.AddUtf8(text);
        buffer.GuessSegmentProperties();
        buffer.Direction = Direction.LeftToRight;

        _font.Shape(buffer);

        var glyphInfos = buffer.GlyphInfos;
        var glyphPositions = buffer.GlyphPositions;
        var result = new GlyphInfo[glyphInfos.Length];

        // Convert text to codepoints for lookup
        var codepoints = text.EnumerateRunes().Select(r => (uint)r.Value).ToArray();

        for (int i = 0; i < glyphInfos.Length; i++)
        {
            result[i] = new GlyphInfo
            {
                GlyphIndex = glyphInfos[i].Codepoint,  // This is actually glyph index
                Codepoint = codepoints[glyphInfos[i].Cluster],  // Original character codepoint
                Cluster = glyphInfos[i].Cluster,
                XAdvance = glyphPositions[i].XAdvance,
                YAdvance = glyphPositions[i].YAdvance,
                XOffset = glyphPositions[i].XOffset,
                YOffset = glyphPositions[i].YOffset
            };
        }

        return result;
    }

    public byte[] FontBytes { get; }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _font?.Dispose();
        _face?.Dispose();
        _blob?.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}