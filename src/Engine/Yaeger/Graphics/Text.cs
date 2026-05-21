namespace Yaeger.Graphics;

/// <summary>
/// Represents a text component that can be attached to an entity for rendering.
/// </summary>
public record struct Text(string Content, Font.Font Font, int FontSize, Color Color)
{
    public Text(string content, IFontHandle font, int fontSize, Color color)
        : this(content, ToNativeFont(font), fontSize, color) { }

    private static Font.Font ToNativeFont(IFontHandle font)
    {
        ArgumentNullException.ThrowIfNull(font);
        return font as Font.Font
            ?? throw new InvalidOperationException(
                "Yaeger.Graphics.Text requires a native Yaeger.Font.Font when used from Yaeger."
            );
    }
}
