using System;
using System.Numerics;
using Yaeger.Browser.Interop;
using Yaeger.Platform;

namespace Yaeger.Browser;

/// <summary>
/// <see cref="IRenderSurface"/> implementation that draws colored quads onto an HTML5
/// Canvas 2D context via JavaScript interop.
/// Texture paths and UV coordinates are ignored; the quad's <c>color</c> tint is used instead.
/// When <see cref="SetCamera"/> has been called with a non-identity matrix, that
/// view-projection is incorporated into every quad's transform before submission.
/// </summary>
public sealed class BrowserRenderSurface(string canvasId) : IRenderSurface, IDisposable
{
    private Matrix4x4 _viewProjection = Matrix4x4.Identity;

    /// <summary>
    /// Initialises the canvas 2D context. Must be called once, after
    /// <c>JSHost.ImportAsync("yaeger-browser", …)</c> has completed.
    /// </summary>
    public void Initialize() => JsInterop.InitCanvas(canvasId);

    public void Dispose() => JsInterop.DisposeCanvas();

    public void BeginFrame()
    {
        // Primary path: BrowserInputState.BeginFrame() is called at the tick boundary
        // (before update systems run) in GameController.Tick(), so scroll is snapshotted
        // before Update runs. This call is a defensive fallback if a host misses the
        // tick-boundary call; gameplay code should still read ScrollDelta during Update.
        BrowserInputState.BeginFrame();
        JsInterop.ClearFrame();
    }

    public void EndFrame() => BrowserInputState.EndFrame();

    public void FlushQueuedQuads() { }

    /// <summary>
    /// Stores the view-projection matrix that will be combined with every subsequent
    /// quad transform.  Uses row-major (System.Numerics) convention:
    /// <c>combined = model * viewProjection</c>.
    /// </summary>
    public void SetCamera(Matrix4x4 viewProjection) => _viewProjection = viewProjection;

    /// <inheritdoc/>
    /// <remarks>
    /// The model <paramref name="transform"/> is multiplied by the stored view-projection
    /// matrix (row-major: <c>combined = transform * _viewProjection</c>) and the six 2-D
    /// affine components (m11, m12, m21, m22, tx, ty) of the result are passed to the
    /// Canvas 2D <c>ctx.transform()</c> call.
    /// </remarks>
    public void SubmitQuad(Matrix4x4 transform, string texturePath, Vector4 color)
    {
        var combined = transform * _viewProjection;
        JsInterop.DrawQuad(
            combined.M11,
            combined.M12,
            combined.M21,
            combined.M22,
            combined.M41,
            combined.M42,
            color.X,
            color.Y,
            color.Z,
            color.W
        );
    }

    public void SubmitQuad(
        Matrix4x4 transform,
        string texturePath,
        Vector2 uvMin,
        Vector2 uvMax,
        Vector4 color
    ) => SubmitQuad(transform, texturePath, color);
}
