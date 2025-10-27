namespace Yaeger.Font;

public record struct GlyphInfo(uint GlyphIndex, uint Codepoint, int XAdvance, int YAdvance, int XOffset, int YOffset, uint Cluster);