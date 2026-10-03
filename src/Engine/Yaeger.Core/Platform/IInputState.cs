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

    /// <summary>
    /// True if <paramref name="key"/> went down since the previous frame boundary. Unlike
    /// <see cref="IsKeyPressed"/>, a press and release that both land inside one frame still
    /// report here (and in <see cref="WasKeyReleased"/>).
    /// </summary>
    bool WasKeyPressed(Keys key);

    /// <summary>True if <paramref name="key"/> went up since the previous frame boundary.</summary>
    bool WasKeyReleased(Keys key);

    /// <summary>True if <paramref name="button"/> went down since the previous frame boundary.</summary>
    bool WasMouseButtonPressed(MouseButton button);

    /// <summary>True if <paramref name="button"/> went up since the previous frame boundary.</summary>
    bool WasMouseButtonReleased(MouseButton button);

    Vector2 MousePosition { get; }
    Vector2 MousePositionNdc { get; }
    float ScrollDelta { get; }
}
