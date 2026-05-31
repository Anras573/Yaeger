using System.Runtime.InteropServices.JavaScript;

namespace Yaeger.Browser.Interop;

/// <summary>
/// JavaScript interop declarations for the Yaeger browser runtime.
/// All functions are exported by the "yaeger-browser" ES module loaded at startup.
/// </summary>
internal static partial class JsInterop
{
    [JSImport("initWebGL", "yaeger-browser")]
    public static partial void InitWebGL(string canvasId);

    [JSImport("clearFrame", "yaeger-browser")]
    public static partial void ClearFrame();

    [JSImport("disposeCanvas", "yaeger-browser")]
    public static partial void DisposeCanvas();

    /// <summary>
    /// Sets the view-projection matrix uniform used by all subsequent draw calls.
    /// <paramref name="matrix16"/> is the 16 elements of a System.Numerics.Matrix4x4 in
    /// row-major order (M11…M44). WebGL reads it as column-major (transpose=false), which
    /// matches the convention used by the desktop OpenGL renderer.
    /// </summary>
    [JSImport("setViewProjection", "yaeger-browser")]
    public static partial void SetViewProjection(float[] matrix16);

    /// <summary>
    /// Draws one texture batch. <paramref name="vertices"/> is the full vertex scratch buffer
    /// (9 floats per vertex, 4 vertices per quad); only the first
    /// <c>quadCount * 4 * 9</c> floats are uploaded to the GPU.
    /// </summary>
    [JSImport("drawBatch", "yaeger-browser")]
    public static partial void DrawBatch(string textureUrl, float[] vertices, int quadCount);

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
