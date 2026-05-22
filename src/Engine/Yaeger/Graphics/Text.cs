namespace Yaeger.Graphics;

/// <summary>
/// Represents a text component that can be attached to an entity for rendering.
/// </summary>
public record struct Text
{
    private Font.Font? _nativeFont;
    private FontHandle _fontHandle;

    public string Content { get; set; }

    public FontHandle FontHandle
    {
        get => _fontHandle;
        set
        {
            _ = value.Id;
            _fontHandle = value;
            _nativeFont = null;
        }
    }

    public int FontSize { get; set; }
    public Color Color { get; set; }

    public Font.Font Font
    {
        get =>
            _nativeFont
            ?? throw new InvalidOperationException(
                "Text does not have a native Yaeger.Font.Font instance. Use FontHandle for rendering."
            );
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _nativeFont = value;
            _fontHandle = new FontHandle(value.Id);
        }
    }

    /// <summary>
    /// Returns the native font instance when this component was created from one.
    /// </summary>
    public bool TryGetNativeFont(out Font.Font nativeFont)
    {
        if (_nativeFont is null)
        {
            nativeFont = default!;
            return false;
        }

        nativeFont = _nativeFont;
        return true;
    }

    public Text(string content, Font.Font font, int fontSize, Color color)
    {
        ArgumentNullException.ThrowIfNull(font);

        Content = content;
        _fontHandle = new FontHandle(font.Id);
        FontSize = fontSize;
        Color = color;
        _nativeFont = font;
    }

    public Text(string content, FontHandle font, int fontSize, Color color)
    {
        _ = font.Id;
        Content = content;
        _fontHandle = font;
        FontSize = fontSize;
        Color = color;
        _nativeFont = null;
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
        _nativeFont = font as Font.Font;
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
