using Yaeger.ECS;
using Yaeger.Graphics;

namespace Yaeger.Physics;

/// <summary>
/// 3D ray queries against every entity carrying <see cref="Transform3D"/> + <see cref="Aabb3D"/>.
/// This is deliberately not 3D physics: no rigid bodies, no resolution, just "what is along this
/// line" against the bounds the engine already tracks for culling/shadows. Each call does a
/// linear scan over the <see cref="Aabb3D"/> store (via <see cref="WorldExtensions.Query{T1,T2}"/>)
/// — fine for typical scene sizes; a broadphase (spatial hash, BVH) is the follow-up if profiling
/// says otherwise.
/// </summary>
public static class WorldRaycastExtensions
{
    /// <summary>Bitmask meaning "hit every layer" — the default for a raycast's <c>mask</c>.</summary>
    public const uint AllLayers = uint.MaxValue;

    /// <summary>
    /// Casts <paramref name="ray"/> against every <see cref="Aabb3D"/>-bearing entity and returns
    /// the nearest hit within <paramref name="maxDistance"/> (default: unbounded), or <c>null</c>
    /// if nothing is hit. <paramref name="mask"/> restricts the test to entities whose
    /// <see cref="Aabb3D.Layer"/> bit is set in the mask — same bit-index convention as
    /// <c>BoxCollider2D.Layer</c>/<c>CollidesWith</c>; the default matches every layer.
    /// </summary>
    public static RaycastHit? Raycast(
        this World world,
        Ray3D ray,
        float maxDistance = float.PositiveInfinity,
        uint mask = AllLayers
    )
    {
        RaycastHit? best = null;

        foreach (var (entity, transform, aabb) in world.Query<Transform3D, Aabb3D>())
        {
            if ((mask & (1u << aabb.Layer)) == 0)
                continue;

            if (
                !RayAabbIntersection.TryIntersect(
                    ray.Origin,
                    ray.Direction,
                    aabb,
                    transform.ModelMatrix,
                    out var distance,
                    out var normal
                )
                || distance > maxDistance
            )
                continue;

            if (best is not null && distance >= best.Value.Distance)
                continue;

            best = new RaycastHit(entity, distance, ray.Origin + ray.Direction * distance, normal);
        }

        return best;
    }

    /// <summary>
    /// Casts <paramref name="ray"/> against every <see cref="Aabb3D"/>-bearing entity and returns
    /// every hit within <paramref name="maxDistance"/>, sorted nearest-first. Empty when nothing
    /// is hit. See <see cref="Raycast"/> for <paramref name="mask"/>.
    /// </summary>
    public static IReadOnlyList<RaycastHit> RaycastAll(
        this World world,
        Ray3D ray,
        float maxDistance = float.PositiveInfinity,
        uint mask = AllLayers
    )
    {
        var hits = new List<RaycastHit>();

        foreach (var (entity, transform, aabb) in world.Query<Transform3D, Aabb3D>())
        {
            if ((mask & (1u << aabb.Layer)) == 0)
                continue;

            if (
                !RayAabbIntersection.TryIntersect(
                    ray.Origin,
                    ray.Direction,
                    aabb,
                    transform.ModelMatrix,
                    out var distance,
                    out var normal
                )
                || distance > maxDistance
            )
                continue;

            hits.Add(
                new RaycastHit(entity, distance, ray.Origin + ray.Direction * distance, normal)
            );
        }

        hits.Sort((a, b) => a.Distance.CompareTo(b.Distance));
        return hits;
    }
}
