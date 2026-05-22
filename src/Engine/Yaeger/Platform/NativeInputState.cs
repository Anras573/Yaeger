using System.Numerics;
using Yaeger.Input;

namespace Yaeger.Platform;

/// <summary>
/// Native input adapter over Yaeger's static keyboard and mouse surfaces.
/// </summary>
public sealed class NativeInputState : IInputState
{
    public bool IsKeyPressed(Keys key) => Keyboard.IsKeyPressed(key);

    public bool IsMouseButtonPressed(MouseButton button) => Mouse.IsButtonPressed(button);

    public Vector2 MousePosition => Mouse.Position;

    public Vector2 MousePositionNdc => Mouse.PositionNdc;

    public float ScrollDelta => Mouse.ScrollDelta;
}
