namespace Yaeger.UI;

/// <summary>
/// Written each frame by <c>UiSystem</c> based on mouse input and hit-testing.
/// Read this component in game code to react to button interaction.
/// </summary>
public struct UiButtonState
{
    public bool IsHovered;
    public bool IsPressed;

    /// <summary>True for exactly one frame: the frame the left button was released over this button.</summary>
    public bool WasClicked;
}
