using System.Runtime.InteropServices.JavaScript;

namespace Yaeger.Browser.Interop;

/// <summary>
/// JavaScript interop declarations for the Yaeger browser runtime.
/// All functions are exported by the "yaeger-browser" ES module loaded at startup.
/// </summary>
internal static partial class JsInterop
{
    [JSImport("startGameLoop", "yaeger-browser")]
    public static partial void StartGameLoop(
        [JSMarshalAs<JSType.Function<JSType.Number>>] Action<double> tick
    );

    [JSImport("stopGameLoop", "yaeger-browser")]
    public static partial void StopGameLoop();

    [JSImport("initWebGL", "yaeger-browser")]
    public static partial void InitWebGL(string canvasId);

    [JSImport("clearFrame", "yaeger-browser")]
    public static partial void ClearFrame();

    /// <summary>
    /// Returns <c>[clientWidth, clientHeight, devicePixelRatio]</c> of the canvas (CSS pixels).
    /// </summary>
    [JSImport("getViewport", "yaeger-browser")]
    [return: JSMarshalAs<JSType.Array<JSType.Number>>]
    public static partial double[] GetViewport();

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

    [JSImport("takeKeyDownCodes", "yaeger-browser")]
    public static partial string[] TakeKeyDownCodes();

    [JSImport("takeKeyUpCodes", "yaeger-browser")]
    public static partial string[] TakeKeyUpCodes();

    [JSImport("takeMouseDownButtons", "yaeger-browser")]
    public static partial int[] TakeMouseDownButtons();

    [JSImport("takeMouseUpButtons", "yaeger-browser")]
    public static partial int[] TakeMouseUpButtons();

    [JSImport("setPreventDefaultKeys", "yaeger-browser")]
    public static partial void SetPreventDefaultKeys(string[] codes);

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

    [JSImport("fontLoad", "yaeger-browser")]
    [return: JSMarshalAs<JSType.Promise<JSType.Void>>]
    public static partial Task FontLoad(string family, string url);

    /// <summary>Returns <c>[lineHeight, ascent]</c> in CSS pixels.</summary>
    [JSImport("fontLineMetrics", "yaeger-browser")]
    [return: JSMarshalAs<JSType.Array<JSType.Number>>]
    public static partial double[] FontLineMetrics(string family, int sizePx, double dpr);

    /// <summary>
    /// Rasterizes missing glyphs and returns 11 numbers per codepoint:
    /// <c>[codepoint, page, advance, offsetX, offsetY, width, height, u0, v0, u1, v1]</c>.
    /// </summary>
    [JSImport("glyphAtlasEnsure", "yaeger-browser")]
    [return: JSMarshalAs<JSType.Array<JSType.Number>>]
    public static partial double[] GlyphAtlasEnsure(
        string family,
        int sizePx,
        double dpr,
        string pathPrefix,
        int[] codepoints
    );

    [JSImport("audioInit", "yaeger-browser")]
    public static partial void AudioInit(int maxVoicesPerSound);

    [JSImport("audioSetVolumes", "yaeger-browser")]
    public static partial void AudioSetVolumes(double master, double music, double sfx);

    /// <summary>Fetches and decodes a sound; resolves to a handle id, or rejects on failure.</summary>
    [JSImport("audioLoad", "yaeger-browser")]
    [return: JSMarshalAs<JSType.Promise<JSType.Number>>]
    public static partial Task<int> AudioLoad(string path);

    /// <param name="group">0 = music, 1 = SFX (the <c>AudioGroup</c> ordinal).</param>
    [JSImport("audioPlay", "yaeger-browser")]
    public static partial void AudioPlay(int id, double gain, double pitch, int group);

    [JSImport("audioOpenStream", "yaeger-browser")]
    public static partial int AudioOpenStream(string path);

    [JSImport("audioStreamPlay", "yaeger-browser")]
    public static partial void AudioStreamPlay(int id);

    [JSImport("audioStreamPause", "yaeger-browser")]
    public static partial void AudioStreamPause(int id);

    [JSImport("audioStreamStop", "yaeger-browser")]
    public static partial void AudioStreamStop(int id);

    [JSImport("audioStreamSetLooping", "yaeger-browser")]
    public static partial void AudioStreamSetLooping(int id, bool looping);

    [JSImport("audioStreamSetGain", "yaeger-browser")]
    public static partial void AudioStreamSetGain(int id, double gain);

    [JSImport("audioStreamDispose", "yaeger-browser")]
    public static partial void AudioStreamDispose(int id);
}
