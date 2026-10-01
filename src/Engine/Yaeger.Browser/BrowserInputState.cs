using System.Numerics;
using Yaeger.Browser.Interop;
using Yaeger.Input;
using Yaeger.Platform;

namespace Yaeger.Browser;

/// <summary>
/// <see cref="IInputState"/> implementation for the browser, backed by keyboard and pointer
/// event listeners registered by the "yaeger-browser" JavaScript module.
/// Touch and pen input are mapped into existing mouse-style semantics by the JS layer.
/// </summary>
public sealed class BrowserInputState : IInputState
{
    // Cached scroll delta, snapshotted once per frame at BeginFrame so every reader within
    // a single tick sees the same value. In the browser host, the JS accumulator is reset
    // during BeginFrame snapshot; EndFrame only clears this cached frame value.
    private static float _scrollDelta;
    private static bool _frameStarted;

    /// <summary>
    /// Snapshots the JS scroll accumulator for the current frame and resets it.
    /// Must be called once per tick at the tick boundary, before any game code reads
    /// <see cref="ScrollDelta"/>. This ensures all systems and gameplay code within a single
    /// tick see the same stable scroll value, consistent with native <c>Mouse.ScrollDelta</c>
    /// read behavior (without implying identical reset timing).
    /// Callers should invoke this before running update systems (e.g., at the start of
    /// the host tick method), not during rendering.
    /// </summary>
    public static void BeginFrame()
    {
        if (_frameStarted)
            return;

        _scrollDelta = (float)JsInterop.GetAndResetScrollDelta();
        _frameStarted = true;
    }

    /// <summary>
    /// Marks the current frame complete so the next <see cref="BeginFrame"/> call can
    /// snapshot fresh browser input.
    /// </summary>
    public static void EndFrame()
    {
        _scrollDelta = 0f;
        _frameStarted = false;
    }

    /// <summary>
    /// Opts the given keys out of the browser's default behaviour (page scroll on Space/arrows,
    /// focus change on Tab, reload on F5, ...) while the page has focus. Replaces any previous
    /// list; pass an empty collection to restore defaults. Nothing is suppressed by default.
    /// Browser/OS shortcuts the page can't intercept (e.g. Ctrl+W) are unaffected.
    /// </summary>
    public static void SetPreventDefaultKeys(IEnumerable<Keys> keys) =>
        JsInterop.SetPreventDefaultKeys([
            .. keys.SelectMany(BrowserKeyMapper.GetDomCodes).Distinct(),
        ]);

    public bool IsKeyPressed(Keys key)
    {
        foreach (var code in BrowserKeyMapper.GetDomCodes(key))
        {
            if (JsInterop.IsKeyPressed(code))
                return true;
        }
        return false;
    }

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
    /// The accumulator is snapshotted and reset once per frame by <see cref="BeginFrame"/>;
    /// <see cref="EndFrame"/> allows the next frame to snapshot again.
    /// </summary>
    public float ScrollDelta => _scrollDelta;
}
