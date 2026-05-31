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
    /// <paramref name="matrix64bytes"/> is the 64 raw bytes of a System.Numerics.Matrix4x4
    /// (16 × IEEE 754 single-precision floats, row-major). The JS side reinterprets the
    /// Uint8Array as a Float32Array before passing it to uniformMatrix4fv(transpose=false),
    /// matching the convention used by the desktop OpenGL renderer.
    /// </summary>
    [JSImport("setViewProjection", "yaeger-browser")]
    public static partial void SetViewProjection(byte[] matrix64bytes);

    /// <summary>
    /// Draws one texture batch. <paramref name="vertexBytes"/> contains the raw bytes of the
    /// float vertex scratch buffer (9 floats × 4 bytes per vertex, 4 vertices per quad);
    /// only the first <c>quadCount * 4 * 9 * 4</c> bytes are uploaded to the GPU.
    /// </summary>
    [JSImport("drawBatch", "yaeger-browser")]
    public static partial void DrawBatch(string textureUrl, byte[] vertexBytes, int quadCount);

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
