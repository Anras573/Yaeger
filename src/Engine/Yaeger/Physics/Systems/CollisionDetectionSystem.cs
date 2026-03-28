using System.Numerics;
using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Physics.Components;

namespace Yaeger.Physics.Systems;

/// <summary>
/// Detects collisions between entities using narrowphase checks.
/// Supports Box-Box (AABB), Circle-Circle, and Box-Circle collision pairs.
/// Phase 1 uses brute-force broadphase (O(n^2)).
/// </summary>
public class CollisionDetectionSystem(World world)
{
    private readonly List<CollisionManifold> _manifolds = [];

    /// <summary>
    /// The collision manifolds detected in the last call to <see cref="Detect"/>.
    /// </summary>
    public IReadOnlyList<CollisionManifold> Manifolds => _manifolds;

    /// <summary>
    /// Runs collision detection for all collidable entities.
    /// </summary>
    public void Detect()
    {
        _manifolds.Clear();

        // Collect all collidable entities with their world positions
        var boxEntities = new List<(Entity Entity, Vector2 Center, BoxCollider2D Collider)>();
        var circleEntities = new List<(Entity Entity, Vector2 Center, CircleCollider2D Collider)>();

        foreach (
            (Entity entity, Transform2D transform, BoxCollider2D collider) in world.Query<
                Transform2D,
                BoxCollider2D
            >()
        )
        {
            var center = transform.Position + collider.Offset;
            boxEntities.Add((entity, center, collider));
        }

        foreach (
            (Entity entity, Transform2D transform, CircleCollider2D collider) in world.Query<
                Transform2D,
                CircleCollider2D
            >()
        )
        {
            var center = transform.Position + collider.Offset;
            circleEntities.Add((entity, center, collider));
        }

        // Box vs Box
        for (var i = 0; i < boxEntities.Count; i++)
        {
            for (var j = i + 1; j < boxEntities.Count; j++)
            {
                if (TestBoxBox(boxEntities[i], boxEntities[j], out var manifold))
                {
                    _manifolds.Add(manifold);
                }
            }
        }

        // Circle vs Circle
        for (var i = 0; i < circleEntities.Count; i++)
        {
            for (var j = i + 1; j < circleEntities.Count; j++)
            {
                if (TestCircleCircle(circleEntities[i], circleEntities[j], out var manifold))
                {
                    _manifolds.Add(manifold);
                }
            }
        }

        // Box vs Circle
        for (var i = 0; i < boxEntities.Count; i++)
        {
            for (var j = 0; j < circleEntities.Count; j++)
            {
                if (TestBoxCircle(boxEntities[i], circleEntities[j], out var manifold))
                {
                    _manifolds.Add(manifold);
                }
            }
        }
    }

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

        if (distance > 0)
        {
            normal = delta / distance;
            penetration = circle.Collider.Radius - distance;
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
            }
            else
            {
                normal = new Vector2(0, circle.Center.Y < box.Center.Y ? -1.0f : 1.0f);
                penetration = dy + circle.Collider.Radius;
            }
        }

        // Normal points from box (A) towards circle (B)
        manifold = new CollisionManifold
        {
            EntityA = box.Entity,
            EntityB = circle.Entity,
            Normal = normal,
            PenetrationDepth = penetration,
            ContactPoint = closest,
        };

        return true;
    }
}
