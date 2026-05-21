namespace Yaeger.Graphics;

/// <summary>
/// Represents a text component that can be attached to an entity for rendering.
/// </summary>
public record struct Text(string Content, FontHandle Font, int FontSize, Color Color)
{
    public Text(string content, IFontHandle font, int fontSize, Color color)
        : this(content, ToFontHandle(font), fontSize, color) { }

    private static FontHandle ToFontHandle(IFontHandle font)
    {
        ArgumentNullException.ThrowIfNull(font);
        return new FontHandle(font.Id);
    }
}
