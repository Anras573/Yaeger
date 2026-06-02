using System.Numerics;

namespace Yaeger.Graphics;

/// <summary>
/// 3D perspective camera component. Attach to an entity to provide a view + projection matrix
/// pair for 3D rendering. The first camera entity encountered wins if multiple are present.
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
    public Camera3D()
        : this(new Vector3(0, 2, 5), Vector3.Zero, Vector3.UnitY, MathF.PI / 4, 0.1f, 1000f) { }

    public static Camera3D Default =>
        new(new Vector3(0, 2, 5), Vector3.Zero, Vector3.UnitY, MathF.PI / 4, 0.1f, 1000f);

    public Matrix4x4 ViewMatrix
    {
        get
        {
            // Guard against default(Camera3D): Position == Target or zero Up produces NaN.
            if (Position == Target || Up == Vector3.Zero)
                return Matrix4x4.Identity;
            return Matrix4x4.CreateLookAt(Position, Target, Up);
        }
    }

    public Matrix4x4 ProjectionMatrix(float aspectRatio)
    {
        // Guard against default(Camera3D): zero Fov/Near/Far throw inside
        // CreatePerspectiveFieldOfView.
        var fov = Fov > 0f ? Fov : MathF.PI / 4;
        var near = Near > 0f ? Near : 0.1f;
        var far = Far > near ? Far : near + 1000f;
        return Matrix4x4.CreatePerspectiveFieldOfView(fov, aspectRatio, near, far);
    }

    public Matrix4x4 ViewProjection(float aspectRatio) =>
        ViewMatrix * ProjectionMatrix(aspectRatio);
}
