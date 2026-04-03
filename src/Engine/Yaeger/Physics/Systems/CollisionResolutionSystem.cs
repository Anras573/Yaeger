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
    public void Resolve(IReadOnlyList<CollisionManifold> manifolds)
    {
        foreach (var manifold in manifolds)
        {
            ResolveManifold(manifold);
        }
    }

    private void ResolveManifold(CollisionManifold manifold)
    {
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
