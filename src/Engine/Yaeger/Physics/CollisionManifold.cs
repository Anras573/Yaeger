using System.Numerics;
using Yaeger.ECS;

namespace Yaeger.Physics;

/// <summary>
/// Contains the contact information from a collision between two entities.
/// </summary>
public struct CollisionManifold
{
    /// <summary>
    /// The first entity involved in the collision.
    /// </summary>
    public Entity EntityA;

    /// <summary>
    /// The second entity involved in the collision.
    /// </summary>
    public Entity EntityB;

    /// <summary>
    /// The collision normal pointing from entity A towards entity B.
    /// Must be a unit vector (length 1). Collision resolution depends on this invariant.
    /// </summary>
    public Vector2 Normal;

    /// <summary>
    /// How far the two colliders are overlapping.
    /// </summary>
    public float PenetrationDepth;

    /// <summary>
    /// The point of contact between the two colliders.
    /// </summary>
    public Vector2 ContactPoint;
}
