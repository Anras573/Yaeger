using System.Numerics;
using Yaeger.ECS;
using Yaeger.Graphics;
using Yaeger.Physics.Components;
using Yaeger.Systems;

namespace Yaeger.Physics.Systems;

/// <summary>
/// Moves <see cref="CharacterController2D"/> entities by axis-separated sweep-and-slide against
/// solid <see cref="BoxCollider2D"/> obstacles, instead of impulse-based resolution. Applies its
/// own gravity (independent of any <c>PhysicsWorld2D.Gravity</c>), integrates each entity's
/// <see cref="Velocity2D"/>, depenetrates fully along each axis, and zeroes velocity into
/// contacts rather than bouncing off them.
/// </summary>
/// <remarks>
/// <para>
/// Movement is resolved X-axis-first, then Y-axis, matching the standard AABB "move and slide"
/// technique: because a body resting on top of a platform does not vertically overlap it,
/// horizontal movement never touches the platforms underfoot, so running across a seam between
/// two adjacent (or merged, see <see cref="TilemapColliderSystem"/>) colliders never snags.
/// </para>
/// <para>
/// Only <see cref="BoxCollider2D"/> obstacles are tested (not <see cref="CircleCollider2D"/>) —
/// tilemap-generated colliders and hand-placed platforms are boxes; circular obstacles for
/// kinematic controllers are out of scope for this pass. One-way platforms
/// (<see cref="BoxCollider2D.OneWay"/>) are respected via the same direction/velocity filter as
/// <see cref="CollisionResolutionSystem"/> (see <see cref="OneWayPlatformFilter"/>), though the
/// controller has no equivalent of <c>PhysicsWorld2D.DropThrough</c>.
/// </para>
/// <para>
/// Candidate obstacles are queried brute-force each axis, each step (no broadphase) — adequate
/// for typical level sizes; revisit if profiling shows otherwise.
/// </para>
/// </remarks>
public class CharacterControllerSystem(World world, Vector2 gravity) : IUpdateSystem
{
    /// <summary>
    /// Contacts within this distance of exact touching (but not actually overlapping) still count
    /// as a contact for flag-setting purposes. Without this, a contact that resolves to exactly
    /// zero overlap (velocity zeroed, nothing regenerating overlap frame-to-frame — unlike gravity
    /// against the ground) would fail the "overlap > 0" test on the very next step and silently
    /// drop its <c>IsTouching*</c>/<c>IsGrounded</c> flag while still resting flush against the
    /// obstacle.
    /// </summary>
    private const float ContactSkin = 0.001f;

    /// <summary>
    /// The gravity vector applied to every <see cref="CharacterController2D"/> (scaled by each
    /// controller's own <see cref="CharacterController2D.GravityScale"/>). Default is (0, -9.81).
    /// </summary>
    public Vector2 Gravity { get; set; } = gravity;

    /// <summary>
    /// Creates a system using the default Earth-like gravity, (0, -9.81).
    /// </summary>
    public CharacterControllerSystem(World world)
        : this(world, new Vector2(0, -9.81f)) { }

    public void Update(float deltaTime)
    {
        // Snapshot before mutating components on these same entities, so we never depend on
        // subtle Dictionary-enumeration-during-mutation semantics.
        var controllers = world.Query<CharacterController2D, Transform2D>().ToList();

        foreach (var (entity, controllerSnapshot, transformSnapshot) in controllers)
        {
            var controller = controllerSnapshot;
            var transform = transformSnapshot;
            world.TryGetComponent<Velocity2D>(entity, out var velocity);

            velocity.Linear += Gravity * controller.GravityScale * deltaTime;

            controller.IsGrounded = false;
            controller.IsTouchingWallLeft = false;
            controller.IsTouchingWallRight = false;
            controller.IsTouchingCeiling = false;
            controller.GroundNormal = Vector2.Zero;

            var position = transform.Position;

            // X-axis first, then Y — see the type-level remarks for why this ordering avoids
            // tile-seam snags.
            MoveHorizontal(entity, ref controller, ref position, ref velocity, deltaTime);
            MoveVertical(entity, ref controller, ref position, ref velocity, deltaTime);

            transform.Position = position;
            world.AddComponent(entity, transform);
            world.AddComponent(entity, velocity);
            world.AddComponent(entity, controller);
        }
    }

    private void MoveHorizontal(
        Entity entity,
        ref CharacterController2D controller,
        ref Vector2 position,
        ref Velocity2D velocity,
        float deltaTime
    )
    {
        position.X += velocity.Linear.X * deltaTime;

        var halfSize = controller.HalfSize;

        foreach (var (_, collider, colliderCenter) in SolidCandidates(entity, controller))
        {
            var center = position + controller.Offset;
            var colliderHalf = collider.HalfSize;

            var overlapX = halfSize.X + colliderHalf.X - MathF.Abs(center.X - colliderCenter.X);
            var overlapY = halfSize.Y + colliderHalf.Y - MathF.Abs(center.Y - colliderCenter.Y);

            // The perpendicular (Y) axis must have genuine positive overlap — this is what
            // distinguishes "resting flush against this obstacle" from the ambiguous case of
            // merely grazing a corner where both axes are near zero (see the seam-crossing test).
            // The X axis itself is allowed within skin distance of touching so a wall contact
            // resolved to exactly zero overlap keeps reporting a contact on later frames, instead
            // of failing "overlap > 0" and silently losing its flag once velocity stops
            // regenerating the overlap.
            if (overlapX <= -ContactSkin || overlapY <= 0f)
                continue;

            var pushX = center.X < colliderCenter.X ? -1f : 1f;

            if (collider.OneWay)
            {
                var pushOnOther = new Vector2(pushX, 0f);
                if (
                    OneWayPlatformFilter.ShouldPassThrough(
                        pushOnOther,
                        collider.SurfaceDirection,
                        velocity.Linear
                    )
                )
                    continue;
            }

            // Step-up: a short enough ledge is climbed instead of stopping horizontal motion.
            // Checked unconditionally (ahead of the axis-priority gate below) since a climbable
            // ledge often has a smaller vertical overlap than horizontal, which would otherwise
            // defer it to the Y-phase and block it outright.
            if (controller.StepHeight > 0f)
            {
                var characterBottom = center.Y - halfSize.Y;
                var colliderTop = colliderCenter.Y + colliderHalf.Y;
                var requiredLift = colliderTop - characterBottom;

                if (requiredLift > 0f && requiredLift <= controller.StepHeight)
                {
                    position.Y += requiredLift;
                    continue;
                }
            }

            // Resolve here only when X is the axis of least penetration — otherwise this is
            // really a vertical conflict (e.g. spawning embedded in the floor below) and is left
            // for MoveVertical, which uses the complementary (strict) comparison so exactly one
            // phase resolves any given overlap.
            if (overlapX > overlapY)
                continue;

            position.X += pushX * MathF.Max(overlapX, 0f);
            velocity.Linear.X = 0f;

            if (pushX < 0f)
                controller.IsTouchingWallRight = true;
            else
                controller.IsTouchingWallLeft = true;
        }
    }

    private void MoveVertical(
        Entity entity,
        ref CharacterController2D controller,
        ref Vector2 position,
        ref Velocity2D velocity,
        float deltaTime
    )
    {
        position.Y += velocity.Linear.Y * deltaTime;

        var halfSize = controller.HalfSize;

        foreach (var (_, collider, colliderCenter) in SolidCandidates(entity, controller))
        {
            var center = position + controller.Offset;
            var colliderHalf = collider.HalfSize;

            var overlapX = halfSize.X + colliderHalf.X - MathF.Abs(center.X - colliderCenter.X);
            var overlapY = halfSize.Y + colliderHalf.Y - MathF.Abs(center.Y - colliderCenter.Y);

            // Mirrors MoveHorizontal's guard: the perpendicular (X) axis must have genuine
            // positive overlap, while Y itself is allowed within skin distance of touching so a
            // ground/ceiling contact resolved to exactly zero overlap keeps reporting a contact.
            if (overlapY <= -ContactSkin || overlapX <= 0f)
                continue;

            var pushY = center.Y < colliderCenter.Y ? -1f : 1f;

            if (collider.OneWay)
            {
                var pushOnOther = new Vector2(0f, pushY);
                if (
                    OneWayPlatformFilter.ShouldPassThrough(
                        pushOnOther,
                        collider.SurfaceDirection,
                        velocity.Linear
                    )
                )
                    continue;
            }

            // Complementary (strict) comparison to MoveHorizontal's, so exactly one phase
            // resolves any given overlap — see the comment there.
            if (overlapY >= overlapX)
                continue;

            position.Y += pushY * MathF.Max(overlapY, 0f);
            velocity.Linear.Y = 0f;

            if (pushY > 0f)
            {
                controller.IsGrounded = true;
                controller.GroundNormal = Vector2.UnitY;
            }
            else
            {
                controller.IsTouchingCeiling = true;
            }
        }
    }

    private IEnumerable<(Entity Entity, BoxCollider2D Collider, Vector2 Center)> SolidCandidates(
        Entity self,
        CharacterController2D controller
    )
    {
        foreach (
            (Entity candidateEntity, BoxCollider2D collider, Transform2D transform) in world.Query<
                BoxCollider2D,
                Transform2D
            >()
        )
        {
            if (candidateEntity == self)
                continue;
            if (collider.IsTrigger)
                continue;
            if (
                !ShouldCollide(
                    controller.Layer,
                    controller.CollidesWith,
                    collider.Layer,
                    collider.CollidesWith
                )
            )
                continue;

            yield return (candidateEntity, collider, transform.Position + collider.Offset);
        }
    }

    private static bool ShouldCollide(
        int layerA,
        uint collidesWithA,
        int layerB,
        uint collidesWithB
    ) => (collidesWithA & (1u << layerB)) != 0 && (collidesWithB & (1u << layerA)) != 0;
}
