using Yaeger.Font;

namespace Yaeger.Graphics;

/// <summary>
/// Represents a text component that can be attached to an entity for rendering.
/// </summary>
public readonly struct Text
{
    public string Content { get; }
    public Font.Font Font { get; }
    public int FontSize { get; }
    public Color Color { get; }

    public Text(string content, Font.Font font, int fontSize = 48, Color? color = null)
    {
        Content = content ?? throw new ArgumentNullException(nameof(content));
        Font = font ?? throw new ArgumentNullException(nameof(font));
        FontSize = fontSize;
        Color = color ?? Graphics.Color.White;
    }
}