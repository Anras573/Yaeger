using System.Numerics;

namespace Yaeger.Platform;

/// <summary>
/// Rendering abstraction for sprite submission and frame orchestration.
/// </summary>
public interface IRenderSurface
{
    void BeginFrame();

    /// <summary>
    /// Begins a frame, optionally without clearing the target. <c>clear: false</c> lets a
    /// second pass (an overlay or HUD) draw on top of what an earlier pass rendered. The
    /// default implementation calls <see cref="BeginFrame()"/> when <paramref name="clear"/>
    /// is <see langword="true"/> and does nothing otherwise; the built-in surfaces override it
    /// to keep their per-frame housekeeping while skipping only the clear.
    /// </summary>
    void BeginFrame(bool clear)
    {
        if (clear)
            BeginFrame();
    }
    void EndFrame();
    void FlushQueuedQuads();

    /// <summary>
    /// Sets the view-projection applied to quads submitted from now on. If the matrix
    /// differs from the current one, quads already queued are flushed first, so they are
    /// drawn with the matrix that was active when they were submitted.
    /// </summary>
    void SetCamera(Matrix4x4 viewProjection);
    void SubmitQuad(Matrix4x4 transform, string texturePath, Vector4 color);
    void SubmitQuad(
        Matrix4x4 transform,
        string texturePath,
        Vector2 uvMin,
        Vector2 uvMax,
        Vector4 color
    );

    /// <summary>
    /// Queues a quad from a pre-resolved 2D affine placement instead of a full
    /// <see cref="Matrix4x4"/>: <paramref name="center"/> is the quad centre and
    /// <paramref name="xAxis"/> / <paramref name="yAxis"/> are the world-space edge vectors
    /// (the unit quad's X and Y steps after transformation). It draws exactly what the
    /// equivalent matrix would, but lets bulk submitters (tilemaps) skip building a matrix
    /// per quad. The default builds that matrix and forwards; the built-in surfaces override
    /// it to queue the placement directly.
    /// </summary>
    void SubmitQuad(
        Vector2 center,
        Vector2 xAxis,
        Vector2 yAxis,
        string texturePath,
        Vector2 uvMin,
        Vector2 uvMax,
        Vector4 color
    ) =>
        SubmitQuad(
            new Matrix4x4(
                xAxis.X,
                xAxis.Y,
                0f,
                0f,
                yAxis.X,
                yAxis.Y,
                0f,
                0f,
                0f,
                0f,
                1f,
                0f,
                center.X,
                center.Y,
                0f,
                1f
            ),
            texturePath,
            uvMin,
            uvMax,
            color
        );

    /// <summary>
    /// Pixel size of the texture at <paramref name="path"/>, or <see cref="Vector2.Zero"/> if
    /// unknown (the default). Used to inset sprite-sheet/tile UVs against neighbour bleed.
    /// Wrappers/decorators around another surface must forward this; otherwise they inherit
    /// the default and the anti-bleed inset is silently skipped.
    /// </summary>
    Vector2 GetTextureSize(string path) => Vector2.Zero;

    /// <summary>
    /// Texture path meaning "no texture": a quad submitted with it is drawn as a flat
    /// <c>color</c>. It shares the regular batched quad path.
    /// </summary>
    const string SolidTexturePath = "";

    /// <summary>
    /// Queues an untextured quad filled with <paramref name="color"/>. <paramref name="transform"/>
    /// maps the unit quad (centred on the origin, spanning -0.5..0.5) exactly like the
    /// textured overloads.
    /// </summary>
    void SubmitQuad(Matrix4x4 transform, Vector4 color) =>
        SubmitQuad(transform, SolidTexturePath, color);

    /// <summary>
    /// Queues a flat-coloured line segment from <paramref name="from"/> to <paramref name="to"/>
    /// (world units), <paramref name="thickness"/> wide, as a thin rotated quad at z = 0.
    /// </summary>
    void SubmitLine(Vector2 from, Vector2 to, float thickness, Vector4 color) =>
        SubmitQuad(LineGeometry.CreateTransform(from, to, thickness), color);
}
