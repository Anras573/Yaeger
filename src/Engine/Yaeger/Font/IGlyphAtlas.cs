namespace Yaeger.Font;

internal interface IGlyphAtlas : IDisposable
{
    AtlasGlyph[] AddGlyphsForText(string text);

    AtlasGlyph? GetGlyph(uint codepoint);

    void BindTexture();

    void UnbindTexture();
}
