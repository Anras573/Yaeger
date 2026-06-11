using System.Numerics;

namespace Yaeger.UI;

/// <summary>
/// Defines the screen-space bounds of a UI element. Position is the top-left corner
/// in client pixels; origin is the top-left of the window with Y increasing downward.
/// </summary>
public struct UiRect
{
    public Vector2 Position;
    public Vector2 Size;
}
