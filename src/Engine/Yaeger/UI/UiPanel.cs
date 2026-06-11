using Yaeger.Graphics;

namespace Yaeger.UI;

/// <summary>
/// Renders a solid-color background rectangle for the entity's <see cref="UiRect"/>.
/// </summary>
public struct UiPanel
{
    public Color BackgroundColor;

    /// <summary>Border radius hint. Reserved for future use; not yet rendered.</summary>
    public float BorderRadius;
}
