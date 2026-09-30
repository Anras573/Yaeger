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
