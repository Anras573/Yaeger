using System.Numerics;
using Yaeger.Input;

namespace Yaeger.Platform;

/// <summary>
/// Read-only input abstraction used by gameplay systems.
/// </summary>
public interface IInputState
{
    bool IsKeyPressed(Keys key);
    bool IsMouseButtonPressed(MouseButton button);
    Vector2 MousePosition { get; }
    Vector2 MousePositionNdc { get; }
    float ScrollDelta { get; }
}
