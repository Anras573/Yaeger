using Yaeger.Graphics;

namespace Yaeger.UI;

/// <summary>
/// Renders a text label whose glyph baseline is placed at <see cref="UiRect.Position"/>.
/// Unlike panels and buttons, <see cref="UiRect.Position"/> here is the text <em>baseline</em>
/// origin — text ascenders extend upward (lower Y values) from <c>Position.Y</c>.
/// <see cref="UiRect.Size"/> is ignored; labels are not clipped or laid out within a rectangle.
/// </summary>
public struct UiLabel
{
    public string? Text;
    public Color Color;
    public float FontSize;
}
