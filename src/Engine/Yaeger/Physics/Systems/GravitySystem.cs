using System.Numerics;
using Yaeger.ECS;
using Yaeger.Physics.Components;
using Yaeger.Systems;

namespace Yaeger.Physics.Systems;

/// <summary>
/// Applies gravitational acceleration to all dynamic rigid bodies.
/// </summary>
public class GravitySystem(World world, Vector2 gravity) : IUpdateSystem
{
    /// <summary>
    /// The global gravity vector. Default is (0, -9.81).
    /// </summary>
    public Vector2 Gravity { get; set; } = gravity;

    public GravitySystem(World world)
        : this(world, new Vector2(0, -9.81f)) { }

    public void Update(float deltaTime)
    {
        foreach (
            (Entity entity, RigidBody2D body, Velocity2D velocity) in world.Query<
                RigidBody2D,
                Velocity2D
            >()
        )
        {
            if (body.Type != BodyType.Dynamic)
                continue;

            var newVelocity = velocity;
            newVelocity.Linear += Gravity * body.GravityScale * deltaTime;
            world.AddComponent(entity, newVelocity);
        }
    }
}
