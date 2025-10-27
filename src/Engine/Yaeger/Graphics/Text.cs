namespace Yaeger.Graphics;

/// <summary>
/// Represents a text component that can be attached to an entity for rendering.
/// </summary>
public readonly struct Text(string content, Font.Font font, int fontSize = 48, Color? color = null)
{
    public string Content { get; } = content ?? throw new ArgumentNullException(nameof(content));
    public Font.Font Font { get; } = font ?? throw new ArgumentNullException(nameof(font));
    public int FontSize { get; } = fontSize;
    public Color Color { get; } = color ?? Color.White;
}