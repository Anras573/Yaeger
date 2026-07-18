using System.Numerics;
using Yaeger.ECS;
using Yaeger.Physics.Systems;
using Yaeger.Systems;

namespace Yaeger.Physics;

/// <summary>
/// Facade that orchestrates all 2D physics systems in the correct order.
/// Provides a simple interface for stepping the physics simulation.
/// </summary>
public class PhysicsWorld2D : IUpdateSystem
{
    private readonly TilemapColliderSystem _tilemapColliderSystem;
    private readonly GravitySystem _gravitySystem;
    private readonly MovementSystem _movementSystem;
    private readonly CollisionDetectionSystem _collisionDetectionSystem;
    private readonly CollisionResolutionSystem _collisionResolutionSystem;

    /// <summary>
    /// The global gravity vector. Default is (0, -9.81).
    /// </summary>
    public Vector2 Gravity
    {
        get => _gravitySystem.Gravity;
        set => _gravitySystem.Gravity = value;
    }

    // The previous step's contact pairs, keyed order-independently on the two entity IDs, so
    // this step's manifolds can be diffed against it to derive enter/exit events. Replaced
    // wholesale (not mutated) at the end of each Step — see the comment there.
    private Dictionary<(Entity A, Entity B), CollisionManifold> _activePairs = new();

    // Entities currently dropping through one-way platforms, and when each expires. Checked
    // (and pruned) once per Step — see DropThrough.
    private readonly Dictionary<Entity, float> _dropThroughUntil = new();
    private float _elapsedTime;

    // Leftover time not yet consumed by a fixed sub-step — see Update.
    private float _accumulator;

    /// <summary>
    /// The fixed duration, in seconds, of each physics sub-step. Every <see cref="Update"/> call
    /// advances the simulation by whole multiples of this value, regardless of the caller's
    /// frame delta — this is what makes simulation results independent of frame rate. Set via
    /// the constructor; defaults to 1/120s (120 Hz).
    /// </summary>
    public float FixedTimeStep { get; }

    /// <summary>
    /// The maximum number of fixed sub-steps a single <see cref="Update"/> call will run. Caps
    /// how much accumulated time one call can consume — the "spiral of death" guard: after a
    /// long stall (a debugger pause, a huge hitch), the world catches up by at most this many
    /// steps instead of a burst proportional to however long the stall was, and any time beyond
    /// that is discarded rather than carried forward to compound on the next call.
    /// </summary>
    public int MaxSubSteps { get; }

    /// <summary>
    /// How far the accumulator is into the next, not-yet-run sub-step, as a fraction in
    /// [0, 1) of <see cref="FixedTimeStep"/>. Intended for render-side interpolation between the
    /// previous and current physics states; this class does not use it internally. Always 0
    /// after a zero-or-negative-<c>deltaTime</c> call to <see cref="Update"/> (see its remarks).
    /// </summary>
    public float InterpolationAlpha { get; private set; }

    /// <summary>
    /// Fired for each collision detected during a step — including the first step a pair
    /// overlaps (alongside <see cref="OnCollisionEnter"/>) and every step after
    /// (the "stay" signal). Fires every step a pair remains in contact, so gameplay reactions
    /// that should happen once per contact (stomping an enemy, picking up a coin) should use
    /// <see cref="OnCollisionEnter"/> instead.
    /// </summary>
    public event Action<CollisionManifold>? OnCollision;

    /// <summary>
    /// Fired once when a pair of entities begins overlapping (was not in contact last step,
    /// is in contact this step). Fires for trigger pairs too — this is the coin-pickup
    /// primitive: check <see cref="CollisionManifold.IsTrigger"/> to distinguish sensor
    /// overlaps from physically-resolved collisions.
    /// </summary>
    public event Action<CollisionManifold>? OnCollisionEnter;

    /// <summary>
    /// Fired once when a pair of entities stops overlapping (was in contact last step, is not
    /// in contact this step) — including when either entity is destroyed between steps, which
    /// ends its contacts without a final manifold to report.
    /// </summary>
    public event Action<Entity, Entity>? OnCollisionExit;

    /// <summary>
    /// The collision manifolds from the last physics step.
    /// </summary>
    public IReadOnlyList<CollisionManifold> Manifolds => _collisionDetectionSystem.Manifolds;

    /// <summary>
    /// Creates a new 2D physics world with the specified gravity and broadphase cell size.
    /// </summary>
    /// <param name="world">The ECS world containing physics entities.</param>
    /// <param name="gravity">Gravity vector. Defaults to (0, -9.81).</param>
    /// <param name="broadphaseCellSize">
    /// Size of each spatial-hash cell in world units. Should be roughly 2× the average
    /// collider extent for best pruning. Defaults to 1.0 (suitable for unit-scale worlds).
    /// Use a smaller value (e.g. 0.1) for NDC-scale worlds, or a larger value (e.g. 64)
    /// for pixel-scale worlds.
    /// </param>
    /// <param name="fixedTimeStep">
    /// Duration, in seconds, of each physics sub-step (see <see cref="FixedTimeStep"/>).
    /// Must be a positive finite value. Defaults to 1/120s (120 Hz).
    /// </param>
    /// <param name="maxSubSteps">
    /// Maximum sub-steps per <see cref="Update"/> call (see <see cref="MaxSubSteps"/>). Must be
    /// at least 1. Defaults to 8.
    /// </param>
    public PhysicsWorld2D(
        World world,
        Vector2? gravity = null,
        float broadphaseCellSize = 1.0f,
        float fixedTimeStep = 1f / 120f,
        int maxSubSteps = 8
    )
    {
        if (broadphaseCellSize <= 0 || !float.IsFinite(broadphaseCellSize))
            throw new ArgumentOutOfRangeException(
                nameof(broadphaseCellSize),
                broadphaseCellSize,
                "Cell size must be a positive finite value."
            );
        if (fixedTimeStep <= 0 || !float.IsFinite(fixedTimeStep))
            throw new ArgumentOutOfRangeException(
                nameof(fixedTimeStep),
                fixedTimeStep,
                "Fixed time step must be a positive finite value."
            );
        if (maxSubSteps < 1)
            throw new ArgumentOutOfRangeException(
                nameof(maxSubSteps),
                maxSubSteps,
                "Max sub-steps must be at least 1."
            );

        var g = gravity ?? new Vector2(0, -9.81f);
        _tilemapColliderSystem = new TilemapColliderSystem(world);
        _gravitySystem = new GravitySystem(world, g);
        _movementSystem = new MovementSystem(world);
        _collisionDetectionSystem = new CollisionDetectionSystem(world, broadphaseCellSize);
        _collisionResolutionSystem = new CollisionResolutionSystem(world);
        FixedTimeStep = fixedTimeStep;
        MaxSubSteps = maxSubSteps;
    }

    /// <summary>
    /// Makes <paramref name="entity"/> ignore one-way platform contacts for
    /// <paramref name="duration"/> seconds — the "drop-through" escape hatch for a down+jump
    /// input on a one-way platform. Has no effect on regular (non-one-way) colliders. Calling
    /// this again before the previous window expires replaces it rather than stacking.
    /// </summary>
    /// <param name="entity">The entity that should fall through any one-way platform it is on.</param>
    /// <param name="duration">How long the drop-through lasts, in seconds. Must be positive.</param>
    public void DropThrough(Entity entity, float duration = 0.25f)
    {
        if (duration <= 0 || !float.IsFinite(duration))
            throw new ArgumentOutOfRangeException(
                nameof(duration),
                duration,
                "Duration must be a positive finite value."
            );

        _dropThroughUntil[entity] = _elapsedTime + duration;
    }

    /// <summary>
    /// Advances the simulation by <paramref name="deltaTime"/> seconds using a fixed-timestep
    /// accumulator: <paramref name="deltaTime"/> is added to a running total, and
    /// <see cref="Step"/> then runs once per whole <see cref="FixedTimeStep"/> the accumulator
    /// contains (up to <see cref="MaxSubSteps"/> — see its remarks), leaving any remainder for
    /// next call. Callers keep calling this once per frame with their frame's own delta; the
    /// simulation itself always advances in fixed increments, so results no longer depend on
    /// frame rate. <see cref="InterpolationAlpha"/> is updated to reflect the leftover fraction.
    /// </summary>
    /// <remarks>
    /// A <paramref name="deltaTime"/> of zero or less runs exactly one <see cref="Step"/>
    /// immediately with that raw value, bypassing the accumulator entirely, instead of doing
    /// nothing — there's no future instant for a non-positive delta to accumulate towards, and a
    /// single deterministic step keeps this useful for tests and manual single-step invocations
    /// that want detection/resolution/events to run once without elapsing simulated time.
    /// <see cref="InterpolationAlpha"/> is reset to 0 in this case.
    /// </remarks>
    /// <param name="deltaTime">The time elapsed since the last call, in seconds.</param>
    public void Update(float deltaTime)
    {
        if (deltaTime <= 0f)
        {
            Step(deltaTime);
            InterpolationAlpha = 0f;
            return;
        }

        _accumulator += deltaTime;

        // Spiral-of-death clamp: cap how much accumulated time this single call will consume,
        // discarding the rest rather than carrying it forward to compound on the next call —
        // see MaxSubSteps.
        var maxAccumulated = MaxSubSteps * FixedTimeStep;
        if (_accumulator > maxAccumulated)
            _accumulator = maxAccumulated;

        var steps = 0;
        while (_accumulator >= FixedTimeStep && steps < MaxSubSteps)
        {
            Step(FixedTimeStep);
            _accumulator -= FixedTimeStep;
            steps++;
        }

        InterpolationAlpha = _accumulator / FixedTimeStep;
    }

    /// <summary>
    /// Runs one physics step of <paramref name="deltaTime"/> seconds.
    /// Executes: Tilemap Collider Rebuild -> Gravity -> Movement -> Collision Detection ->
    /// Collision Resolution -> Collision Events (<see cref="OnCollisionEnter"/>, then
    /// <see cref="OnCollision"/> for every current contact, then <see cref="OnCollisionExit"/>
    /// for contacts that ended).
    /// </summary>
    /// <param name="deltaTime">The time this step advances the simulation by, in seconds.</param>
    private void Step(float deltaTime)
    {
        _elapsedTime += deltaTime;

        // 1. Rebuild tilemap-derived colliders for any tilemap whose tiles changed
        _tilemapColliderSystem.Update(deltaTime);

        // 2. Apply gravity to dynamic bodies
        _gravitySystem.Update(deltaTime);

        // 3. Integrate velocity into position
        _movementSystem.Update(deltaTime);

        // 4. Detect collisions
        _collisionDetectionSystem.Detect();

        // 5. Resolve collisions (entities currently dropping through a one-way platform are
        // exempted from one-way resolution for the remainder of their drop-through window)
        var droppingThrough = GetActiveDropThroughEntities();
        _collisionResolutionSystem.Resolve(_collisionDetectionSystem.Manifolds, droppingThrough);

        // 6. Fire collision events: enter (new pairs) and stay (every current pair) first, then
        // exit (pairs from last step no longer present — including pairs where an entity was
        // destroyed between steps, since a destroyed entity's collider can no longer produce a
        // manifold and so silently drops out of "current").
        var currentPairs = new Dictionary<(Entity A, Entity B), CollisionManifold>();
        foreach (var manifold in _collisionDetectionSystem.Manifolds)
        {
            currentPairs[NormalizePair(manifold.EntityA, manifold.EntityB)] = manifold;
        }

        var enterHandler = OnCollisionEnter;
        if (enterHandler is not null)
        {
            foreach (var (pair, manifold) in currentPairs)
            {
                if (!_activePairs.ContainsKey(pair))
                    enterHandler(manifold);
            }
        }

        var stayHandler = OnCollision;
        if (stayHandler is not null)
        {
            foreach (var manifold in _collisionDetectionSystem.Manifolds)
            {
                stayHandler(manifold);
            }
        }

        var exitHandler = OnCollisionExit;
        if (exitHandler is not null)
        {
            foreach (var pair in _activePairs.Keys)
            {
                if (!currentPairs.ContainsKey(pair))
                    exitHandler(pair.A, pair.B);
            }
        }

        _activePairs = currentPairs;
    }

    /// <summary>
    /// Orders an entity pair by ascending <see cref="Entity.Id"/> so the same two entities
    /// always produce the same dictionary key regardless of which side of a manifold each
    /// landed on.
    /// </summary>
    private static (Entity A, Entity B) NormalizePair(Entity a, Entity b) =>
        a.Id <= b.Id ? (a, b) : (b, a);

    /// <summary>
    /// Returns the set of entities whose <see cref="DropThrough"/> window is still active,
    /// pruning any that have expired.
    /// </summary>
    private HashSet<Entity>? GetActiveDropThroughEntities()
    {
        if (_dropThroughUntil.Count == 0)
            return null;

        HashSet<Entity>? active = null;
        List<Entity>? expired = null;

        foreach (var (entity, until) in _dropThroughUntil)
        {
            if (until <= _elapsedTime)
            {
                expired ??= [];
                expired.Add(entity);
            }
            else
            {
                active ??= [];
                active.Add(entity);
            }
        }

        if (expired is not null)
        {
            foreach (var entity in expired)
                _dropThroughUntil.Remove(entity);
        }

        return active;
    }
}
