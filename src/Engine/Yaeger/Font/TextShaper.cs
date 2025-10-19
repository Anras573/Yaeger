using HarfBuzzSharp;

using Buffer = HarfBuzzSharp.Buffer;

namespace Yaeger.Font;

public struct GlyphInfo
{
    public uint Codepoint { get; set; }
    public int XAdvance { get; set; }
    public int YAdvance { get; set; }
    public int XOffset { get; set; }
    public int YOffset { get; set; }
    public uint Cluster { get; set; }
}

public class TextShaper
{
    private readonly Font _font;

    public TextShaper(Font font)
    {
        _font = font ?? throw new ArgumentNullException(nameof(font));
    }

    public GlyphInfo[] Shape(string text, Direction direction = Direction.LeftToRight)
    {
        if (string.IsNullOrEmpty(text))
        {
            return Array.Empty<GlyphInfo>();
        }

        using var buffer = new Buffer();
        buffer.AddUtf8(text);
        buffer.GuessSegmentProperties();
        buffer.Direction = direction;

        _font.HarfBuzzFont.Shape(buffer);

        var glyphInfos = buffer.GlyphInfos;
        var glyphPositions = buffer.GlyphPositions;
        var result = new GlyphInfo[glyphInfos.Length];

        for (int i = 0; i < glyphInfos.Length; i++)
        {
            result[i] = new GlyphInfo
            {
                Codepoint = glyphInfos[i].Codepoint,
                Cluster = glyphInfos[i].Cluster,
                XAdvance = glyphPositions[i].XAdvance,
                YAdvance = glyphPositions[i].YAdvance,
                XOffset = glyphPositions[i].XOffset,
                YOffset = glyphPositions[i].YOffset
            };
        }

        return result;
    }

    public GlyphInfo[] ShapeWithFeatures(string text, Feature[] features, Direction direction = Direction.LeftToRight)
    {
        if (string.IsNullOrEmpty(text))
        {
            return Array.Empty<GlyphInfo>();
        }

        using var buffer = new Buffer();
        buffer.AddUtf8(text);
        buffer.GuessSegmentProperties();
        buffer.Direction = direction;

        _font.HarfBuzzFont.Shape(buffer, features);

        var glyphInfos = buffer.GlyphInfos;
        var glyphPositions = buffer.GlyphPositions;
        var result = new GlyphInfo[glyphInfos.Length];

        for (int i = 0; i < glyphInfos.Length; i++)
        {
            result[i] = new GlyphInfo
            {
                Codepoint = glyphInfos[i].Codepoint,
                Cluster = glyphInfos[i].Cluster,
                XAdvance = glyphPositions[i].XAdvance,
                YAdvance = glyphPositions[i].YAdvance,
                XOffset = glyphPositions[i].XOffset,
                YOffset = glyphPositions[i].YOffset
            };
        }

        return result;
    }
}