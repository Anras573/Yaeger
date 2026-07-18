using System.Numerics;

namespace Yaeger.Physics.Systems;

/// <summary>
/// Ray-vs-expanded-box sweep test used to stop a fast-moving box from tunneling clean through a
/// thin stationary box within a single step. Uses the standard Minkowski-sum technique: the
/// stationary box is grown by the moving box's half-size, reducing the problem to a ray (the
/// moving box's center, travelling along its planned displacement) against a single box.
/// </summary>
public static class SweptAabb
{
    /// <summary>
    /// Finds the fraction of <paramref name="displacement"/> (in the open interval (0, 1)) at
    /// which a box of <paramref name="halfSize"/> starting at <paramref name="start"/> first
    /// touches a stationary box centered at <paramref name="obstacleCenter"/>.
    /// </summary>
    /// <returns>
    /// <c>false</c> when no new contact occurs strictly within this displacement — including
    /// when the boxes are already overlapping at <paramref name="start"/> (an existing overlap
    /// is discrete detection's job, not this sweep's) or when the path never reaches the
    /// obstacle at all.
    /// </returns>
    public static bool TryGetEntryFraction(
        Vector2 start,
        Vector2 halfSize,
        Vector2 displacement,
        Vector2 obstacleCenter,
        Vector2 obstacleHalfSize,
        out float tEntry
    )
    {
        tEntry = 0f;

        var expandedHalfSize = obstacleHalfSize + halfSize;
        var boxMin = obstacleCenter - expandedHalfSize;
        var boxMax = obstacleCenter + expandedHalfSize;

        var entryX = float.NegativeInfinity;
        var exitX = float.PositiveInfinity;
        if (!SlabIntersect(start.X, displacement.X, boxMin.X, boxMax.X, ref entryX, ref exitX))
            return false;

        var entryY = float.NegativeInfinity;
        var exitY = float.PositiveInfinity;
        if (!SlabIntersect(start.Y, displacement.Y, boxMin.Y, boxMax.Y, ref entryY, ref exitY))
            return false;

        var entry = MathF.Max(entryX, entryY);
        var exit = MathF.Min(exitX, exitY);

        // entry <= 0 means the boxes already overlap (or the ray starts inside the expanded
        // box) at t = 0 — an existing overlap, not tunneling. entry >= 1 means the obstacle
        // isn't reached within this displacement at all.
        if (entry > exit || entry <= 0f || entry >= 1f)
            return false;

        tEntry = entry;
        return true;
    }

    /// <summary>
    /// Intersects a ray (<paramref name="start"/> + t * <paramref name="displacement"/>) against
    /// the [<paramref name="min"/>, <paramref name="max"/>] slab on one axis, narrowing
    /// <paramref name="entry"/>/<paramref name="exit"/>. Returns <c>false</c> only when the ray
    /// is parallel to this axis and starts outside the slab — a permanent miss regardless of the
    /// other axis.
    /// </summary>
    private static bool SlabIntersect(
        float start,
        float displacement,
        float min,
        float max,
        ref float entry,
        ref float exit
    )
    {
        if (MathF.Abs(displacement) < 1e-10f)
            return start >= min && start <= max;

        var invDisplacement = 1f / displacement;
        var t1 = (min - start) * invDisplacement;
        var t2 = (max - start) * invDisplacement;

        if (t1 > t2)
            (t1, t2) = (t2, t1);

        entry = MathF.Max(entry, t1);
        exit = MathF.Min(exit, t2);
        return true;
    }
}
