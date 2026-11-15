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
/// Candidate obstacles come from a region query against a persistent <see cref="BoxBroadphase"/>
/// (one per system, synced at the start of each <see cref="Update"/> — only colliders that were
/// added, removed or moved are re-hashed): each axis move queries just the controller's swept box,
/// so the cost scales with the colliders near the controller rather than every collider in the
/// world. Candidates are visited in the same order a full world query would, so results match a
/// brute-force scan.
/// </para>
/// <para>
/// <b>Rider carrying</b>: before anything else each step, a grounded controller inherits its
/// ground entity's displacement since the last step (<see cref="CharacterController2D.GroundEntity"/>,
/// tracked via a per-entity position snapshot taken at the end of every <see cref="Update"/>
/// call). This is what lets a controller stand still on a moving platform (an elevator, a
/// horizontal ferry) without sliding off — call this system <i>after</i> whatever moves the
/// platform (typically <c>PhysicsWorld2D.Update</c>, for a kinematic <see cref="BoxCollider2D"/>
/// platform) in the same frame, so the platform has already moved before its riders are carried.
/// The carry is a no-op for a stationary ground (zero displacement), so it's always safe to
/// apply regardless of what the controller happens to be standing on. Because the carry runs
/// before this step's own move-and-slide resolution, riding into a wall depenetrates normally
/// (the platform can't push the rider through it) and a vertically moving platform doesn't cause
/// grounded-state flicker (the rider is carried down with it instead of momentarily being left
/// behind and re-falling into contact every step).
/// </para>
/// <para>
/// <b>Gravity-relative ground</b>: <c>IsGrounded</c>/<c>GroundNormal</c>/<c>GroundEntity</c> are
/// set from any contact whose push normal opposes the controller's effective gravity
/// (<c>Gravity * GravityScale</c>), so sideways or upside-down gravity grounds against walls or
/// ceilings. <c>IsTouchingWallLeft/Right</c> and <c>IsTouchingCeiling</c> remain world-space
/// contact flags. With zero gravity, world-down is assumed.
/// </para>
/// </remarks>
public class CharacterControllerSystem : IUpdateSystem
{
    private readonly World world;
    private readonly BoxBroadphase _broadphase;
    private readonly List<(Entity, CharacterController2D, Transform2D)> _controllers = [];
    private readonly List<int> _candidates = [];
    private readonly List<(Entity Controller, Entity Trigger)> _exitedTriggers = [];

    /// <summary>
    /// A solid contact resolved by move-and-slide: the <paramref name="Controller"/> was pushed
    /// out of <paramref name="Other"/> along <paramref name="Normal"/> (the push direction applied
    /// to the controller, pointing away from the obstacle).
    /// </summary>
    public readonly record struct Contact(Entity Controller, Entity Other, Vector2 Normal);

    private readonly List<Contact> _contacts = new();
    private readonly HashSet<(Entity Controller, Entity Trigger)> _activeTriggers = new();
    private readonly HashSet<(Entity Controller, Entity Trigger)> _currentTriggers = new();

    /// <summary>
    /// Solid contacts resolved during the last <see cref="Update"/> call (entity + normal). Cleared
    /// at the start of every update.
    /// </summary>
    public IReadOnlyList<Contact> Contacts => _contacts;

    /// <summary>Raised (controller, trigger) the step a controller starts overlapping a trigger collider.</summary>
    public event Action<Entity, Entity>? OnTriggerEnter;

    /// <summary>Raised (controller, trigger) every step a controller overlaps a trigger collider, including the entering step.</summary>
    public event Action<Entity, Entity>? OnTriggerStay;

    /// <summary>
    /// Raised (controller, trigger) the step a controller stops overlapping a trigger collider,
    /// including when either entity was destroyed or lost its components.
    /// </summary>
    public event Action<Entity, Entity>? OnTriggerExit;

    /// <summary>Raised for every solid contact resolved during move-and-slide, after all controllers have moved.</summary>
    public event Action<Contact>? OnContact;

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
    /// Minimum <c>dot(contactNormal, -gravityDirection)</c> for a contact to count as ground.
    /// </summary>
    private const float GroundDotThreshold = 0.5f;

    /// <summary>
    /// The gravity vector applied to every <see cref="CharacterController2D"/> (scaled by each
    /// controller's own <see cref="CharacterController2D.GravityScale"/>). Default is (0, -9.81).
    /// </summary>
    public Vector2 Gravity { get; set; }

    /// <summary>
    /// Creates a system with the given gravity.
    /// </summary>
    /// <param name="world">The ECS world containing controllers and obstacles.</param>
    /// <param name="gravity">Gravity vector, scaled per controller by its own gravity scale.</param>
    /// <param name="broadphaseCellSize">
    /// Spatial-hash cell size in world units, as for <see cref="PhysicsWorld2D"/> — roughly twice
    /// the typical collider extent. Defaults to 1.0.
    /// </param>
    public CharacterControllerSystem(World world, Vector2 gravity, float broadphaseCellSize = 1.0f)
    {
        this.world = world;
        Gravity = gravity;
        _broadphase = new BoxBroadphase(world, broadphaseCellSize, throwOnOversize: false);
    }

    /// <summary>
    /// Creates a system using the default Earth-like gravity, (0, -9.81).
    /// </summary>
    public CharacterControllerSystem(World world)
        : this(world, new Vector2(0, -9.81f)) { }

    public void Update(float deltaTime)
    {
        _broadphase.Sync();

        // Snapshot before mutating components on these same entities, so we never depend on
        // subtle Dictionary-enumeration-during-mutation semantics.
        var controllers = _controllers;
        controllers.Clear();
        foreach (
            (Entity e, CharacterController2D c, Transform2D t) in world.Query<
                CharacterController2D,
                Transform2D
            >()
        )
            controllers.Add((e, c, t));
        _contacts.Clear();

        foreach (var (entity, controllerSnapshot, transformSnapshot) in controllers)
        {
            var controller = controllerSnapshot;
            var transform = transformSnapshot;
            world.TryGetComponent<Velocity2D>(entity, out var velocity);

            var position = transform.Position;
            ApplyGroundCarry(ref controller, ref position);

            velocity.Linear += Gravity * controller.GravityScale * deltaTime;

            controller.IsGrounded = false;
            controller.IsTouchingWallLeft = false;
            controller.IsTouchingWallRight = false;
            controller.IsTouchingCeiling = false;
            controller.GroundNormal = Vector2.Zero;
            controller.GroundEntity = null;

            // X-axis first, then Y — see the type-level remarks for why this ordering avoids
            // tile-seam snags.
            MoveHorizontal(entity, ref controller, ref position, ref velocity, deltaTime);
            MoveVertical(entity, ref controller, ref position, ref velocity, deltaTime);

            transform.Position = position;
            world.AddComponent(entity, transform);
            world.AddComponent(entity, velocity);
            world.AddComponent(entity, controller);
        }

        UpdateTriggers(controllers);

        foreach (var contact in _contacts)
            OnContact?.Invoke(contact);
    }

    /// <summary>
    /// Tests each controller's box against trigger <see cref="BoxCollider2D"/>s (respecting
    /// layer masks) and raises enter/stay/exit events by diffing against the previous step.
    /// </summary>
    private void UpdateTriggers(List<(Entity, CharacterController2D, Transform2D)> controllers)
    {
        _currentTriggers.Clear();

        foreach (var (entity, _, _) in controllers)
        {
            // Re-read: the controller/transform were rewritten during this step.
            if (
                !world.TryGetComponent<CharacterController2D>(entity, out var controller)
                || !world.TryGetComponent<Transform2D>(entity, out var transform)
            )
                continue;

            var center = transform.Position + controller.Offset;
            var half = controller.HalfSize;

            _candidates.Clear();
            _broadphase.Query(center - half, center + half, _candidates);
            _broadphase.SortByOrder(_candidates);

            foreach (var id in _candidates)
            {
                var trigger = _broadphase.Get(id);
                var collider = trigger.Collider;
                if (!collider.IsTrigger || trigger.Entity == entity)
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

                var delta = Vector2.Abs(center - trigger.Center);
                var limit = half + trigger.Half;
                if (delta.X < limit.X && delta.Y < limit.Y)
                    _currentTriggers.Add((entity, trigger.Entity));
            }
        }

        foreach (var pair in _currentTriggers)
            if (!_activeTriggers.Contains(pair))
                OnTriggerEnter?.Invoke(pair.Controller, pair.Trigger);

        foreach (var pair in _currentTriggers)
            OnTriggerStay?.Invoke(pair.Controller, pair.Trigger);

        var exited = _exitedTriggers;
        exited.Clear();
        foreach (var pair in _activeTriggers)
        {
            if (!_currentTriggers.Contains(pair))
                exited.Add(pair);
        }

        _activeTriggers.Clear();
        foreach (var pair in _currentTriggers)
            _activeTriggers.Add(pair);

        foreach (var pair in exited)
            OnTriggerExit?.Invoke(pair.Controller, pair.Trigger);
    }

    /// <summary>
    /// Shifts <paramref name="position"/> by however far the controller's ground entity (if any)
    /// has moved since the last step — see the type-level remarks on rider carrying.
    /// </summary>
    private void ApplyGroundCarry(ref CharacterController2D controller, ref Vector2 position)
    {
        if (controller.GroundEntity is not { } groundEntity)
            return;

        if (!world.TryGetComponent<Transform2D>(groundEntity, out var groundTransform))
        {
            // The ground entity was destroyed since last step — nothing to carry from.
            controller.GroundEntity = null;
            return;
        }

        // The broadphase kept the ground's position from the previous Update's sync — that
        // call's start-of-step position, which is also its end-of-step one, since nothing here
        // moves colliders.
        var entry = _broadphase.Find(groundEntity);
        if (entry is { HasPrevious: true })
            position += groundTransform.Position - entry.PreviousPosition;
    }

    private void MoveHorizontal(
        Entity entity,
        ref CharacterController2D controller,
        ref Vector2 position,
        ref Velocity2D velocity,
        float deltaTime
    )
    {
        var previous = position;
        position.X += velocity.Linear.X * deltaTime;

        var halfSize = controller.HalfSize;

        foreach (
            var (candidateEntity, collider, colliderCenter) in SolidCandidates(
                entity,
                controller,
                position,
                previous
            )
        )
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

            _contacts.Add(new Contact(entity, candidateEntity, new Vector2(pushX, 0f)));
            TryRegisterGround(ref controller, new Vector2(pushX, 0f), candidateEntity);
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
        var previous = position;
        position.Y += velocity.Linear.Y * deltaTime;

        var halfSize = controller.HalfSize;

        foreach (
            var (candidateEntity, collider, colliderCenter) in SolidCandidates(
                entity,
                controller,
                position,
                previous
            )
        )
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

            if (pushY < 0f)
                controller.IsTouchingCeiling = true;

            _contacts.Add(new Contact(entity, candidateEntity, new Vector2(0f, pushY)));
            TryRegisterGround(ref controller, new Vector2(0f, pushY), candidateEntity);
        }
    }

    /// <summary>
    /// Marks the controller grounded if <paramref name="normal"/> (the push direction of a
    /// resolved contact) opposes the controller's effective gravity, whichever axis resolved it.
    /// </summary>
    private void TryRegisterGround(
        ref CharacterController2D controller,
        Vector2 normal,
        Entity contactEntity
    )
    {
        var effectiveGravity = Gravity * controller.GravityScale;
        // Zero gravity (e.g. isolating other behaviour) falls back to the world-down convention.
        var up =
            effectiveGravity.LengthSquared() <= 0f
                ? Vector2.UnitY
                : -Vector2.Normalize(effectiveGravity);
        if (Vector2.Dot(normal, up) <= GroundDotThreshold)
            return;

        controller.IsGrounded = true;
        controller.GroundNormal = normal;
        controller.GroundEntity = contactEntity;
    }

    /// <summary>
    /// Fills <see cref="_candidates"/> with the solid obstacles near this axis move and returns
    /// them, in world-query order, as (entity, collider, centre) tuples. The region is the
    /// controller's box swept from <paramref name="from"/> to <paramref name="to"/>, grown by the
    /// controller's own size (so a depenetration push or step-up that shifts it mid-resolution
    /// still sees its neighbours) plus the step height and contact skin.
    /// </summary>
    private CandidateEnumerator SolidCandidates(
        Entity self,
        CharacterController2D controller,
        Vector2 to,
        Vector2 from
    )
    {
        var half = controller.HalfSize;
        var margin = half + new Vector2(ContactSkin, controller.StepHeight + ContactSkin);
        var min = Vector2.Min(from, to) + controller.Offset - half - margin;
        var max = Vector2.Max(from, to) + controller.Offset + half + margin;

        _candidates.Clear();
        _broadphase.Query(min, max, _candidates);
        _broadphase.SortByOrder(_candidates);

        return new CandidateEnumerator(this, self, controller);
    }

    private readonly struct CandidateEnumerator(
        CharacterControllerSystem system,
        Entity self,
        CharacterController2D controller
    )
    {
        public Enumerator GetEnumerator() => new(system, self, controller);
    }

    private struct Enumerator(
        CharacterControllerSystem system,
        Entity self,
        CharacterController2D controller
    )
    {
        private int _index = -1;

        public (Entity Entity, BoxCollider2D Collider, Vector2 Center) Current { get; private set; }

        public bool MoveNext()
        {
            while (++_index < system._candidates.Count)
            {
                var entry = system._broadphase.Get(system._candidates[_index]);
                if (entry.Entity == self || entry.Collider.IsTrigger)
                    continue;
                if (
                    !ShouldCollide(
                        controller.Layer,
                        controller.CollidesWith,
                        entry.Collider.Layer,
                        entry.Collider.CollidesWith
                    )
                )
                    continue;

                Current = (entry.Entity, entry.Collider, entry.Center);
                return true;
            }

            return false;
        }
    }

    private static bool ShouldCollide(
        int layerA,
        uint collidesWithA,
        int layerB,
        uint collidesWithB
    ) => (collidesWithA & (1u << layerB)) != 0 && (collidesWithB & (1u << layerA)) != 0;
}
