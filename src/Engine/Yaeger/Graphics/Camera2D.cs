using System.Numerics;

namespace Yaeger.Graphics;

/// <summary>
/// 2D camera component. Attach to an entity to make <c>RenderSystem</c> apply a view-projection
/// to the scene. When no camera entity exists, rendering falls back to an identity view
/// (NDC-direct). The first camera encountered wins if multiple are present.
/// </summary>
/// <remarks>
/// At <see cref="Zoom"/> = 1 with aspect ratio A, the visible world span is
/// [-A, A] × [-1, 1]. Increasing <see cref="Zoom"/> narrows that span (things appear larger).
/// <see cref="Rotation"/> is camera rotation in radians; a positive value rotates the camera
/// counter-clockwise, which makes the world appear to rotate clockwise relative to the camera.
/// </remarks>
public record struct Camera2D(Vector2 Position, float Zoom = 1f, float Rotation = 0f)
{
    public Camera2D()
        : this(Vector2.Zero) { }

    /// <summary>
    /// Builds the combined view-projection matrix for the given window aspect ratio (width / height).
    /// </summary>
    public Matrix4x4 ViewProjection(float aspectRatio)
    {
        var view =
            Matrix4x4.CreateTranslation(-Position.X, -Position.Y, 0f)
            * Matrix4x4.CreateRotationZ(-Rotation)
            * Matrix4x4.CreateScale(Zoom);

        var projection = Matrix4x4.CreateOrthographic(2f * aspectRatio, 2f, -1f, 1f);

        return view * projection;
    }
}
