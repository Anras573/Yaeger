using System.Numerics;
using Yaeger.Graphics;

namespace Yaeger.Physics;

/// <summary>
/// Ray-vs-oriented-box intersection, shared by <see cref="WorldRaycastExtensions.Raycast"/>
/// (gameplay 3D queries against <see cref="Aabb3D"/> bounds) and the editor's viewport
/// click-to-select (<c>Yaeger.Inspector.ViewportPicking.TryIntersectRayAabb</c>) — one
/// implementation instead of two copies of the same slab test.
/// </summary>
public static class RayAabbIntersection
{
    /// <summary>
    /// Transforms the ray into the box's local space via the inverse of <paramref name="model"/>
    /// and runs a standard slab test against <paramref name="box"/>. On a hit,
    /// <paramref name="distance"/> is the ray parameter of the nearest intersection (clamped to
    /// the entry point, or the exit point if the ray starts inside the box), and
    /// <paramref name="normal"/> is the corresponding face normal, transformed back to world
    /// space via the model's inverse-transpose (so it stays perpendicular under non-uniform
    /// scale) and normalized.
    /// </summary>
    public static bool TryIntersect(
        Vector3 rayOrigin,
        Vector3 rayDirection,
        Aabb3D box,
        Matrix4x4 model,
        out float distance,
        out Vector3 normal
    )
    {
        distance = 0f;
        normal = Vector3.Zero;
        if (!Matrix4x4.Invert(model, out var inverseModel))
            return false;

        var localOrigin = Vector3.Transform(rayOrigin, inverseModel);
        var localDirection = Vector3.TransformNormal(rayDirection, inverseModel);

        var tMin = float.NegativeInfinity;
        var tMax = float.PositiveInfinity;
        var entryAxis = -1;
        var entrySign = 0f;
        var exitAxis = -1;
        var exitSign = 0f;

        if (
            !SlabIntersect(
                localOrigin.X,
                localDirection.X,
                box.Min.X,
                box.Max.X,
                0,
                ref tMin,
                ref tMax,
                ref entryAxis,
                ref entrySign,
                ref exitAxis,
                ref exitSign
            )
        )
            return false;
        if (
            !SlabIntersect(
                localOrigin.Y,
                localDirection.Y,
                box.Min.Y,
                box.Max.Y,
                1,
                ref tMin,
                ref tMax,
                ref entryAxis,
                ref entrySign,
                ref exitAxis,
                ref exitSign
            )
        )
            return false;
        if (
            !SlabIntersect(
                localOrigin.Z,
                localDirection.Z,
                box.Min.Z,
                box.Max.Z,
                2,
                ref tMin,
                ref tMax,
                ref entryAxis,
                ref entrySign,
                ref exitAxis,
                ref exitSign
            )
        )
            return false;

        // The box is entirely behind the ray's origin.
        if (tMax < 0f)
            return false;

        var startsInside = tMin < 0f;
        distance = startsInside ? tMax : tMin;
        var axis = startsInside ? exitAxis : entryAxis;
        var sign = startsInside ? exitSign : entrySign;

        var localNormal = axis switch
        {
            0 => new Vector3(sign, 0f, 0f),
            1 => new Vector3(0f, sign, 0f),
            2 => new Vector3(0f, 0f, sign),
            _ => Vector3.Zero,
        };

        if (localNormal != Vector3.Zero)
        {
            var normalMatrix = Matrix4x4.Transpose(inverseModel);
            var worldNormal = Vector3.TransformNormal(localNormal, normalMatrix);
            var worldNormalLengthSq = worldNormal.LengthSquared();
            if (worldNormalLengthSq > 1e-12f)
                normal = worldNormal / MathF.Sqrt(worldNormalLengthSq);
        }

        if (normal == Vector3.Zero)
            // Degenerate: no axis contributed a face (or the transformed normal collapsed under
            // an extreme scale) — fall back to a normal facing back along the ray so callers
            // still get a finite, ray-facing vector instead of zero.
            normal = -rayDirection;

        return true;
    }

    private static bool SlabIntersect(
        float origin,
        float direction,
        float min,
        float max,
        int axisIndex,
        ref float tMin,
        ref float tMax,
        ref int entryAxis,
        ref float entrySign,
        ref int exitAxis,
        ref float exitSign
    )
    {
        if (MathF.Abs(direction) < 1e-12f)
            // Ray parallel to this slab: only intersects if the origin already lies within it.
            // A parallel axis never contributes an entry/exit face, so it's excluded from the
            // normal bookkeeping below.
            return origin >= min && origin <= max;

        var t1 = (min - origin) / direction;
        var t2 = (max - origin) / direction;
        var entryFaceSign = -1f;
        if (t1 > t2)
        {
            (t1, t2) = (t2, t1);
            entryFaceSign = 1f;
        }

        if (t1 > tMin)
        {
            tMin = t1;
            entryAxis = axisIndex;
            entrySign = entryFaceSign;
        }
        if (t2 < tMax)
        {
            tMax = t2;
            exitAxis = axisIndex;
            exitSign = -entryFaceSign;
        }

        return tMin <= tMax;
    }
}
