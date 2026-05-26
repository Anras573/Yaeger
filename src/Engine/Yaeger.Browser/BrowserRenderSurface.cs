using System.Numerics;
using Yaeger.Browser.Interop;
using Yaeger.Platform;

namespace Yaeger.Browser;

/// <summary>
/// <see cref="IRenderSurface"/> implementation that draws colored quads onto an HTML5
/// Canvas 2D context via JavaScript interop.
/// Texture paths are ignored; the quad's <c>color</c> tint is used instead.
/// </summary>
public sealed class BrowserRenderSurface(string canvasId) : IRenderSurface
{
    /// <summary>
    /// Initialises the canvas 2D context. Must be called once, after
    /// <c>JSHost.ImportAsync("yaeger-browser", …)</c> has completed.
    /// </summary>
    public void Initialize() => JsInterop.InitCanvas(canvasId);

    public void BeginFrame() => JsInterop.ClearFrame();

    public void EndFrame() { }

    public void FlushQueuedQuads() { }

    public void SetCamera(Matrix4x4 viewProjection) { }

    /// <inheritdoc/>
    /// <remarks>
    /// The model <paramref name="transform"/> is passed as its six 2-D affine components
    /// (m11, m12, m21, m22, tx, ty) directly to the Canvas 2D <c>ctx.transform()</c> call.
    /// The NDC-to-pixel mapping is established once per frame inside <see cref="BeginFrame"/>.
    /// </remarks>
    public void SubmitQuad(Matrix4x4 transform, string texturePath, Vector4 color) =>
        JsInterop.DrawQuad(
            transform.M11,
            transform.M12,
            transform.M21,
            transform.M22,
            transform.M41,
            transform.M42,
            color.X,
            color.Y,
            color.Z,
            color.W
        );

    public void SubmitQuad(
        Matrix4x4 transform,
        string texturePath,
        Vector2 uvMin,
        Vector2 uvMax,
        Vector4 color
    ) => SubmitQuad(transform, texturePath, color);
}
