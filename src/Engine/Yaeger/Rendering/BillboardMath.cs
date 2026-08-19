using System.Numerics;

namespace Yaeger.Rendering;

/// <summary>
/// Pure CPU-side math for camera-facing particle billboards — no GL calls, so (like
/// <see cref="TransparencySorter"/>, <see cref="CameraFrustum"/>, and
/// <see cref="MeshInstanceBatcher"/>) this is unit-testable without a live OpenGL context.
/// </summary>
public static class BillboardMath
{
    /// <summary>
    /// Extracts the world-space right and up axes of the camera that produced <paramref name="view"/>
    /// (a <see cref="Matrix4x4.CreateLookAt"/>-shaped matrix, as returned by <c>Camera3D.ViewMatrix</c>).
    /// A billboard quad built from these two axes always faces the camera, regardless of the
    /// camera's own orientation. Falls back to the world +X/+Y axes if a row degenerates to zero
    /// (a non-invertible/degenerate view matrix, which a valid camera never produces).
    /// </summary>
    public static (Vector3 Right, Vector3 Up) ExtractCameraAxes(Matrix4x4 view)
    {
        // CreateLookAt lays the camera's world-space right/up/back axes down its first three
        // *columns* (M11,M21,M31 / M12,M22,M32 / M13,M23,M33) — reading down a column recovers the
        // corresponding world-space axis directly, with no matrix inversion needed.
        var right = new Vector3(view.M11, view.M21, view.M31);
        var up = new Vector3(view.M12, view.M22, view.M32);

        return (Normalize(right, Vector3.UnitX), Normalize(up, Vector3.UnitY));
    }

    private static Vector3 Normalize(Vector3 v, Vector3 fallback) =>
        v.LengthSquared() > 1e-12f ? Vector3.Normalize(v) : fallback;

    /// <summary>
    /// Projects <paramref name="velocity"/> onto the camera's (<paramref name="cameraRight"/>,
    /// <paramref name="cameraUp"/>) plane and returns the projected speed (used to elongate a
    /// velocity-stretched billboard) and the angle, in radians, to rotate the billboard's local
    /// +X axis onto that projected direction. A particle moving straight toward/away from the
    /// camera projects to (near) zero speed — correctly collapsing a stretched billboard back to
    /// its round/square base size instead of showing a spurious streak — in which case the
    /// rotation is 0 (arbitrary, since the billboard is square at that point anyway).
    /// </summary>
    public static (float ProjectedSpeed, float Rotation) ProjectVelocity(
        Vector3 velocity,
        Vector3 cameraRight,
        Vector3 cameraUp
    )
    {
        var x = Vector3.Dot(velocity, cameraRight);
        var y = Vector3.Dot(velocity, cameraUp);
        var projectedSpeed = MathF.Sqrt(x * x + y * y);

        return projectedSpeed > 1e-6f ? (projectedSpeed, MathF.Atan2(y, x)) : (0f, 0f);
    }
}
