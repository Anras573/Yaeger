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

    /// <summary>
    /// Fired for each collision detected during a step.
    /// </summary>
    public event Action<CollisionManifold>? OnCollision;

    /// <summary>
    /// The collision manifolds from the last physics step.
    /// </summary>
    public IReadOnlyList<CollisionManifold> Manifolds => _collisionDetectionSystem.Manifolds;

    /// <summary>
    /// Creates a new 2D physics world with the specified gravity.
    /// </summary>
    public PhysicsWorld2D(World world, Vector2? gravity = null)
    {
        var g = gravity ?? new Vector2(0, -9.81f);
        _gravitySystem = new GravitySystem(world, g);
        _movementSystem = new MovementSystem(world);
        _collisionDetectionSystem = new CollisionDetectionSystem(world);
        _collisionResolutionSystem = new CollisionResolutionSystem(world);
    }

    /// <summary>
    /// Steps the physics simulation forward by deltaTime seconds.
    /// Executes: Gravity -> Movement -> Collision Detection -> Collision Resolution.
    /// </summary>
    /// <param name="deltaTime">The time elapsed since the last step, in seconds.</param>
    public void Update(float deltaTime)
    {
        // 1. Apply gravity to dynamic bodies
        _gravitySystem.Update(deltaTime);

        // 2. Integrate velocity into position
        _movementSystem.Update(deltaTime);

        // 3. Detect collisions
        _collisionDetectionSystem.Detect();

        // 4. Resolve collisions
        _collisionResolutionSystem.Resolve(_collisionDetectionSystem.Manifolds);

        // 5. Fire collision events
        var handler = OnCollision;
        if (handler is not null)
        {
            foreach (var manifold in _collisionDetectionSystem.Manifolds)
            {
                handler(manifold);
            }
        }
    }
}
