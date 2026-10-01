namespace Yaeger.Graphics;

/// <summary>
/// Represents a text component that can be attached to an entity for rendering.
/// </summary>
/// <remarks>
/// Platform-agnostic; native-only helpers live in <c>TextNativeExtensions</c>.
/// </remarks>
public record struct Text
{
    private IFontHandle? _fontObject;
    private FontHandle _fontHandle;

    public string Content { get; set; }

    public FontHandle FontHandle
    {
        get => _fontHandle;
        set
        {
            _ = value.Id;
            _fontHandle = value;
            _fontObject = null;
        }
    }

    public int FontSize { get; set; }
    public Color Color { get; set; }

    /// <summary>
    /// The font object this component was created from (e.g. a native font), or
    /// <see langword="null"/> when it only carries a <see cref="FontHandle"/>.
    /// </summary>
    public readonly IFontHandle? FontObject => _fontObject;

    public Text(string content, FontHandle font, int fontSize, Color color)
    {
        _ = font.Id;
        Content = content;
        _fontHandle = font;
        FontSize = fontSize;
        Color = color;
        _fontObject = null;
    }

    public Text(string content, IFontHandle font, int fontSize, Color color)
    {
        ArgumentNullException.ThrowIfNull(font);
        var handle = ToFontHandle(font);
        _ = handle.Id;

        Content = content;
        _fontHandle = handle;
        FontSize = fontSize;
        Color = color;
        _fontObject = font is FontHandle ? null : font;
    }

    public void Deconstruct(
        out string content,
        out FontHandle font,
        out int fontSize,
        out Color color
    )
    {
        content = Content;
        font = _fontHandle;
        fontSize = FontSize;
        color = Color;
    }

    private static FontHandle ToFontHandle(IFontHandle font) =>
        font is FontHandle handle ? handle : new FontHandle(font.Id);
}
