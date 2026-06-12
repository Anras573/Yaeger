using Yaeger.Graphics;

namespace Yaeger.UI;

/// <summary>
/// Renders a text label at <see cref="UiRect.Position"/> (used as the baseline origin).
/// <see cref="UiRect.Size"/> is ignored; labels are not clipped or laid out within a rectangle.
/// </summary>
public struct UiLabel
{
    public string? Text;
    public Color Color;
    public float FontSize;
}
