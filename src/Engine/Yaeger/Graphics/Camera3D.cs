using System.Numerics;

namespace Yaeger.Graphics;

/// <summary>
/// 3D perspective camera component. Attach to an entity to make <c>MeshRenderSystem</c> upload
/// a combined view-projection matrix as <c>uViewProj</c>. When no camera entity exists the
/// system falls back to its own default. The first camera encountered wins if multiple are present.
/// </summary>
public record struct Camera3D(
    Vector3 Position,
    Vector3 Target,
    Vector3 Up,
    float Fov,
    float Near,
    float Far
)
{
    public static Camera3D Default =>
        new(new Vector3(0, 2, 5), Vector3.Zero, Vector3.UnitY, MathF.PI / 4, 0.1f, 1000f);

    public Matrix4x4 ViewMatrix => Matrix4x4.CreateLookAt(Position, Target, Up);

    public Matrix4x4 ProjectionMatrix(float aspectRatio) =>
        Matrix4x4.CreatePerspectiveFieldOfView(Fov, aspectRatio, Near, Far);

    public Matrix4x4 ViewProjection(float aspectRatio) =>
        ViewMatrix * ProjectionMatrix(aspectRatio);
}
