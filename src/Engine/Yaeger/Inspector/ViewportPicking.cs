using System.Numerics;
using Yaeger.Graphics;

namespace Yaeger.Inspector;

/// <summary>
/// Pure ray/projection math backing viewport click-to-select and translate-axis dragging: screen ↔
/// world conversions, ray-vs-AABB picking, the closest point on a drag axis to the camera ray, and
/// 2D oriented-rectangle hit-testing. No GPU or ECS state — everything here takes plain matrices and
/// vectors, so it is fully unit-testable independent of a live window.
/// </summary>
public static class ViewportPicking
{
    /// <summary>
    /// Builds a world-space ray through OpenGL NDC point <paramref name="ndc"/> (as returned by
    /// <c>Mouse.PositionNdc</c>) for the given view-projection matrix, by unprojecting the near and
    /// far points and taking their difference as the ray direction. Fails (returns <c>false</c>) if
    /// <paramref name="viewProj"/> is singular or the input is non-finite.
    /// </summary>
    public static bool TryGetPickRay(
        Vector2 ndc,
        Matrix4x4 viewProj,
        out Vector3 origin,
        out Vector3 direction
    )
    {
        origin = Vector3.Zero;
        direction = Vector3.Zero;

        if (!IsFinite(ndc) || !Matrix4x4.Invert(viewProj, out var inverse))
            return false;

        var near = UnprojectPoint(ndc, 0f, inverse);
        var far = UnprojectPoint(ndc, 1f, inverse);
        var diff = far - near;
        var lengthSq = diff.LengthSquared();
        if (!float.IsFinite(lengthSq) || lengthSq < 1e-12f)
            return false;

        origin = near;
        direction = diff / MathF.Sqrt(lengthSq);
        return true;
    }

    /// <summary>
    /// Unprojects OpenGL NDC point <paramref name="ndc"/> straight to a world-space point in the
    /// Z = 0 plane, for orthographic 2D picking — no ray needed since 2D entities all live at Z = 0.
    /// Fails if <paramref name="viewProj"/> is singular or the input is non-finite.
    /// </summary>
    public static bool TryUnprojectPoint2D(Vector2 ndc, Matrix4x4 viewProj, out Vector2 world)
    {
        world = Vector2.Zero;

        if (!IsFinite(ndc) || !Matrix4x4.Invert(viewProj, out var inverse))
            return false;

        var point = UnprojectPoint(ndc, 0f, inverse);
        if (!float.IsFinite(point.X) || !float.IsFinite(point.Y))
            return false;

        world = new Vector2(point.X, point.Y);
        return true;
    }

    /// <summary>
    /// Projects a world-space point to client-pixel screen coordinates (top-left origin, Y-down —
    /// matching <c>Mouse.Position</c>), for hit-testing gizmo handles against the cursor at a fixed
    /// pixel tolerance regardless of camera zoom/distance.
    /// </summary>
    public static Vector2 WorldToScreen(Vector3 world, Matrix4x4 viewProj, Vector2 windowSize)
    {
        var clip = Vector4.Transform(new Vector4(world, 1f), viewProj);
        if (MathF.Abs(clip.W) > 1e-8f)
            clip /= clip.W;

        var x = (clip.X * 0.5f + 0.5f) * windowSize.X;
        var y = (1f - (clip.Y * 0.5f + 0.5f)) * windowSize.Y;
        return new Vector2(x, y);
    }

    /// <summary>
    /// Ray-vs-oriented-box test: transforms the ray into the box's local space via the inverse of
    /// <paramref name="model"/> and runs a standard slab test against <paramref name="box"/>. On a
    /// hit, <paramref name="distance"/> is the ray parameter of the nearest intersection (clamped to
    /// the entry point, or the exit point if the ray starts inside the box).
    /// </summary>
    public static bool TryIntersectRayAabb(
        Vector3 rayOrigin,
        Vector3 rayDirection,
        Aabb3D box,
        Matrix4x4 model,
        out float distance
    )
    {
        distance = 0f;
        if (!Matrix4x4.Invert(model, out var inverseModel))
            return false;

        var localOrigin = Vector3.Transform(rayOrigin, inverseModel);
        var localDirection = Vector3.TransformNormal(rayDirection, inverseModel);

        var tMin = float.NegativeInfinity;
        var tMax = float.PositiveInfinity;

        if (
            !SlabIntersect(
                localOrigin.X,
                localDirection.X,
                box.Min.X,
                box.Max.X,
                ref tMin,
                ref tMax
            )
        )
            return false;
        if (
            !SlabIntersect(
                localOrigin.Y,
                localDirection.Y,
                box.Min.Y,
                box.Max.Y,
                ref tMin,
                ref tMax
            )
        )
            return false;
        if (
            !SlabIntersect(
                localOrigin.Z,
                localDirection.Z,
                box.Min.Z,
                box.Max.Z,
                ref tMin,
                ref tMax
            )
        )
            return false;

        // The box is entirely behind the ray's origin.
        if (tMax < 0f)
            return false;

        distance = tMin >= 0f ? tMin : tMax;
        return true;
    }

    private static bool SlabIntersect(
        float origin,
        float direction,
        float min,
        float max,
        ref float tMin,
        ref float tMax
    )
    {
        if (MathF.Abs(direction) < 1e-12f)
            // Ray parallel to this slab: only intersects if the origin already lies within it.
            return origin >= min && origin <= max;

        var t1 = (min - origin) / direction;
        var t2 = (max - origin) / direction;
        if (t1 > t2)
            (t1, t2) = (t2, t1);

        tMin = MathF.Max(tMin, t1);
        tMax = MathF.Min(tMax, t2);
        return tMin <= tMax;
    }

    /// <summary>
    /// Finds the point along the infinite line through <paramref name="axisOrigin"/> in unit
    /// direction <paramref name="axisDirection"/> that is closest to the (skew) ray
    /// <paramref name="rayOrigin"/> + t·<paramref name="rayDirection"/>, returning the axis
    /// parameter <paramref name="axisParam"/> (the point itself is
    /// <paramref name="axisOrigin"/> + <paramref name="axisParam"/>·<paramref name="axisDirection"/>).
    /// This is the standard closest-point-between-two-lines solution specialised to unit direction
    /// vectors. When the two lines are (near-)parallel — the camera looking straight down the axis —
    /// falls back to projecting the ray origin onto the axis, since the skew-line solution is
    /// undefined there.
    /// </summary>
    public static bool TryClosestPointOnAxisToRay(
        Vector3 axisOrigin,
        Vector3 axisDirection,
        Vector3 rayOrigin,
        Vector3 rayDirection,
        out float axisParam
    )
    {
        axisParam = 0f;
        if (
            !IsFinite(axisOrigin)
            || !IsFinite(axisDirection)
            || !IsFinite(rayOrigin)
            || !IsFinite(rayDirection)
        )
            return false;

        var r = axisOrigin - rayOrigin;
        var b = Vector3.Dot(axisDirection, rayDirection);
        var c = Vector3.Dot(axisDirection, r);
        var f = Vector3.Dot(rayDirection, r);
        var denom = 1f - b * b;

        axisParam = MathF.Abs(denom) < 1e-9f ? c : (b * f - c) / denom;
        return float.IsFinite(axisParam);
    }

    /// <summary>
    /// Projects a 2D world point onto the infinite line through <paramref name="axisOrigin"/> in
    /// unit direction <paramref name="axisDirection"/>, returning the axis parameter (the point
    /// itself is <paramref name="axisOrigin"/> + parameter·<paramref name="axisDirection"/>).
    /// </summary>
    public static float ClosestPointOnAxis2D(
        Vector2 axisOrigin,
        Vector2 axisDirection,
        Vector2 point
    ) => Vector2.Dot(point - axisOrigin, axisDirection);

    /// <summary>Shortest distance from <paramref name="point"/> to the segment [<paramref name="a"/>, <paramref name="b"/>].</summary>
    public static float DistancePointToSegment(Vector2 point, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        var lengthSq = ab.LengthSquared();
        var t = lengthSq > 1e-12f ? Math.Clamp(Vector2.Dot(point - a, ab) / lengthSq, 0f, 1f) : 0f;
        var closest = a + ab * t;
        return Vector2.Distance(point, closest);
    }

    /// <summary>
    /// Tests whether <paramref name="point"/> lies inside the rectangle centred at
    /// <paramref name="center"/> with the given <paramref name="halfExtents"/>, rotated by
    /// <paramref name="rotationRadians"/> about its center — mirrors the bounds
    /// <see cref="GizmoBuilder.AddRect"/> draws for a <see cref="Yaeger.Graphics.Transform2D"/>'s
    /// sprite quad, so 2D viewport picking selects exactly what the gizmo outlines.
    /// </summary>
    public static bool PointInOrientedRect(
        Vector2 point,
        Vector2 center,
        Vector2 halfExtents,
        float rotationRadians
    )
    {
        // Rotate the point into the rectangle's local (unrotated) space by the inverse rotation.
        var cos = MathF.Cos(-rotationRadians);
        var sin = MathF.Sin(-rotationRadians);
        var local = point - center;
        var localX = local.X * cos - local.Y * sin;
        var localY = local.X * sin + local.Y * cos;
        return MathF.Abs(localX) <= halfExtents.X && MathF.Abs(localY) <= halfExtents.Y;
    }

    private static Vector3 UnprojectPoint(Vector2 ndc, float z, Matrix4x4 inverseViewProj)
    {
        var clip = new Vector4(ndc.X, ndc.Y, z, 1f);
        var world = Vector4.Transform(clip, inverseViewProj);
        if (MathF.Abs(world.W) > 1e-8f)
            world /= world.W;
        return new Vector3(world.X, world.Y, world.Z);
    }

    private static bool IsFinite(Vector2 v) => float.IsFinite(v.X) && float.IsFinite(v.Y);

    private static bool IsFinite(Vector3 v) =>
        float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);
}
