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
}
