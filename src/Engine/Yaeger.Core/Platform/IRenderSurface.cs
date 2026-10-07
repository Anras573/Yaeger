using System.Numerics;

namespace Yaeger.Platform;

/// <summary>
/// Rendering abstraction for sprite submission and frame orchestration.
/// </summary>
public interface IRenderSurface
{
    void BeginFrame();
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
