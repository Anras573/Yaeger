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

    public float ScrollDelta => (float)JsInterop.GetAndResetScrollDelta();

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
