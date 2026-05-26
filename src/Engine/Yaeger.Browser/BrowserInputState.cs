using System.Numerics;
using Yaeger.Browser.Interop;
using Yaeger.Input;
using Yaeger.Platform;

namespace Yaeger.Browser;

/// <summary>
/// <see cref="IInputState"/> implementation for the browser, backed by keyboard and mouse
/// event listeners registered by the "yaeger-browser" JavaScript module.
/// </summary>
public sealed class BrowserInputState : IInputState
{
    // Cached scroll delta, snapshotted once per frame at BeginFrame so every reader within
    // a single tick sees the same value — matching the native Mouse.ScrollDelta behavior
    // where Mouse.EndFrame() resets the accumulator once after Render (see Window.cs).
    private static float _scrollDelta;

    /// <summary>
    /// Snapshots the JS scroll accumulator for the current frame and resets it.
    /// Must be called once per tick at the tick boundary, before any game code reads
    /// <see cref="ScrollDelta"/>. This ensures all systems and gameplay code within a single
    /// tick see the same stable scroll value, matching the native <c>Mouse.ScrollDelta</c> behavior.
    /// Callers should invoke this before running update systems (e.g., at the start of
    /// <see cref="GameController.Tick"/>), not during rendering.
    /// </summary>
    public static void BeginFrame()
    {
        _scrollDelta = (float)JsInterop.GetAndResetScrollDelta();
    }

    public bool IsKeyPressed(Keys key) => JsInterop.IsKeyPressed(ToJsKey(key));

    public bool IsMouseButtonPressed(MouseButton button) =>
        JsInterop.IsMouseButtonPressed(ToDomButton(button));

    private static int ToDomButton(MouseButton button) =>
        button switch
        {
            MouseButton.Left => 0,
            MouseButton.Middle => 1,
            MouseButton.Right => 2,
            MouseButton.Side1 => 3,
            MouseButton.Side2 => 4,
            _ => (int)button,
        };

    public Vector2 MousePosition => new((float)JsInterop.GetMouseX(), (float)JsInterop.GetMouseY());

    public Vector2 MousePositionNdc =>
        new((float)JsInterop.GetMouseXNdc(), (float)JsInterop.GetMouseYNdc());

    /// <summary>
    /// Accumulated vertical scroll wheel delta for the current frame.
    /// The value is stable for the entire frame — all reads within a single tick return
    /// the same value, matching the native <c>Mouse.ScrollDelta</c> behavior.
    /// The accumulator is snapshotted and reset once per frame by <see cref="BeginFrame"/>.
    /// </summary>
    public float ScrollDelta => _scrollDelta;

    private static string ToJsKey(Keys key) =>
        key switch
        {
            Keys.W => "w",
            Keys.A => "a",
            Keys.S => "s",
            Keys.D => "d",
            Keys.Q => "q",
            Keys.E => "e",
            Keys.R => "r",
            Keys.I => "i",
            Keys.J => "j",
            Keys.H => "h",
            Keys.C => "c",
            Keys.Space => " ",
            Keys.Escape => "Escape",
            Keys.Up => "ArrowUp",
            Keys.Down => "ArrowDown",
            Keys.Left => "ArrowLeft",
            Keys.Right => "ArrowRight",
            Keys.Num1 => "1",
            Keys.Num2 => "2",
            Keys.Num3 => "3",
            _ => key.ToString(),
        };
}
