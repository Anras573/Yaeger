using System.Numerics;

namespace Yaeger.Graphics;

/// <summary>
/// Six-plane view frustum derived from a combined view-projection matrix.
/// Planes are extracted using the Gribb–Hartmann method.
/// </summary>
public readonly struct CameraFrustum
{
    private readonly Vector4 _left;
    private readonly Vector4 _right;
    private readonly Vector4 _bottom;
    private readonly Vector4 _top;
    private readonly Vector4 _near;
    private readonly Vector4 _far;

    private CameraFrustum(
        Vector4 left,
        Vector4 right,
        Vector4 bottom,
        Vector4 top,
        Vector4 near,
        Vector4 far
    )
    {
        _left = left;
        _right = right;
        _bottom = bottom;
        _top = top;
        _near = near;
        _far = far;
    }

    /// <summary>
    /// Extracts the six frustum planes from the combined view-projection matrix.
    /// The matrix is assumed to use .NET's row-vector convention (clip = world * M)
    /// with a [0, 1] depth range.
    /// </summary>
    public static CameraFrustum FromMatrix(Matrix4x4 viewProj)
    {
        // Gribb–Hartmann plane extraction for row-vector convention.
        // col(n) refers to the n-th column of the matrix (1-indexed).
        // col(1) = (M11, M21, M31, M41), col(4) = (M14, M24, M34, M44)
        var left = Normalize(
            new Vector4(
                viewProj.M11 + viewProj.M14,
                viewProj.M21 + viewProj.M24,
                viewProj.M31 + viewProj.M34,
                viewProj.M41 + viewProj.M44
            )
        );
        var right = Normalize(
            new Vector4(
                viewProj.M14 - viewProj.M11,
                viewProj.M24 - viewProj.M21,
                viewProj.M34 - viewProj.M31,
                viewProj.M44 - viewProj.M41
            )
        );
        var bottom = Normalize(
            new Vector4(
                viewProj.M12 + viewProj.M14,
                viewProj.M22 + viewProj.M24,
                viewProj.M32 + viewProj.M34,
                viewProj.M42 + viewProj.M44
            )
        );
        var top = Normalize(
            new Vector4(
                viewProj.M14 - viewProj.M12,
                viewProj.M24 - viewProj.M22,
                viewProj.M34 - viewProj.M32,
                viewProj.M44 - viewProj.M42
            )
        );
        // Near plane: z_clip >= 0, i.e. dot(p, col(3)) >= 0
        var near = Normalize(
            new Vector4(viewProj.M13, viewProj.M23, viewProj.M33, viewProj.M43)
        );
        var far = Normalize(
            new Vector4(
                viewProj.M14 - viewProj.M13,
                viewProj.M24 - viewProj.M23,
                viewProj.M34 - viewProj.M33,
                viewProj.M44 - viewProj.M43
            )
        );

        return new CameraFrustum(left, right, bottom, top, near, far);
    }

    /// <summary>
    /// Returns true if the AABB (transformed by <paramref name="model"/>) intersects or is
    /// inside the frustum. Returns false only when all 8 corners are on the negative side of
    /// any single frustum plane (fully outside).
    /// </summary>
    public bool Intersects(Aabb3D aabb, Matrix4x4 model)
    {
        Span<Vector3> corners = stackalloc Vector3[8];
        var min = aabb.Min;
        var max = aabb.Max;

        corners[0] = Vector3.Transform(new Vector3(min.X, min.Y, min.Z), model);
        corners[1] = Vector3.Transform(new Vector3(max.X, min.Y, min.Z), model);
        corners[2] = Vector3.Transform(new Vector3(min.X, max.Y, min.Z), model);
        corners[3] = Vector3.Transform(new Vector3(max.X, max.Y, min.Z), model);
        corners[4] = Vector3.Transform(new Vector3(min.X, min.Y, max.Z), model);
        corners[5] = Vector3.Transform(new Vector3(max.X, min.Y, max.Z), model);
        corners[6] = Vector3.Transform(new Vector3(min.X, max.Y, max.Z), model);
        corners[7] = Vector3.Transform(new Vector3(max.X, max.Y, max.Z), model);

        return !IsOutside(corners, _left)
            && !IsOutside(corners, _right)
            && !IsOutside(corners, _bottom)
            && !IsOutside(corners, _top)
            && !IsOutside(corners, _near)
            && !IsOutside(corners, _far);
    }

    private static bool IsOutside(Span<Vector3> corners, Vector4 plane)
    {
        var normal = new Vector3(plane.X, plane.Y, plane.Z);
        foreach (var corner in corners)
        {
            if (Vector3.Dot(normal, corner) + plane.W >= 0f)
                return false;
        }
        return true;
    }

    private static Vector4 Normalize(Vector4 plane)
    {
        var length = MathF.Sqrt(plane.X * plane.X + plane.Y * plane.Y + plane.Z * plane.Z);
        return length > 0f ? plane / length : plane;
    }
}
