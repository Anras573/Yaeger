using System.Numerics;
using Yaeger.ECS;
using Yaeger.Input;
using Yaeger.Platform;
using Yaeger.UI;

namespace Yaeger.Systems;

/// <summary>
/// Performs mouse hit-testing against all entities that have both a <see cref="UiRect"/>
/// and a <see cref="UiButton"/>, then writes the result to <see cref="UiButtonState"/>.
/// Call <see cref="Update"/> from your game's update loop before rendering.
/// </summary>
public class UiSystem(World world, IInputState inputState) : IUpdateSystem
{
    private readonly HashSet<Entity> _pressStartedOn = [];

    public void Update(float deltaTime)
    {
        var mousePos = inputState.MousePosition;
        var isMouseHeld = inputState.IsMouseButtonPressed(MouseButton.Left);
        // Edges, not the held level: a press + release inside one frame (a fast click, or a tap
        // on a slow browser frame) never shows up as held but still reports both edges.
        var pressedThisFrame = inputState.WasMouseButtonPressed(MouseButton.Left);
        var releasedThisFrame = inputState.WasMouseButtonReleased(MouseButton.Left);

        foreach ((Entity entity, UiRect rect, UiButton _) in world.Query<UiRect, UiButton>())
        {
            var isHovered = HitTest(mousePos, rect);

            if (pressedThisFrame && isHovered)
                _pressStartedOn.Add(entity);

            var startedHere = _pressStartedOn.Contains(entity);
            var isPressed = isHovered && isMouseHeld && startedHere;
            var wasClicked = isHovered && releasedThisFrame && startedHere;

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

        // A release ends the gesture; also self-heal if the button is no longer held without a
        // release edge having been observed (e.g. focus loss).
        if (releasedThisFrame || !isMouseHeld)
            _pressStartedOn.Clear();
    }

    private static bool HitTest(Vector2 mousePos, UiRect rect) =>
        mousePos.X >= rect.Position.X
        && mousePos.X < rect.Position.X + rect.Size.X
        && mousePos.Y >= rect.Position.Y
        && mousePos.Y < rect.Position.Y + rect.Size.Y;
}
