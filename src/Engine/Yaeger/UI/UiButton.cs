using Yaeger.Graphics;

namespace Yaeger.UI;

/// <summary>
/// Marks an entity as an interactive button. Requires a <see cref="UiRect"/> for hit-testing.
/// Interaction state is written to <see cref="UiButtonState"/> each frame by <c>UiSystem</c>.
/// </summary>
public struct UiButton
{
    public Color Normal;
    public Color Hovered;
    public Color Pressed;
}
