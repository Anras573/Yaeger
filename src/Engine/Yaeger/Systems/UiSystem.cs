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
    public void Update(float deltaTime)
    {
        var mousePos = Mouse.Position;
        var isMousePressed = Mouse.IsButtonPressed(MouseButton.Left);
        var stateStore = world.GetStore<UiButtonState>();

        foreach ((Entity entity, UiRect rect, UiButton _) in world.Query<UiRect, UiButton>())
        {
            var isHovered = HitTest(mousePos, rect);
            var isPressed = isHovered && isMousePressed;
            // A click requires the press to have started on this entity (IsPressed last frame).
            var wasPressed = stateStore.TryGet(entity, out var prev) && prev.IsPressed;
            var wasClicked = isHovered && wasPressed && !isMousePressed;

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
    }

    private static bool HitTest(Vector2 mousePos, UiRect rect) =>
        mousePos.X >= rect.Position.X
        && mousePos.X <= rect.Position.X + rect.Size.X
        && mousePos.Y >= rect.Position.Y
        && mousePos.Y <= rect.Position.Y + rect.Size.Y;
}
