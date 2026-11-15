using System.Numerics;
using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Physics.Components;

namespace Yaeger.Physics.Systems;

/// <summary>
/// Detects collisions between entities using a spatial-hash broadphase to cull candidate
/// pairs, followed by exact AABB/circle narrowphase checks.
/// Supports Box-Box (AABB), Circle-Circle, and Box-Circle collision pairs.
/// </summary>
/// <remarks>
/// Box colliders live in a persistent <see cref="BoxBroadphase"/> that only re-hashes colliders
/// that were added, removed or moved, so static and tilemap-generated geometry costs nothing per
/// step. Candidate pairs are produced by querying the broadphase from each non-static collider,
/// which emits every pair exactly once (no de-duplication set), and are then sorted, so manifold
/// order is deterministic. Pairs of two <see cref="BodyType.Static"/> bodies are found once and
/// cached until the static geometry changes, rather than re-queried every step.
/// Circle colliders are few and short-lived by comparison and are hashed into a small per-step
/// grid of their own.
/// </remarks>
public class CollisionDetectionSystem
{
    private readonly World _world;
    private readonly BoxBroadphase _broadphase;
    private readonly SpatialHash _circleHash;
    private readonly List<CollisionManifold> _manifolds = [];
    private readonly List<(
        Entity Entity,
        Vector2 Center,
        CircleCollider2D Collider
    )> _circleEntities = [];

    // Candidate pairs packed as (indexA << 32) | indexB, where a box's index is its
    // BoxBroadphase.Entry.Order and circle k's index is boxCount + k. Sorted before use.
    private readonly List<long> _pairKeys = [];
    private readonly List<int> _hits = [];

    // Pairs of overlapping-AABB static bodies, as broadphase entry ids. Neither side can move, so
    // the list only changes when the static geometry does (BoxBroadphase.StaticRevision) — it is
    // recomputed then and merely replayed on every other step.
    private readonly List<(int A, int B)> _staticPairs = [];
    private int _staticPairsRevision = -1;
    private readonly List<int> _circleHits = [];
    private int[] _circleVisited = new int[16];
    private int _circleStamp;

    /// <summary>
    /// Creates a detection system with its own broadphase.
    /// </summary>
    /// <param name="world">The ECS world containing physics entities.</param>
    /// <param name="cellSize">Spatial-hash cell size in world units.</param>
    public CollisionDetectionSystem(World world, float cellSize = 1.0f)
        : this(world, CreateBroadphase(world, cellSize)) { }

    /// <summary>
    /// Creates a detection system over a broadphase shared with other systems. The broadphase
    /// is still re-synced inside <see cref="Detect"/>, since positions have changed since it was
    /// last built (a no-op for everything that didn't move).
    /// </summary>
    internal CollisionDetectionSystem(World world, BoxBroadphase broadphase)
    {
        _world = world;
        _broadphase = broadphase;
        _circleHash = new SpatialHash(broadphase.CellSize);
    }

    private static BoxBroadphase CreateBroadphase(World world, float cellSize) =>
        cellSize > 0 && float.IsFinite(cellSize)
            ? new BoxBroadphase(world, cellSize, throwOnOversize: true)
            : throw new ArgumentOutOfRangeException(
                nameof(cellSize),
                cellSize,
                "Cell size must be a positive finite value."
            );

    /// <summary>
    /// The collision manifolds detected in the last call to <see cref="Detect"/>.
    /// </summary>
    public IReadOnlyList<CollisionManifold> Manifolds => _manifolds;

    /// <summary>
    /// Runs broadphase spatial partitioning followed by narrowphase collision detection.
    /// </summary>
    public void Detect()
    {
        _manifolds.Clear();
        _circleEntities.Clear();
        _pairKeys.Clear();
        _circleHash.Clear();

        _broadphase.Sync();

        foreach (
            (Entity entity, CircleCollider2D collider, Transform2D transform) in _world.Query<
                CircleCollider2D,
                Transform2D
            >()
        )
        {
            var center = transform.Position + collider.Offset;
            _circleEntities.Add((entity, center, collider));
        }

        var ordered = _broadphase.Ordered;
        var boxCount = ordered.Count;

        // Box indices: 0..boxCount-1 (broadphase order). Circle indices: boxCount.. onwards.
        // Box-box: query from every body that can move; a pair of two movers is emitted from the
        // lower-ordered side only. Static-vs-static pairs are replayed from a cache below.
        for (var i = 0; i < boxCount; i++)
        {
            var self = _broadphase.Get(ordered[i]);
            if (self.IsStaticBody)
                continue;

            _hits.Clear();
            _broadphase.Query(self.Center - self.Half, self.Center + self.Half, _hits);
            foreach (var id in _hits)
            {
                var other = _broadphase.Get(id);
                if (other.Order == i)
                    continue;
                if (!other.IsStaticBody && other.Order < i)
                    continue;

                AddPair(Math.Min(i, other.Order), Math.Max(i, other.Order));
            }
        }

        RefreshStaticPairs(ordered);
        foreach (var (idA, idB) in _staticPairs)
        {
            var orderA = _broadphase.Get(idA).Order;
            var orderB = _broadphase.Get(idB).Order;
            AddPair(Math.Min(orderA, orderB), Math.Max(orderA, orderB));
        }

        // Circles: against the boxes through the shared broadphase, against each other through
        // a small hash of their own.
        for (var k = 0; k < _circleEntities.Count; k++)
        {
            var (_, center, collider) = _circleEntities[k];
            var r = new Vector2(collider.Radius);
            _circleHash.Insert(k, center - r, center + r);
        }

        if (_circleVisited.Length < _circleEntities.Count)
            _circleVisited = new int[Math.Max(_circleEntities.Count, _circleVisited.Length * 2)];

        for (var k = 0; k < _circleEntities.Count; k++)
        {
            var (_, center, collider) = _circleEntities[k];
            var r = new Vector2(collider.Radius);

            _hits.Clear();
            _broadphase.Query(center - r, center + r, _hits);
            foreach (var id in _hits)
                AddPair(_broadphase.Get(id).Order, boxCount + k);

            _circleHits.Clear();
            if (_circleHash.Query(center - r, center + r, _circleHits))
            {
                _circleStamp++;
                foreach (var m in _circleHits)
                {
                    if (m <= k || _circleVisited[m] == _circleStamp)
                        continue;
                    _circleVisited[m] = _circleStamp;
                    AddPair(boxCount + k, boxCount + m);
                }
            }
        }

        // Sort pairs for deterministic narrowphase and manifold order.
        _pairKeys.Sort();

        // Narrowphase: test only broadphase candidate pairs.
        foreach (var key in _pairKeys)
        {
            var ia = (int)(key >> 32);
            var ib = (int)(key & 0xFFFFFFFF);
            var aIsBox = ia < boxCount;
            var bIsBox = ib < boxCount;

            if (aIsBox && bIsBox)
            {
                var a = BoxTuple(ordered[ia]);
                var b = BoxTuple(ordered[ib]);

                if (
                    !ShouldCollide(
                        a.Collider.Layer,
                        a.Collider.CollidesWith,
                        b.Collider.Layer,
                        b.Collider.CollidesWith
                    )
                )
                    continue;

                if (TestBoxBox(a, b, out var manifold))
                    _manifolds.Add(manifold);
            }
            else if (!aIsBox && !bIsBox)
            {
                var a = _circleEntities[ia - boxCount];
                var b = _circleEntities[ib - boxCount];

                if (
                    !ShouldCollide(
                        a.Collider.Layer,
                        a.Collider.CollidesWith,
                        b.Collider.Layer,
                        b.Collider.CollidesWith
                    )
                )
                    continue;

                if (TestCircleCircle(a, b, out var manifold))
                    _manifolds.Add(manifold);
            }
            else
            {
                // Mixed pair: box indices are always less than circle indices, so ia is
                // always the box index and ib - boxCount is always the circle index.
                var box = BoxTuple(ordered[ia]);
                var circle = _circleEntities[ib - boxCount];

                if (box.Entity == circle.Entity)
                    continue;

                if (
                    !ShouldCollide(
                        box.Collider.Layer,
                        box.Collider.CollidesWith,
                        circle.Collider.Layer,
                        circle.Collider.CollidesWith
                    )
                )
                    continue;

                if (TestBoxCircle(box, circle, out var manifold))
                    _manifolds.Add(manifold);
            }
        }
    }

    private void RefreshStaticPairs(IReadOnlyList<int> ordered)
    {
        if (_staticPairsRevision == _broadphase.StaticRevision)
            return;

        _staticPairsRevision = _broadphase.StaticRevision;
        _staticPairs.Clear();

        foreach (var id in ordered)
        {
            var self = _broadphase.Get(id);
            if (!self.IsStaticBody)
                continue;

            _hits.Clear();
            _broadphase.Query(self.Center - self.Half, self.Center + self.Half, _hits);
            foreach (var otherId in _hits)
            {
                // Each unordered pair once, from its lower-id side.
                if (otherId > id && _broadphase.Get(otherId).IsStaticBody)
                    _staticPairs.Add((id, otherId));
            }
        }
    }

    private void AddPair(int a, int b) => _pairKeys.Add(((long)a << 32) | (uint)b);

    private (Entity Entity, Vector2 Center, BoxCollider2D Collider) BoxTuple(int id)
    {
        var entry = _broadphase.Get(id);
        return (entry.Entity, entry.Center, entry.Collider);
    }

    /// <summary>
    /// Cheap early-out layer/mask filter run before narrowphase: two colliders are only tested
    /// against each other when each side's <c>CollidesWith</c> mask includes the other side's
    /// <c>Layer</c> bit (a symmetric check).
    /// </summary>
    private static bool ShouldCollide(
        int layerA,
        uint collidesWithA,
        int layerB,
        uint collidesWithB
    ) => (collidesWithA & (1u << layerB)) != 0 && (collidesWithB & (1u << layerA)) != 0;

    /// <summary>
    /// Tests AABB overlap between two box colliders.
    /// </summary>
    internal static bool TestBoxBox(
        (Entity Entity, Vector2 Center, BoxCollider2D Collider) a,
        (Entity Entity, Vector2 Center, BoxCollider2D Collider) b,
        out CollisionManifold manifold
    )
    {
        manifold = default;

        var halfA = a.Collider.HalfSize;
        var halfB = b.Collider.HalfSize;
        var delta = b.Center - a.Center;

        var overlapX = halfA.X + halfB.X - MathF.Abs(delta.X);
        var overlapY = halfA.Y + halfB.Y - MathF.Abs(delta.Y);

        if (overlapX <= 0 || overlapY <= 0)
            return false;

        var isTrigger = a.Collider.IsTrigger || b.Collider.IsTrigger;

        // Use the axis of minimum penetration
        if (overlapX < overlapY)
        {
            var normalX = delta.X < 0 ? -1.0f : 1.0f;
            manifold = new CollisionManifold
            {
                EntityA = a.Entity,
                EntityB = b.Entity,
                Normal = new Vector2(normalX, 0),
                PenetrationDepth = overlapX,
                ContactPoint = new Vector2(
                    a.Center.X + halfA.X * normalX,
                    (a.Center.Y + b.Center.Y) / 2.0f
                ),
                IsTrigger = isTrigger,
            };
        }
        else
        {
            var normalY = delta.Y < 0 ? -1.0f : 1.0f;
            manifold = new CollisionManifold
            {
                EntityA = a.Entity,
                EntityB = b.Entity,
                Normal = new Vector2(0, normalY),
                PenetrationDepth = overlapY,
                ContactPoint = new Vector2(
                    (a.Center.X + b.Center.X) / 2.0f,
                    a.Center.Y + halfA.Y * normalY
                ),
                IsTrigger = isTrigger,
            };
        }

        return true;
    }

    /// <summary>
    /// Tests overlap between two circle colliders.
    /// </summary>
    internal static bool TestCircleCircle(
        (Entity Entity, Vector2 Center, CircleCollider2D Collider) a,
        (Entity Entity, Vector2 Center, CircleCollider2D Collider) b,
        out CollisionManifold manifold
    )
    {
        manifold = default;

        var delta = b.Center - a.Center;
        var distanceSq = delta.LengthSquared();
        var radiusSum = a.Collider.Radius + b.Collider.Radius;

        if (distanceSq >= radiusSum * radiusSum)
            return false;

        var distance = MathF.Sqrt(distanceSq);
        var normal = distance > 0 ? delta / distance : Vector2.UnitX;

        manifold = new CollisionManifold
        {
            EntityA = a.Entity,
            EntityB = b.Entity,
            Normal = normal,
            PenetrationDepth = radiusSum - distance,
            ContactPoint = a.Center + normal * a.Collider.Radius,
            IsTrigger = a.Collider.IsTrigger || b.Collider.IsTrigger,
        };

        return true;
    }

    /// <summary>
    /// Tests overlap between a box collider and a circle collider.
    /// </summary>
    internal static bool TestBoxCircle(
        (Entity Entity, Vector2 Center, BoxCollider2D Collider) box,
        (Entity Entity, Vector2 Center, CircleCollider2D Collider) circle,
        out CollisionManifold manifold
    )
    {
        manifold = default;

        var halfSize = box.Collider.HalfSize;

        // Find the closest point on the AABB to the circle center
        var closest = new Vector2(
            Math.Clamp(circle.Center.X, box.Center.X - halfSize.X, box.Center.X + halfSize.X),
            Math.Clamp(circle.Center.Y, box.Center.Y - halfSize.Y, box.Center.Y + halfSize.Y)
        );

        var delta = circle.Center - closest;
        var distanceSq = delta.LengthSquared();

        if (distanceSq >= circle.Collider.Radius * circle.Collider.Radius)
            return false;

        var distance = MathF.Sqrt(distanceSq);

        // Handle the case where the circle center is inside the box
        Vector2 normal;
        float penetration;
        Vector2 contactPoint;

        if (distance > 0)
        {
            normal = delta / distance;
            penetration = circle.Collider.Radius - distance;
            contactPoint = closest;
        }
        else
        {
            // Circle center is inside the box — find the shortest axis to push out
            var dx = halfSize.X - MathF.Abs(circle.Center.X - box.Center.X);
            var dy = halfSize.Y - MathF.Abs(circle.Center.Y - box.Center.Y);

            if (dx < dy)
            {
                normal = new Vector2(circle.Center.X < box.Center.X ? -1.0f : 1.0f, 0);
                penetration = dx + circle.Collider.Radius;
                // Contact point on the box face along the normal
                contactPoint = new Vector2(box.Center.X + halfSize.X * normal.X, circle.Center.Y);
            }
            else
            {
                normal = new Vector2(0, circle.Center.Y < box.Center.Y ? -1.0f : 1.0f);
                penetration = dy + circle.Collider.Radius;
                // Contact point on the box face along the normal
                contactPoint = new Vector2(circle.Center.X, box.Center.Y + halfSize.Y * normal.Y);
            }
        }

        // Normal points from box (A) towards circle (B)
        manifold = new CollisionManifold
        {
            EntityA = box.Entity,
            EntityB = circle.Entity,
            Normal = normal,
            PenetrationDepth = penetration,
            ContactPoint = contactPoint,
            IsTrigger = box.Collider.IsTrigger || circle.Collider.IsTrigger,
        };

        return true;
    }
}
