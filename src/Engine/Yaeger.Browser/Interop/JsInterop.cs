using System.Runtime.InteropServices.JavaScript;

namespace Yaeger.Browser.Interop;

/// <summary>
/// JavaScript interop declarations for the Yaeger browser runtime.
/// All functions are exported by the "yaeger-browser" ES module loaded at startup.
/// </summary>
internal static partial class JsInterop
{
    [JSImport("initCanvas", "yaeger-browser")]
    public static partial void InitCanvas(string canvasId);

    [JSImport("clearFrame", "yaeger-browser")]
    public static partial void ClearFrame();

    /// <summary>
    /// Draws a unit quad [-0.5, 0.5]^2.
    /// m11/m12/m21/m22 are the 2-D linear part of the model matrix (row-major .NET convention).
    /// tx/ty are the translation.  r/g/b/a are the fill color (0–1 each).
    /// </summary>
    [JSImport("drawQuad", "yaeger-browser")]
    public static partial void DrawQuad(
        double m11,
        double m12,
        double m21,
        double m22,
        double tx,
        double ty,
        double r,
        double g,
        double b,
        double a
    );

    [JSImport("isKeyPressed", "yaeger-browser")]
    public static partial bool IsKeyPressed(string key);

    [JSImport("isMouseButtonPressed", "yaeger-browser")]
    public static partial bool IsMouseButtonPressed(int button);

    [JSImport("getMouseX", "yaeger-browser")]
    public static partial double GetMouseX();

    [JSImport("getMouseY", "yaeger-browser")]
    public static partial double GetMouseY();

    [JSImport("getMouseXNdc", "yaeger-browser")]
    public static partial double GetMouseXNdc();

    [JSImport("getMouseYNdc", "yaeger-browser")]
    public static partial double GetMouseYNdc();

    [JSImport("getAndResetScrollDelta", "yaeger-browser")]
    public static partial double GetAndResetScrollDelta();
}
