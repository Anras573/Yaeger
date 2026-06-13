using Yaeger.Graphics;

namespace Yaeger.UI;

/// <summary>
/// Renders a text label anchored at the top-left corner of <see cref="UiRect.Position"/>,
/// consistent with panels and buttons. The renderer offsets to the glyph baseline internally.
/// <see cref="UiRect.Size"/> is ignored; labels are not clipped or laid out within a rectangle.
/// </summary>
public struct UiLabel
{
    public string? Text;
    public Color Color;
    public float FontSize;
}
