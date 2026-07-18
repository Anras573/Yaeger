using System.Numerics;
using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Physics.Components;

namespace Yaeger.Physics.Systems;

/// <summary>
/// Resolves collisions using impulse-based resolution with positional correction.
/// </summary>
public class CollisionResolutionSystem(World world)
{
    /// <summary>
    /// Percentage of penetration to correct per frame (0.2 - 0.8 typical). Prevents jitter.
    /// </summary>
    public float CorrectionPercent { get; set; } = 0.4f;

    /// <summary>
    /// Minimum penetration threshold before positional correction is applied.
    /// Prevents micro-corrections that cause jitter.
    /// </summary>
    public float CorrectionSlop { get; set; } = 0.005f;

    /// <summary>
    /// Resolves a list of collision manifolds by applying impulses and positional corrections.
    /// </summary>
    /// <param name="manifolds">The manifolds to resolve, typically from the last detection step.</param>
    /// <param name="droppingThroughEntities">
    /// Entities currently "dropping through" one-way platforms (see
    /// <c>PhysicsWorld2D.DropThrough</c>): any manifold where one side is a one-way
    /// <see cref="BoxCollider2D"/> and the other is in this set is skipped unconditionally,
    /// regardless of approach direction. Pass <c>null</c> (the default) when no entities are
    /// currently dropping through.
    /// </param>
    public void Resolve(
        IReadOnlyList<CollisionManifold> manifolds,
        IReadOnlySet<Entity>? droppingThroughEntities = null
    )
    {
        foreach (var manifold in manifolds)
        {
            ResolveManifold(manifold, droppingThroughEntities);
        }
    }

    private void ResolveManifold(
        CollisionManifold manifold,
        IReadOnlySet<Entity>? droppingThroughEntities
    )
    {
        // Triggers/sensors are reported (manifold + OnCollision) but never physically resolved.
        if (manifold.IsTrigger)
            return;

        // One-way platforms are reported (manifold + OnCollision) but only resolved when the
        // other body approaches from the platform's solid side while not moving against it.
        if (ShouldSkipOneWayPlatform(manifold, droppingThroughEntities))
            return;

        // Ensure Normal is a unit vector; skip degenerate manifolds
        var normalLenSq = manifold.Normal.LengthSquared();
        if (normalLenSq < 1e-10f)
            return;
        if (MathF.Abs(normalLenSq - 1.0f) > 1e-4f)
            manifold.Normal = manifold.Normal / MathF.Sqrt(normalLenSq);

        var hasBodyA = world.TryGetComponent<RigidBody2D>(manifold.EntityA, out var bodyA);
        var hasBodyB = world.TryGetComponent<RigidBody2D>(manifold.EntityB, out var bodyB);

        // Default to static if no rigid body component
        if (!hasBodyA)
            bodyA = RigidBody2D.CreateStatic();
        if (!hasBodyB)
            bodyB = RigidBody2D.CreateStatic();

        // Skip if both are static or kinematic (nothing to resolve)
        if (bodyA.Type != BodyType.Dynamic && bodyB.Type != BodyType.Dynamic)
            return;

        var inverseMassA = bodyA.Type == BodyType.Dynamic ? bodyA.InverseMass : 0.0f;
        var inverseMassB = bodyB.Type == BodyType.Dynamic ? bodyB.InverseMass : 0.0f;
        var inverseMassSum = inverseMassA + inverseMassB;

        if (inverseMassSum <= 0)
            return;

        // Get velocities
        world.TryGetComponent<Velocity2D>(manifold.EntityA, out var velocityA);
        world.TryGetComponent<Velocity2D>(manifold.EntityB, out var velocityB);

        // Get physics materials for restitution and friction (fall back to default if missing)
        if (!world.TryGetComponent<PhysicsMaterial>(manifold.EntityA, out var materialA))
            materialA = PhysicsMaterial.Default;
        if (!world.TryGetComponent<PhysicsMaterial>(manifold.EntityB, out var materialB))
            materialB = PhysicsMaterial.Default;

        // Use minimum restitution (more conservative bounce)
        var restitution = MathF.Min(materialA.Restitution, materialB.Restitution);

        // --- Impulse resolution ---
        var relativeVelocity = velocityB.Linear - velocityA.Linear;
        var velocityAlongNormal = Vector2.Dot(relativeVelocity, manifold.Normal);

        // Don't resolve if objects are separating
        if (velocityAlongNormal > 0)
        {
            // Still apply positional correction even if separating
            ApplyPositionalCorrection(
                manifold,
                bodyA,
                bodyB,
                inverseMassA,
                inverseMassB,
                inverseMassSum
            );
            return;
        }

        // Calculate impulse magnitude
        var impulseMagnitude = -(1.0f + restitution) * velocityAlongNormal / inverseMassSum;
        var impulse = impulseMagnitude * manifold.Normal;

        // Apply impulse to velocities
        if (bodyA.Type == BodyType.Dynamic)
        {
            velocityA.Linear -= inverseMassA * impulse;
            world.AddComponent(manifold.EntityA, velocityA);
        }

        if (bodyB.Type == BodyType.Dynamic)
        {
            velocityB.Linear += inverseMassB * impulse;
            world.AddComponent(manifold.EntityB, velocityB);
        }

        // --- Friction impulse ---
        ApplyFriction(
            manifold,
            bodyA,
            bodyB,
            velocityA,
            velocityB,
            materialA,
            materialB,
            inverseMassA,
            inverseMassB,
            inverseMassSum,
            impulseMagnitude
        );

        // --- Positional correction ---
        ApplyPositionalCorrection(
            manifold,
            bodyA,
            bodyB,
            inverseMassA,
            inverseMassB,
            inverseMassSum
        );
    }

    /// <summary>
    /// Determines whether a manifold involving a one-way <see cref="BoxCollider2D"/> should be
    /// skipped: true when either side is a one-way platform and the contact should currently
    /// pass through it.
    /// </summary>
    private bool ShouldSkipOneWayPlatform(
        CollisionManifold manifold,
        IReadOnlySet<Entity>? droppingThroughEntities
    )
    {
        var aIsOneWay =
            world.TryGetComponent<BoxCollider2D>(manifold.EntityA, out var boxA) && boxA.OneWay;
        var bIsOneWay =
            world.TryGetComponent<BoxCollider2D>(manifold.EntityB, out var boxB) && boxB.OneWay;

        if (
            aIsOneWay
            && ShouldPassThroughOneWayPlatform(
                manifold.Normal,
                boxA.SurfaceDirection,
                platformIsA: true,
                platform: manifold.EntityA,
                other: manifold.EntityB,
                droppingThroughEntities
            )
        )
            return true;

        if (
            bIsOneWay
            && ShouldPassThroughOneWayPlatform(
                manifold.Normal,
                boxB.SurfaceDirection,
                platformIsA: false,
                platform: manifold.EntityB,
                other: manifold.EntityA,
                droppingThroughEntities
            )
        )
            return true;

        return false;
    }

    /// <summary>
    /// A contact with a one-way platform is only resolved when the other body is on the
    /// platform's solid side (the resolution push points the same way as
    /// <paramref name="surfaceDirection"/>) and its velocity relative to the platform is not
    /// moving against that direction (i.e. not still rising up through it). Either condition
    /// failing — or an active drop-through — means the contact passes through.
    /// </summary>
    private bool ShouldPassThroughOneWayPlatform(
        Vector2 normal,
        Vector2 surfaceDirection,
        bool platformIsA,
        Entity platform,
        Entity other,
        IReadOnlySet<Entity>? droppingThroughEntities
    )
    {
        if (droppingThroughEntities is not null && droppingThroughEntities.Contains(other))
            return true;

        // Direction the *other* body would be pushed by positional correction along this
        // manifold's normal (see ApplyPositionalCorrection: A moves along -Normal, B along +Normal).
        var pushOnOther = platformIsA ? normal : -normal;
        if (Vector2.Dot(pushOnOther, surfaceDirection) <= 0f)
            return true;

        world.TryGetComponent<Velocity2D>(other, out var otherVelocity);
        world.TryGetComponent<Velocity2D>(platform, out var platformVelocity);
        var relativeVelocity = otherVelocity.Linear - platformVelocity.Linear;

        return Vector2.Dot(relativeVelocity, surfaceDirection) > 0f;
    }

    private void ApplyFriction(
        CollisionManifold manifold,
        RigidBody2D bodyA,
        RigidBody2D bodyB,
        Velocity2D velocityA,
        Velocity2D velocityB,
        PhysicsMaterial materialA,
        PhysicsMaterial materialB,
        float inverseMassA,
        float inverseMassB,
        float inverseMassSum,
        float normalImpulseMagnitude
    )
    {
        // Re-read velocities (they may have been updated by the impulse step)
        if (bodyA.Type == BodyType.Dynamic)
            world.TryGetComponent<Velocity2D>(manifold.EntityA, out velocityA);
        if (bodyB.Type == BodyType.Dynamic)
            world.TryGetComponent<Velocity2D>(manifold.EntityB, out velocityB);

        var relativeVelocity = velocityB.Linear - velocityA.Linear;

        // Calculate tangent vector (perpendicular to normal, in the direction of relative velocity)
        var tangent =
            relativeVelocity - Vector2.Dot(relativeVelocity, manifold.Normal) * manifold.Normal;
        if (tangent.LengthSquared() < 1e-10f)
            return;

        tangent = Vector2.Normalize(tangent);

        // Calculate friction impulse magnitude
        var frictionMagnitude = -Vector2.Dot(relativeVelocity, tangent) / inverseMassSum;

        // Use average friction
        var staticFriction = (materialA.Friction + materialB.Friction) / 2.0f;

        // Coulomb's law: clamp friction impulse to static friction cone
        Vector2 frictionImpulse;
        if (MathF.Abs(frictionMagnitude) < normalImpulseMagnitude * staticFriction)
        {
            // Static friction
            frictionImpulse = frictionMagnitude * tangent;
        }
        else
        {
            // Dynamic friction (use a slightly lower coefficient)
            var dynamicFriction = staticFriction * 0.7f;
            frictionImpulse = -normalImpulseMagnitude * dynamicFriction * tangent;
        }

        // Apply friction impulse
        if (bodyA.Type == BodyType.Dynamic)
        {
            velocityA.Linear -= inverseMassA * frictionImpulse;
            world.AddComponent(manifold.EntityA, velocityA);
        }

        if (bodyB.Type == BodyType.Dynamic)
        {
            velocityB.Linear += inverseMassB * frictionImpulse;
            world.AddComponent(manifold.EntityB, velocityB);
        }
    }

    private void ApplyPositionalCorrection(
        CollisionManifold manifold,
        RigidBody2D bodyA,
        RigidBody2D bodyB,
        float inverseMassA,
        float inverseMassB,
        float inverseMassSum
    )
    {
        var correctionMagnitude =
            MathF.Max(manifold.PenetrationDepth - CorrectionSlop, 0.0f)
            / inverseMassSum
            * CorrectionPercent;

        var correction = correctionMagnitude * manifold.Normal;

        if (
            bodyA.Type == BodyType.Dynamic
            && world.TryGetComponent<Transform2D>(manifold.EntityA, out var transformA)
        )
        {
            transformA.Position -= inverseMassA * correction;
            world.AddComponent(manifold.EntityA, transformA);
        }

        if (
            bodyB.Type == BodyType.Dynamic
            && world.TryGetComponent<Transform2D>(manifold.EntityB, out var transformB)
        )
        {
            transformB.Position += inverseMassB * correction;
            world.AddComponent(manifold.EntityB, transformB);
        }
    }
}
