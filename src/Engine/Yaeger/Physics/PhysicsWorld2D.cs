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
    // wholesale (not mutated) at the end of each Update — see the comment there.
    private Dictionary<(Entity A, Entity B), CollisionManifold> _activePairs = new();

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
    public PhysicsWorld2D(World world, Vector2? gravity = null, float broadphaseCellSize = 1.0f)
    {
        if (broadphaseCellSize <= 0 || !float.IsFinite(broadphaseCellSize))
            throw new ArgumentOutOfRangeException(
                nameof(broadphaseCellSize),
                broadphaseCellSize,
                "Cell size must be a positive finite value."
            );

        var g = gravity ?? new Vector2(0, -9.81f);
        _tilemapColliderSystem = new TilemapColliderSystem(world);
        _gravitySystem = new GravitySystem(world, g);
        _movementSystem = new MovementSystem(world);
        _collisionDetectionSystem = new CollisionDetectionSystem(world, broadphaseCellSize);
        _collisionResolutionSystem = new CollisionResolutionSystem(world);
    }

    /// <summary>
    /// Steps the physics simulation forward by deltaTime seconds.
    /// Executes: Tilemap Collider Rebuild -> Gravity -> Movement -> Collision Detection ->
    /// Collision Resolution -> Collision Events (<see cref="OnCollisionEnter"/>, then
    /// <see cref="OnCollision"/> for every current contact, then <see cref="OnCollisionExit"/>
    /// for contacts that ended).
    /// </summary>
    /// <param name="deltaTime">The time elapsed since the last step, in seconds.</param>
    public void Update(float deltaTime)
    {
        // 1. Rebuild tilemap-derived colliders for any tilemap whose tiles changed
        _tilemapColliderSystem.Update(deltaTime);

        // 2. Apply gravity to dynamic bodies
        _gravitySystem.Update(deltaTime);

        // 3. Integrate velocity into position
        _movementSystem.Update(deltaTime);

        // 4. Detect collisions
        _collisionDetectionSystem.Detect();

        // 5. Resolve collisions
        _collisionResolutionSystem.Resolve(_collisionDetectionSystem.Manifolds);

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
}
