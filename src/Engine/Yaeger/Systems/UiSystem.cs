using System.Numerics;
using Yaeger.ECS;
using Yaeger.Input;
using Yaeger.UI;

namespace Yaeger.Systems;

/// <summary>
/// Performs mouse hit-testing against all entities that have both a <see cref="UiRect"/>
/// and a <see cref="UiButton"/>, then writes the result to <see cref="UiButtonState"/>.
/// Call <see cref="Update"/> from your game's update loop before rendering.
/// </summary>
public class UiSystem(World world) : IUpdateSystem
{
    private bool _wasMousePressed;

    public void Update(float deltaTime)
    {
        var mousePos = Mouse.Position;
        var isMousePressed = Mouse.IsButtonPressed(MouseButton.Left);

        foreach (
            (Entity entity, UiRect rect, UiButton _) in world.Query<UiRect, UiButton>()
        )
        {
            var isHovered = HitTest(mousePos, rect);
            var isPressed = isHovered && isMousePressed;
            var wasClicked = isHovered && _wasMousePressed && !isMousePressed;

            world.AddComponent(
                entity,
                new UiButtonState
                {
                    IsHovered = isHovered,
                    IsPressed = isPressed,
                    WasClicked = wasClicked,
                }
            );
        }

        _wasMousePressed = isMousePressed;
    }

    private static bool HitTest(Vector2 mousePos, UiRect rect) =>
        mousePos.X >= rect.Position.X
        && mousePos.X <= rect.Position.X + rect.Size.X
        && mousePos.Y >= rect.Position.Y
        && mousePos.Y <= rect.Position.Y + rect.Size.Y;
}
